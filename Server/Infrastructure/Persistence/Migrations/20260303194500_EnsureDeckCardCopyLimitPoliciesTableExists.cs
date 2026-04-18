using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Safety migration for databases that are missing DeckCardCopyLimitPolicies table.
    /// Uses IF NOT EXISTS to avoid conflicts on already-migrated databases.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260303194500_EnsureDeckCardCopyLimitPoliciesTableExists")]
    public partial class EnsureDeckCardCopyLimitPoliciesTableExists : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "DeckCardCopyLimitPolicies" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_DeckCardCopyLimitPolicies" PRIMARY KEY,
                    "Environment" INTEGER NOT NULL DEFAULT 0,
                    "Rule" INTEGER NOT NULL DEFAULT 0,
                    "CardDefinitionId" INTEGER NOT NULL,
                    "MaxCopies" INTEGER NOT NULL DEFAULT 3,
                    "UpdatedAtUtc" TEXT NOT NULL,
                    CONSTRAINT "FK_DeckCardCopyLimitPolicies_CardDefinitions_CardDefinitionId"
                        FOREIGN KEY ("CardDefinitionId")
                        REFERENCES "CardDefinitions" ("Id")
                        ON DELETE RESTRICT
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_DeckCardCopyLimitPolicies_Environment_Rule_CardDefinitionId"
                ON "DeckCardCopyLimitPolicies" ("Environment", "Rule", "CardDefinitionId");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_DeckCardCopyLimitPolicies_UpdatedAtUtc"
                ON "DeckCardCopyLimitPolicies" ("UpdatedAtUtc");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_DeckCardCopyLimitPolicies_CardDefinitionId"
                ON "DeckCardCopyLimitPolicies" ("CardDefinitionId");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op:
            // This migration is a defensive "ensure exists" patch for drifted databases.
            // Dropping the table here may destroy data on databases where it is legitimately present.
        }
    }
}
