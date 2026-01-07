using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGmOperationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gm_operation_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    details = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gm_operation_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gm_operation_logs_created_at",
                table: "gm_operation_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_gm_operation_logs_operator_id",
                table: "gm_operation_logs",
                column: "operator_id");

            migrationBuilder.CreateIndex(
                name: "ix_gm_operation_logs_target_player_id",
                table: "gm_operation_logs",
                column: "target_player_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gm_operation_logs");
        }
    }
}
