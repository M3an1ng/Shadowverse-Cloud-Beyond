using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261025000000_NormalizeLegacyCardDefinitionEnums")]
    public partial class NormalizeLegacyCardDefinitionEnums : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite keeps legacy names in integer-affinity columns but reads them as zero.
            // Freeze this historical mapping. Unknown values fail existing NOT NULL constraints.
            migrationBuilder.Sql(
                """
                UPDATE "CardDefinitions"
                SET
                    "Type" = CASE LOWER(TRIM(CAST("Type" AS TEXT)))
                        WHEN '0' THEN 'Leader'
                        WHEN 'leader' THEN 'Leader'
                        WHEN '1' THEN 'Follower'
                        WHEN 'follower' THEN 'Follower'
                        WHEN '2' THEN 'Spell'
                        WHEN 'spell' THEN 'Spell'
                        WHEN '3' THEN 'Amulet'
                        WHEN 'amulet' THEN 'Amulet'
                        WHEN '4' THEN 'Crest'
                        WHEN 'crest' THEN 'Crest'
                        WHEN '5' THEN 'Faith'
                        WHEN 'faith' THEN 'Faith'
                        ELSE NULL
                    END,
                    "Class" = CASE LOWER(TRIM(CAST("Class" AS TEXT)))
                        WHEN '0' THEN 0
                        WHEN 'neutral' THEN 0
                        WHEN '1' THEN 1
                        WHEN 'forestcraft' THEN 1
                        WHEN '2' THEN 2
                        WHEN 'swordcraft' THEN 2
                        WHEN '3' THEN 3
                        WHEN 'runecraft' THEN 3
                        WHEN '4' THEN 4
                        WHEN 'dragoncraft' THEN 4
                        WHEN '5' THEN 5
                        WHEN 'abysscraft' THEN 5
                        WHEN '6' THEN 6
                        WHEN 'havencraft' THEN 6
                        WHEN '7' THEN 7
                        WHEN 'portalcraft' THEN 7
                        ELSE NULL
                    END,
                    "Rarity" = CASE LOWER(TRIM(CAST("Rarity" AS TEXT)))
                        WHEN '0' THEN 0
                        WHEN 'special' THEN 0
                        WHEN '1' THEN 1
                        WHEN 'bronze' THEN 1
                        WHEN '2' THEN 2
                        WHEN 'silver' THEN 2
                        WHEN '3' THEN 3
                        WHEN 'gold' THEN 3
                        WHEN '4' THEN 4
                        WHEN 'legend' THEN 4
                        ELSE NULL
                    END
                WHERE "Type" NOT IN ('Leader', 'Follower', 'Spell', 'Amulet', 'Crest', 'Faith')
                   OR typeof("Class") <> 'integer' OR "Class" NOT IN (0, 1, 2, 3, 4, 5, 6, 7)
                   OR typeof("Rarity") <> 'integer' OR "Rarity" NOT IN (0, 1, 2, 3, 4);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Numeric IDs and names have the same domain meaning; the old spelling is not recoverable.
        }
    }
}
