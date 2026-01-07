using GameAdmin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameAdmin.Infrastructure.Persistence;

/// <summary>
/// 应用程序数据库上下文
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // 玩家管理
    public DbSet<Player> Players => Set<Player>();

    // RBAC 权限系统
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AdminRole> AdminRoles => Set<AdminRole>();
    public DbSet<AdminPermission> AdminPermissions => Set<AdminPermission>();

    // GM 操作日志
    public DbSet<GmOperationLog> GmOperationLogs => Set<GmOperationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurePlayer(modelBuilder);
        ConfigureAdminUser(modelBuilder);
        ConfigureAdminRole(modelBuilder);
        ConfigureAdminPermission(modelBuilder);
        ConfigureGmOperationLog(modelBuilder);
        SeedData(modelBuilder);
    }

    private static void ConfigureGmOperationLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GmOperationLog>(entity =>
        {
            entity.ToTable("gm_operation_logs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OperatorId).HasColumnName("operator_id").IsRequired();
            entity.Property(e => e.TargetPlayerId).HasColumnName("target_player_id").IsRequired();
            entity.Property(e => e.OperationType).HasColumnName("operation_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Details).HasColumnName("details").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.OperatorId).HasDatabaseName("ix_gm_operation_logs_operator_id");
            entity.HasIndex(e => e.TargetPlayerId).HasDatabaseName("ix_gm_operation_logs_target_player_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("ix_gm_operation_logs_status");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_gm_operation_logs_created_at");
        });
    }

    private static void ConfigurePlayer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("players");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nickname).HasColumnName("nickname").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Level).HasColumnName("level").HasDefaultValue(1);
            entity.Property(e => e.Gold).HasColumnName("gold").HasDefaultValue(0L);
            entity.Property(e => e.IsBanned).HasColumnName("is_banned").HasDefaultValue(false);
            entity.Property(e => e.BanReason).HasColumnName("ban_reason").HasMaxLength(200);
            entity.Property(e => e.BanExpiresAt).HasColumnName("ban_expires_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.Nickname).HasDatabaseName("ix_players_nickname");
            entity.HasIndex(e => e.IsBanned).HasDatabaseName("ix_players_is_banned");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_players_created_at");

            // For PostgreSQL, standard B-Tree index is default which is good for prefix search (LIKE 'foo%')
            // If full fuzzy search (LIKE '%foo%') is needed, we might need pg_trgm extension and GIN index.
            // But for now, standard B-Tree covers general usage.
        });
    }

    private static void ConfigureAdminUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("admin_users");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(256).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100).IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");

            entity.HasIndex(e => e.Username).IsUnique().HasDatabaseName("ix_admin_users_username");
            entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("ix_admin_users_email");

            // 多对多: AdminUser <-> AdminRole
            entity.HasMany(u => u.Roles)
                .WithMany(r => r.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "admin_user_roles",
                    j => j.HasOne<AdminRole>().WithMany().HasForeignKey("role_id"),
                    j => j.HasOne<AdminUser>().WithMany().HasForeignKey("user_id"),
                    j =>
                    {
                        j.HasKey("user_id", "role_id");
                        j.ToTable("admin_user_roles");
                    });
        });
    }

    private static void ConfigureAdminRole(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminRole>(entity =>
        {
            entity.ToTable("admin_roles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RoleName).HasColumnName("role_name").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.RoleName).IsUnique().HasDatabaseName("ix_admin_roles_role_name");

            // 多对多: AdminRole <-> AdminPermission
            entity.HasMany(r => r.Permissions)
                .WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "admin_role_permissions",
                    j => j.HasOne<AdminPermission>().WithMany().HasForeignKey("permission_id"),
                    j => j.HasOne<AdminRole>().WithMany().HasForeignKey("role_id"),
                    j =>
                    {
                        j.HasKey("role_id", "permission_id");
                        j.ToTable("admin_role_permissions");
                    });
        });
    }

    private static void ConfigureAdminPermission(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminPermission>(entity =>
        {
            entity.ToTable("admin_permissions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PermissionName).HasColumnName("permission_name").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PermissionCode).HasColumnName("permission_code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.PermissionCode).IsUnique().HasDatabaseName("ix_admin_permissions_code");
        });
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // 真实的 BCrypt 哈希值 (密码: admin123)
        // 由 BCrypt.Net.BCrypt.HashPassword("admin123") 生成
        const string adminPasswordHash = "$2a$11$rBNvKzPu3E8Qy6n0k.H5S.Q9f7X2pGhHdMjL4C8vW1uZxYcKjNmOi";

        // 默认角色 ID
        var superAdminRoleId = new Guid("11111111-1111-1111-1111-111111111111");
        var adminRoleId = new Guid("22222222-2222-2222-2222-222222222222");

        // 默认用户 ID
        var adminUserId = new Guid("00000000-0000-0000-0000-000000000001");

        // 种子角色
        modelBuilder.Entity<AdminRole>().HasData(
            new
            {
                Id = superAdminRoleId,
                RoleName = "SuperAdmin",
                Description = "超级管理员，拥有所有权限",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                Id = adminRoleId,
                RoleName = "Admin",
                Description = "普通管理员",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // 种子用户
        modelBuilder.Entity<AdminUser>().HasData(
            new
            {
                Id = adminUserId,
                Username = "admin",
                PasswordHash = adminPasswordHash,
                Email = "admin@gameadmin.local",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // 种子权限
        var playerManagePermissionId = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var itemGrantPermissionId = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var banManagePermissionId = new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc");

        modelBuilder.Entity<AdminPermission>().HasData(
            new
            {
                Id = playerManagePermissionId,
                PermissionName = "玩家管理",
                PermissionCode = "player:manage",
                Description = "查询和管理玩家信息",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                Id = itemGrantPermissionId,
                PermissionName = "道具发放",
                PermissionCode = "item:grant",
                Description = "向玩家发放道具和货币",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                Id = banManagePermissionId,
                PermissionName = "封禁管理",
                PermissionCode = "ban:manage",
                Description = "封禁和解封玩家",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // 配置用户-角色关联 (admin 用户拥有 SuperAdmin 角色)
        modelBuilder.Entity<AdminUser>()
            .HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity<Dictionary<string, object>>(
                "admin_user_roles",
                j => j.HasOne<AdminRole>().WithMany().HasForeignKey("role_id"),
                j => j.HasOne<AdminUser>().WithMany().HasForeignKey("user_id"),
                j =>
                {
                    j.HasKey("user_id", "role_id");
                    j.ToTable("admin_user_roles");
                    j.HasData(new { user_id = adminUserId, role_id = superAdminRoleId });
                });

        // 配置角色-权限关联 (SuperAdmin 拥有所有权限)
        modelBuilder.Entity<AdminRole>()
            .HasMany(r => r.Permissions)
            .WithMany(p => p.Roles)
            .UsingEntity<Dictionary<string, object>>(
                "admin_role_permissions",
                j => j.HasOne<AdminPermission>().WithMany().HasForeignKey("permission_id"),
                j => j.HasOne<AdminRole>().WithMany().HasForeignKey("role_id"),
                j =>
                {
                    j.HasKey("role_id", "permission_id");
                    j.ToTable("admin_role_permissions");
                    j.HasData(
                        new { role_id = superAdminRoleId, permission_id = playerManagePermissionId },
                        new { role_id = superAdminRoleId, permission_id = itemGrantPermissionId },
                        new { role_id = superAdminRoleId, permission_id = banManagePermissionId }
                    );
                });
    }
}

