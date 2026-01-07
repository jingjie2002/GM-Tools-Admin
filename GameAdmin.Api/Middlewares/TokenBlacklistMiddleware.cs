using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using GameAdmin.Infrastructure.Services;

namespace GameAdmin.Api.Middlewares;

/// <summary>
/// Token 黑名单检查中间件
/// </summary>
public class TokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenBlacklistMiddleware> _logger;

    public TokenBlacklistMiddleware(RequestDelegate next, ILogger<TokenBlacklistMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IRedisService redisService)
    {
        // 1. 检查 Token 是否被注销 (Logout)
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader["Bearer ".Length..];

            // 检查 Token 是否在黑名单中
            if (await redisService.IsBlacklistedAsync(token))
            {
                _logger.LogWarning("Blacklisted token detected");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Token 已失效，请重新登录"
                });
                return;
            }
        }

        // 2. 检查用户是否被强制下线 (Banned)
        // 注意：此检查需要在 UseAuthentication 之后执行
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                if (await redisService.IsUserBlacklistedAsync(userId))
                {
                    _logger.LogWarning("Blacklisted user detected: {UserId}", userId);
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        success = false,
                        message = "账号已被封禁或强制下线"
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}

/// <summary>
/// 中间件扩展方法
/// </summary>
public static class TokenBlacklistMiddlewareExtensions
{
    public static IApplicationBuilder UseTokenBlacklist(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TokenBlacklistMiddleware>();
    }
}
