using GameAdmin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameAdmin.Infrastructure.Persistence;

/// <summary>
/// 数据库初始化器
/// 用于在开发环境下确保基础数据存在
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        // 设置 5 秒连接超时
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var token = cts.Token;

        try
        {
            // 1. 检查是否已有用户 (Async with cancellation)
            // 如果连接池满或网络不通，这里会抛出 OperationCanceledException
            if (await context.AdminUsers.AnyAsync(token))
            {
                logger.LogInformation("Database already seeded.");
                return;
            }

            logger.LogInformation("正在初始化默认管理员账号...");

            // 2. 确保 SuperAdmin 角色存在
            var superAdminRole = await context.AdminRoles.FirstOrDefaultAsync(r => r.RoleName == "SuperAdmin", token);
            if (superAdminRole == null)
            {
                superAdminRole = new AdminRole
                {
                    Id = Guid.NewGuid(),
                    RoleName = "SuperAdmin",
                    Description = "超级管理员",
                    CreatedAt = DateTime.UtcNow
                };
                context.AdminRoles.Add(superAdminRole);
                await context.SaveChangesAsync(token);
                logger.LogInformation("Created default SuperAdmin role.");
            }

            // 3. 创建默认 Admin 用户
            // 密码：admin123
            var adminUser = AdminUser.Create(
                username: "admin",
                passwordHash: BCrypt.Net.BCrypt.HashPassword("admin123"),
                email: "admin@gameadmin.local"
            );

            adminUser.Roles.Add(superAdminRole);

            context.AdminUsers.Add(adminUser);
            await context.SaveChangesAsync(token);

            logger.LogInformation("Default admin user 'admin' created successfully.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[ERROR] 数据库连接或初始化超时 (5s)。跳过数据播种。");
            logger.LogError("Database initialization timed out.");
            // 不抛出异常，允许应用启动（可能导致后续报错，但至少能启动调试）
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] 数据库初始化失败: {ex.Message}");
            logger.LogError(ex, "Database initialization failed.");
        }
    }
}
