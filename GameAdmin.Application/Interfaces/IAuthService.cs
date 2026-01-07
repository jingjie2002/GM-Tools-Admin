using GameAdmin.Application.DTOs;

namespace GameAdmin.Application.Interfaces;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 管理员登录
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>登录响应，失败返回 null</returns>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证二次操作密码
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="password">二次密码</param>
    /// <returns>验证结果</returns>
    Task<bool> ValidateSecondaryPasswordAsync(Guid userId, string password);
}
