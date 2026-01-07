namespace GameAdmin.Domain.Entities;

/// <summary>
/// 管理员角色实体
/// </summary>
public class AdminRole
{
    public required Guid Id { get; init; }
    public required string RoleName { get; set; }
    public string? Description { get; set; }
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// 拥有该角色的用户 (多对多)
    /// </summary>
    public ICollection<AdminUser> Users { get; set; } = [];

    /// <summary>
    /// 角色拥有的权限 (多对多)
    /// </summary>
    public ICollection<AdminPermission> Permissions { get; set; } = [];

    public static AdminRole Create(string roleName, string? description = null)
    {
        return new AdminRole
        {
            Id = Guid.NewGuid(),
            RoleName = roleName,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }
}
