using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerBanDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ban_expires_at",
                table: "players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ban_reason",
                table: "players",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ban_expires_at",
                table: "players");

            migrationBuilder.DropColumn(
                name: "ban_reason",
                table: "players");
        }
    }
}
