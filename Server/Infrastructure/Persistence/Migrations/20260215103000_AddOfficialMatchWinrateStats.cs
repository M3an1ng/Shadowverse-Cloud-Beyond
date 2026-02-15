using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260215103000_AddOfficialMatchWinrateStats")]
    public partial class AddOfficialMatchWinrateStats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardStatsAggs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Environment = table.Column<int>(type: "INTEGER", nullable: false),
                    CardDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeckGames = table.Column<int>(type: "INTEGER", nullable: false),
                    DeckWins = table.Column<int>(type: "INTEGER", nullable: false),
                    DrawnGames = table.Column<int>(type: "INTEGER", nullable: false),
                    DrawnWins = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayedGames = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayedWins = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardStatsAggs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeckStatsAggs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Environment = table.Column<int>(type: "INTEGER", nullable: false),
                    DeckShapeHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DeckClass = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalGames = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalWins = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckStatsAggs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameParticipantStatsFacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsWinner = table.Column<bool>(type: "INTEGER", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeckClass = table.Column<int>(type: "INTEGER", nullable: false),
                    Environment = table.Column<int>(type: "INTEGER", nullable: false),
                    DeckShapeHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UserDeckId = table.Column<Guid>(type: "TEXT", nullable: true),
                    InitialDeckCardCountsJson = table.Column<string>(type: "TEXT", nullable: false),
                    DrawnCardIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PlayedCardIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsOfficial = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameParticipantStatsFacts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardStatsAggs_Environment_CardDefinitionId",
                table: "CardStatsAggs",
                columns: new[] { "Environment", "CardDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardStatsAggs_UpdatedAtUtc",
                table: "CardStatsAggs",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DeckStatsAggs_Environment_DeckShapeHash",
                table: "DeckStatsAggs",
                columns: new[] { "Environment", "DeckShapeHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeckStatsAggs_UpdatedAtUtc",
                table: "DeckStatsAggs",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipantStatsFacts_Environment_EndedAtUtc",
                table: "GameParticipantStatsFacts",
                columns: new[] { "Environment", "EndedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipantStatsFacts_GameId_PlayerId",
                table: "GameParticipantStatsFacts",
                columns: new[] { "GameId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipantStatsFacts_IsOfficial",
                table: "GameParticipantStatsFacts",
                column: "IsOfficial");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardStatsAggs");

            migrationBuilder.DropTable(
                name: "DeckStatsAggs");

            migrationBuilder.DropTable(
                name: "GameParticipantStatsFacts");
        }
    }
}
