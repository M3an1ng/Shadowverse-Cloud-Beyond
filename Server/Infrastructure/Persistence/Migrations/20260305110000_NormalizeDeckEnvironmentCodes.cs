using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Normalizes legacy DeckEnvironment codes after enum rebasing:
    /// Rotation = 0, Unlimited = 1, Open = 2.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260305110000_NormalizeDeckEnvironmentCodes")]
    public partial class NormalizeDeckEnvironmentCodes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            NormalizeEnvironmentColumn(migrationBuilder, "UserDecks");
            NormalizeEnvironmentColumn(migrationBuilder, "GameParticipantStatsFacts");
            NormalizeEnvironmentColumn(migrationBuilder, "CardStatsAggs");
            NormalizeEnvironmentColumn(migrationBuilder, "DeckStatsAggs");
            NormalizeEnvironmentColumn(migrationBuilder, "DeckCardCopyLimitPolicies");

            migrationBuilder.Sql(
                """
                UPDATE "CardSetDefinitions"
                SET "Environments" = '[0]'
                WHERE "Environments" IS NULL OR TRIM("Environments") = '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "CardSetDefinitions"
                SET "Environments" = '[0]'
                WHERE LOWER(TRIM("Environments")) IN ('open', 'default', 'rotation', '0', '-2');
                """);

            migrationBuilder.Sql(
                """
                UPDATE "CardSetDefinitions"
                SET "Environments" = '[1]'
                WHERE LOWER(TRIM("Environments")) IN ('unlimited', '1', '-1');
                """);

            migrationBuilder.Sql(
                """
                UPDATE "CardSetDefinitions"
                SET "Environments" = REPLACE(REPLACE("Environments", '-2', '0'), '-1', '1')
                WHERE "Environments" LIKE '%-2%' OR "Environments" LIKE '%-1%';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: this migration intentionally canonicalizes historical environment data.
        }

        private static void NormalizeEnvironmentColumn(MigrationBuilder migrationBuilder, string tableName)
        {
            migrationBuilder.Sql(
                $"""
                UPDATE "{tableName}"
                SET "Environment" = CASE
                    WHEN "Environment" = -2 THEN 0
                    WHEN "Environment" = -1 THEN 1
                    WHEN "Environment" IN (0, 1, 2) THEN "Environment"
                    ELSE 0
                END
                WHERE "Environment" NOT IN (0, 1, 2);
                """);
        }
    }
}
