namespace GameAdmin.Application.DTOs;

/// <summary>
/// 登录请求 DTO
/// </summary>
public record LoginRequest(
    string Username,
    string Password
);

/// <summary>
/// 登录响应 DTO
/// </summary>
public record LoginResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string Username,
    IReadOnlyList<string> Roles
);
