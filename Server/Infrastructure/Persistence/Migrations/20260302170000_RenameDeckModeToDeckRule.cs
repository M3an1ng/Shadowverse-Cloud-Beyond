using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260302170000_RenameDeckModeToDeckRule")]
    public partial class RenameDeckModeToDeckRule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite does not support RenameIndexOperation in EF Core.
            // Drop and recreate indexes with the new names after column rename.
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_GameParticipantStatsFacts_Environment_Mode_EndedAtUtc\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_DeckStatsAggs_Environment_Mode_DeckShapeHash\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_CardStatsAggs_Environment_Mode_CardDefinitionId\";");

            migrationBuilder.RenameColumn(
                name: "Mode",
                table: "UserDecks",
                newName: "Rule");

            migrationBuilder.RenameColumn(
                name: "Mode",
                table: "GameParticipantStatsFacts",
                newName: "Rule");

            migrationBuilder.RenameColumn(
                name: "Mode",
                table: "DeckStatsAggs",
                newName: "Rule");

            migrationBuilder.RenameColumn(
                name: "Mode",
                table: "CardStatsAggs",
                newName: "Rule");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipantStatsFacts_Environment_Rule_EndedAtUtc",
                table: "GameParticipantStatsFacts",
                columns: new[] { "Environment", "Rule", "EndedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeckStatsAggs_Environment_Rule_DeckShapeHash",
                table: "DeckStatsAggs",
                columns: new[] { "Environment", "Rule", "DeckShapeHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardStatsAggs_Environment_Rule_CardDefinitionId",
                table: "CardStatsAggs",
                columns: new[] { "Environment", "Rule", "CardDefinitionId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameParticipantStatsFacts_Environment_Rule_EndedAtUtc",
                table: "GameParticipantStatsFacts");

            migrationBuilder.DropIndex(
                name: "IX_DeckStatsAggs_Environment_Rule_DeckShapeHash",
                table: "DeckStatsAggs");

            migrationBuilder.DropIndex(
                name: "IX_CardStatsAggs_Environment_Rule_CardDefinitionId",
                table: "CardStatsAggs");

            migrationBuilder.RenameColumn(
                name: "Rule",
                table: "UserDecks",
                newName: "Mode");

            migrationBuilder.RenameColumn(
                name: "Rule",
                table: "GameParticipantStatsFacts",
                newName: "Mode");

            migrationBuilder.RenameColumn(
                name: "Rule",
                table: "DeckStatsAggs",
                newName: "Mode");

            migrationBuilder.RenameColumn(
                name: "Rule",
                table: "CardStatsAggs",
                newName: "Mode");

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
    }
}
