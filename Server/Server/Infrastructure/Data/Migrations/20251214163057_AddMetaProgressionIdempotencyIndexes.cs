using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMetaProgressionIdempotencyIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Conflicting history must fail the unique constraint, never discard quests or ledger entries.

            migrationBuilder.CreateIndex(
                name: "IX_UserQuests_UserId_QuestDefinitionId",
                table: "UserQuests",
                columns: new[] { "UserId", "QuestDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoinTransactions_UserId_Type_RelatedEntityId",
                table: "CoinTransactions",
                columns: new[] { "UserId", "Type", "RelatedEntityId" },
                unique: true,
                filter: "[RelatedEntityId] IS NOT NULL AND [Type] IN (1, 2, 3, 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserQuests_UserId_QuestDefinitionId",
                table: "UserQuests");

            migrationBuilder.DropIndex(
                name: "IX_CoinTransactions_UserId_Type_RelatedEntityId",
                table: "CoinTransactions");
        }
    }
}
