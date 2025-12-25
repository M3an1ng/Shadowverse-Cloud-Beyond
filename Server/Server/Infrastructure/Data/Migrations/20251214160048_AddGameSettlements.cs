using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Result = table.Column<int>(type: "INTEGER", nullable: false),
                    WinnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSettlements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameSettlements_GameId_UserId",
                table: "GameSettlements",
                columns: new[] { "GameId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameSettlements_SettledAtUtc",
                table: "GameSettlements",
                column: "SettledAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GameSettlements_UserId",
                table: "GameSettlements",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameSettlements");
        }
    }
}
