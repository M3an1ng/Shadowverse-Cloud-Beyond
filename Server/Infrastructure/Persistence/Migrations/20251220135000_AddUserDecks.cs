using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserDecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Class = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDecks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDeckCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeckId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDeckCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDeckCards_CardDefinitions_CardDefinitionId",
                        column: x => x.CardDefinitionId,
                        principalTable: "CardDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserDeckCards_UserDecks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "UserDecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDeckCards_CardDefinitionId",
                table: "UserDeckCards",
                column: "CardDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDeckCards_DeckId_CardDefinitionId",
                table: "UserDeckCards",
                columns: new[] { "DeckId", "CardDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDecks_UpdatedAt",
                table: "UserDecks",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserDecks_UserId",
                table: "UserDecks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDeckCards");

            migrationBuilder.DropTable(
                name: "UserDecks");
        }
    }
}
