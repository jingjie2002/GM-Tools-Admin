namespace GameAdmin.Domain.Entities;

/// <summary>
/// 管理员权限实体
/// </summary>
public class AdminPermission
{
    public required Guid Id { get; init; }
    
    /// <summary>
    /// 权限名称 (显示用)
    /// </summary>
    public required string PermissionName { get; set; }
    
    /// <summary>
    /// 权限代码 (如 "player:manage", "item:grant", "ban:create")
    /// </summary>
    public required string PermissionCode { get; set; }
    
    public string? Description { get; set; }
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// 拥有该权限的角色 (多对多)
    /// </summary>
    public ICollection<AdminRole> Roles { get; set; } = [];

    public static AdminPermission Create(string permissionName, string permissionCode, string? description = null)
    {
        return new AdminPermission
        {
            Id = Guid.NewGuid(),
            PermissionName = permissionName,
            PermissionCode = permissionCode,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }
}
