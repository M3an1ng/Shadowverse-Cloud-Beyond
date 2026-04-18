using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260302103000_AddModeToWinrateStats")]
    public partial class AddModeToWinrateStats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "GameParticipantStatsFacts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "DeckStatsAggs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "CardStatsAggs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropIndex(
                name: "IX_GameParticipantStatsFacts_Environment_EndedAtUtc",
                table: "GameParticipantStatsFacts");

            migrationBuilder.DropIndex(
                name: "IX_DeckStatsAggs_Environment_DeckShapeHash",
                table: "DeckStatsAggs");

            migrationBuilder.DropIndex(
                name: "IX_CardStatsAggs_Environment_CardDefinitionId",
                table: "CardStatsAggs");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipantStatsFacts_Environment_Mode_EndedAtUtc",
                table: "GameParticipantStatsFacts",
                columns: new[] { "Environment", "Mode", "EndedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeckStatsAggs_Environment_Mode_DeckShapeHash",
                table: "DeckStatsAggs",
                columns: new[] { "Environment", "Mode", "DeckShapeHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardStatsAggs_Environment_Mode_CardDefinitionId",
                table: "CardStatsAggs",
                columns: new[] { "Environment", "Mode", "CardDefinitionId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameParticipantStatsFacts_Environment_Mode_EndedAtUtc",
                table: "GameParticipantStatsFacts");

            migrationBuilder.DropIndex(
                name: "IX_DeckStatsAggs_Environment_Mode_DeckShapeHash",
                table: "DeckStatsAggs");

            migrationBuilder.DropIndex(
                name: "IX_CardStatsAggs_Environment_Mode_CardDefinitionId",
                table: "CardStatsAggs");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipantStatsFacts_Environment_EndedAtUtc",
                table: "GameParticipantStatsFacts",
                columns: new[] { "Environment", "EndedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeckStatsAggs_Environment_DeckShapeHash",
                table: "DeckStatsAggs",
                columns: new[] { "Environment", "DeckShapeHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardStatsAggs_Environment_CardDefinitionId",
                table: "CardStatsAggs",
                columns: new[] { "Environment", "CardDefinitionId" },
                unique: true);

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "GameParticipantStatsFacts");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "DeckStatsAggs");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "CardStatsAggs");
        }
    }
}
