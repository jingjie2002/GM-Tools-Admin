using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGmOperationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "gm_operation_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by",
                table: "gm_operation_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "gm_operation_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_gm_operation_logs_status",
                table: "gm_operation_logs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_gm_operation_logs_status",
                table: "gm_operation_logs");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "gm_operation_logs");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "gm_operation_logs");

            migrationBuilder.DropColumn(
                name: "status",
                table: "gm_operation_logs");
        }
    }
}
