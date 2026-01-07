using System.Security.Claims;
using GameAdmin.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GameAdmin.Api.Filters;

/// <summary>
/// 二次密码验证过滤器
/// 用于敏感操作（大额扣除、批量操作等）
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequireSecondaryAuthAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequireSecondaryAuthAttribute>>();

        // 1. 获取 Header 中的二次验证密码
        var secondaryPassword = httpContext.Request.Headers["X-Secondary-Password"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(secondaryPassword))
        {
            logger.LogWarning("Secondary auth failed: Missing X-Secondary-Password header");
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "需要二次验证，请提供操作密码",
                code = "SECONDARY_AUTH_REQUIRED"
            });
            return;
        }

        // 2. 获取当前用户 ID
        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "无效的用户身份" });
            return;
        }

        // 3. 通过 AuthService 验证密码
        var authService = httpContext.RequestServices.GetRequiredService<IAuthService>();
        var isValid = await authService.ValidateSecondaryPasswordAsync(userId, secondaryPassword);

        if (!isValid)
        {
            logger.LogWarning("Secondary auth failed for user {UserId}: Invalid password", userId);
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "二次验证失败，操作密码错误",
                code = "SECONDARY_AUTH_FAILED"
            });
            return;
        }

        logger.LogInformation("Secondary auth passed for user {UserId}", userId);

        // 4. 验证通过，继续执行
        await next();
    }
}
