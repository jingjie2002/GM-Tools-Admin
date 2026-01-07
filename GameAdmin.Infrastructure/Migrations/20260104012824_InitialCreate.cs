using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nickname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    gold = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    is_banned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_players_created_at",
                table: "players",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_players_is_banned",
                table: "players",
                column: "is_banned");

            migrationBuilder.CreateIndex(
                name: "ix_players_nickname",
                table: "players",
                column: "nickname");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "players");
        }
    }
}
