using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRbacEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    permission_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "admin_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "admin_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "admin_role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_admin_role_permissions_admin_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "admin_permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_admin_role_permissions_admin_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "admin_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admin_user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_admin_user_roles_admin_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "admin_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_admin_user_roles_admin_users_user_id",
                        column: x => x.user_id,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_permissions_code",
                table: "admin_permissions",
                column: "permission_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_role_permissions_permission_id",
                table: "admin_role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_admin_roles_role_name",
                table: "admin_roles",
                column: "role_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_user_roles_role_id",
                table: "admin_user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_email",
                table: "admin_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_username",
                table: "admin_users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_role_permissions");

            migrationBuilder.DropTable(
                name: "admin_user_roles");

            migrationBuilder.DropTable(
                name: "admin_permissions");

            migrationBuilder.DropTable(
                name: "admin_roles");

            migrationBuilder.DropTable(
                name: "admin_users");
        }
    }
}
