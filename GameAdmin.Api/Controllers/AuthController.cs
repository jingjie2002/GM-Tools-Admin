using GameAdmin.Application.DTOs;
using GameAdmin.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameAdmin.Api.Controllers;

/// <summary>
/// 认证控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// 管理员登录
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>JWT Token 或 401 错误</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "用户名和密码不能为空" });
        }

        var result = await authService.LoginAsync(request, cancellationToken);

        if (result is null)
        {
            return Unauthorized(new { message = "用户名或密码错误" });
        }

        return Ok(result);
    }

    /// <summary>
    /// 获取当前用户信息 (需要认证)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();

        return Ok(new
        {
            UserId = userId,
            Username = username,
            Email = email,
            Roles = roles
        });
    }
}
