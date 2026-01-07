namespace GameAdmin.Domain.Entities;

/// <summary>
/// 管理员用户实体
/// </summary>
public class AdminUser
{
    public required Guid Id { get; init; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required string Email { get; set; }
    public required bool IsActive { get; set; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 用户拥有的角色 (多对多)
    /// </summary>
    public ICollection<AdminRole> Roles { get; set; } = [];

    public static AdminUser Create(string username, string passwordHash, string email)
    {
        return new AdminUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = passwordHash,
            Email = email,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
