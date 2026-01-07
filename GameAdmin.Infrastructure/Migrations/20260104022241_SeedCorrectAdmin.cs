using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GameAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedCorrectAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "admin_permissions",
                columns: new[] { "id", "created_at", "description", "permission_code", "permission_name" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "查询和管理玩家信息", "player:manage", "玩家管理" },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "向玩家发放道具和货币", "item:grant", "道具发放" },
                    { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "封禁和解封玩家", "ban:manage", "封禁管理" }
                });

            migrationBuilder.InsertData(
                table: "admin_roles",
                columns: new[] { "id", "created_at", "description", "role_name" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "超级管理员，拥有所有权限", "SuperAdmin" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "普通管理员", "Admin" }
                });

            migrationBuilder.InsertData(
                table: "admin_users",
                columns: new[] { "id", "created_at", "email", "is_active", "last_login_at", "password_hash", "username" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@gameadmin.local", true, null, "$2a$11$rBNvKzPu3E8Qy6n0k.H5S.Q9f7X2pGhHdMjL4C8vW1uZxYcKjNmOi", "admin" });

            migrationBuilder.InsertData(
                table: "admin_role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new Guid("11111111-1111-1111-1111-111111111111") }
                });

            migrationBuilder.InsertData(
                table: "admin_user_roles",
                columns: new[] { "role_id", "user_id" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("00000000-0000-0000-0000-000000000001") });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "admin_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.DeleteData(
                table: "admin_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.DeleteData(
                table: "admin_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), new Guid("11111111-1111-1111-1111-111111111111") });

            migrationBuilder.DeleteData(
                table: "admin_roles",
                keyColumn: "id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "admin_user_roles",
                keyColumns: new[] { "role_id", "user_id" },
                keyValues: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "admin_permissions",
                keyColumn: "id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            migrationBuilder.DeleteData(
                table: "admin_permissions",
                keyColumn: "id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            migrationBuilder.DeleteData(
                table: "admin_permissions",
                keyColumn: "id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"));

            migrationBuilder.DeleteData(
                table: "admin_roles",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "admin_users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));
        }
    }
}
