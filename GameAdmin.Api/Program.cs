using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using GameAdmin.Api.Filters;
using GameAdmin.Api.Middlewares;
using GameAdmin.Application.Interfaces;
using GameAdmin.Infrastructure.Services;
using GameAdmin.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Serilog;
using Serilog.Events;

Console.WriteLine(">>> [探针] 正在初始化 WebApplicationBuilder...");

var builder = WebApplication.CreateBuilder(args);

// 配置 Serilog
Console.WriteLine(">>> [探针] 正在配置 Serilog (Seq/Console)...");
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}      {Message:lj}{NewLine}{Exception}")
    .WriteTo.Seq("http://localhost:5341"));

Console.WriteLine(">>> [探针] 正在注册服务...");
// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidateModelStateFilter>();
    options.Filters.Add<HttpResponseExceptionFilter>();
})
.ConfigureApiBehaviorOptions(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

// 注册 FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// 配置 CORS - SignalR 必须使用 AllowCredentials
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",    // Vite dev server (localhost)
                "http://127.0.0.1:5173"     // Vite dev server (loopback IP)
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();  // SignalR 必需
    });
});

// 注册 Redis (Fail-Fast: 连接失败则终止启动)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis connection string is not configured");

Console.WriteLine(">>> [探针] 正在连接 Redis...");
IConnectionMultiplexer redisConnection;
try
{
    redisConnection = ConnectionMultiplexer.Connect(redisConnectionString);
    
    // 验证连接有效性
    if (!redisConnection.IsConnected)
    {
        throw new InvalidOperationException("Redis 连接建立后状态异常：IsConnected = false");
    }
    
    // PING 测试
    var db = redisConnection.GetDatabase();
    var pingResult = db.Ping();
    Console.WriteLine($">>> [探针] Redis 连接成功！PING 延迟: {pingResult.TotalMilliseconds:F2}ms");
}
catch (Exception ex)
{
    Console.WriteLine($"[FATAL] >>> Redis 连接失败，应用无法启动: {ex.Message}");
    throw new InvalidOperationException($"Redis 连接失败（Fail-Fast）: {ex.Message}", ex);
}

builder.Services.AddSingleton<IConnectionMultiplexer>(redisConnection);
builder.Services.AddScoped<IRedisService, RedisService>();

// 注册应用服务
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// 注册批量封禁队列服务 (单例 + 后台服务)
builder.Services.AddSingleton<GameAdmin.Infrastructure.Services.BanQueueService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GameAdmin.Infrastructure.Services.BanQueueService>());

// 注册 SignalR 广播桥接服务
builder.Services.AddHostedService<GameAdmin.Api.Services.SignalRBroadcastService>();

// 配置 PostgreSQL 数据库上下文
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("GameAdmin.Infrastructure");
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        });
    
    // 启用 SQL 日志输出到控制台
    options.LogTo(Console.WriteLine, LogLevel.Information);
    options.EnableSensitiveDataLogging(); // 显示参数值（仅开发环境）
});

// 配置 JWT Bearer 认证
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };

    // SignalR WebSocket Token 支持：从 QueryString 提取 access_token
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            // 仅对 SignalR Hub 路径生效
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// 注册 SignalR
builder.Services.AddSignalR();

Console.WriteLine(">>> [探针] 正在构建 App...");
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // 安全注入：初始化数据库种子数据 (Async with Timeout)
    Console.WriteLine(">>> [探针] 准备进入数据库初始化流程...");

    using var scope = app.Services.CreateScope();
    try
    {
        // 强制 10 秒总超时
        var initTask = DbInitializer.InitializeAsync(app.Services);

        using var tempCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await initTask.WaitAsync(tempCts.Token);

        Console.WriteLine(">>> [探针] 数据库初始化完成");
    }
    catch (TimeoutException)
    {
        Console.WriteLine("[ERROR] >>> [探针] 警告：数据库初始化总耗时超过 10 秒，已跳过。");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] >>> [探针] 数据库初始化异常: {ex.Message}");
    }
}

// 启用 CORS (必须在认证之前)
app.UseCors("SignalRPolicy");

// 启用认证
app.UseAuthentication();

// 启用 Serilog HTTP 请求日志
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = ">>> [HTTP] {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});

// 启用 Token 黑名单检查 (需在认证后，以获取 User Claims)
app.UseTokenBlacklist();

// 启用授权
app.UseAuthorization();

app.MapControllers();

// 映射 SignalR Hub
app.MapHub<GameAdmin.Api.Hubs.GmHub>("/hubs/gm");

// 配置优雅关闭
app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine(">>> [探针] 正在关闭应用，释放资源...");
});

app.Lifetime.ApplicationStopped.Register(() =>
{
    Console.WriteLine(">>> [探针] 应用已完全停止，端口已释放");
});

Console.WriteLine(">>> [探针] 应用已就绪，正在启动 HTTP 服务监听...");
app.Run();
