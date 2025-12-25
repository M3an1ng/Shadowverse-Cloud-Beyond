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
            // Best-effort de-duplication before enforcing unique constraints (safe to re-run).
            migrationBuilder.Sql(
                """
                DELETE FROM UserQuests
                WHERE Id IN (
                    SELECT Id FROM (
                        SELECT Id,
                               ROW_NUMBER() OVER (PARTITION BY UserId, QuestDefinitionId ORDER BY AssignedAt DESC, Id DESC) AS rn
                        FROM UserQuests
                    ) t
                    WHERE t.rn > 1
                );
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM CoinTransactions
                WHERE Id IN (
                    SELECT Id FROM (
                        SELECT Id,
                               ROW_NUMBER() OVER (PARTITION BY UserId, Type, RelatedEntityId ORDER BY CreatedAt ASC, Id ASC) AS rn
                        FROM CoinTransactions
                        WHERE RelatedEntityId IS NOT NULL AND Type IN (1, 2, 3, 4)
                    ) t
                    WHERE t.rn > 1
                );
                """);

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
