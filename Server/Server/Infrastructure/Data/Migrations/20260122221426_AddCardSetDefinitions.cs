using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardSetDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardSetDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Region = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Environments = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardSetDefinitions", x => x.Id);
                    table.CheckConstraint("CK_CardSetDefinitions_Region", "Region >= 0 AND Region <= 9");
                    table.CheckConstraint("CK_CardSetDefinitions_Sequence", "Sequence >= 0 AND Sequence <= 99");
                });

            migrationBuilder.AddColumn<int>(
                name: "CardSetDefinitionId",
                table: "CardPackDefinitions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CardSetDefinitionId",
                table: "CardDefinitions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(@"
INSERT INTO CardSetDefinitions (Name, Abbreviation, Region, Sequence, Environments, CreatedAt)
SELECT
    Name,
    CASE
        WHEN Abbreviation IS NULL THEN ''
        ELSE Abbreviation
    END,
    Region,
    Sequence,
    CASE
        WHEN Environments IS NULL OR Environments = '' THEN '[0]'
        ELSE Environments
    END,
    CURRENT_TIMESTAMP
FROM CardPackDefinitions
WHERE Name IS NOT NULL AND Name <> ''
GROUP BY Name;
");

            migrationBuilder.Sql(@"
WITH existing AS (
    SELECT COALESCE(MAX(Sequence), 1) AS MaxSequence
    FROM CardSetDefinitions
    WHERE Region = 0
),
missing AS (
    SELECT DISTINCT CardSet AS Name
    FROM CardDefinitions
    WHERE CardSet IS NOT NULL
      AND CardSet <> ''
      AND LOWER(CardSet) NOT IN (SELECT LOWER(Name) FROM CardSetDefinitions)
),
numbered AS (
    SELECT Name, ROW_NUMBER() OVER (ORDER BY Name) AS RowNumber
    FROM missing
)
INSERT INTO CardSetDefinitions (Name, Abbreviation, Region, Sequence, Environments, CreatedAt)
SELECT
    Name,
    '',
    0,
    existing.MaxSequence + numbered.RowNumber,
    '[0]',
    CURRENT_TIMESTAMP
FROM numbered, existing;
");

            migrationBuilder.Sql(@"
INSERT INTO CardSetDefinitions (Name, Abbreviation, Region, Sequence, Environments, CreatedAt)
SELECT 'Legacy', 'LEG', 9, 99, '[0]', CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM CardSetDefinitions WHERE LOWER(Name) = 'legacy');
");

            migrationBuilder.Sql(@"
UPDATE CardPackDefinitions
SET CardSetDefinitionId = (
    SELECT Id
    FROM CardSetDefinitions
    WHERE CardSetDefinitions.Name = CardPackDefinitions.Name
)
WHERE CardSetDefinitionId IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE CardDefinitions
SET CardSetDefinitionId = (
    SELECT Id
    FROM CardSetDefinitions
    WHERE CardSetDefinitions.Name = CardDefinitions.CardSet
)
WHERE CardSetDefinitionId IS NULL OR CardSetDefinitionId = 0;
");

            migrationBuilder.DropColumn(
                name: "Environments",
                table: "CardPackDefinitions");

            migrationBuilder.AlterColumn<int>(
                name: "CardSetDefinitionId",
                table: "CardPackDefinitions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardPackDefinitions_CardSetDefinitionId",
                table: "CardPackDefinitions",
                column: "CardSetDefinitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardDefinitions_CardSetDefinitionId",
                table: "CardDefinitions",
                column: "CardSetDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CardSetDefinitions_Region_Sequence",
                table: "CardSetDefinitions",
                columns: new[] { "Region", "Sequence" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CardDefinitions_CardSetDefinitions_CardSetDefinitionId",
                table: "CardDefinitions",
                column: "CardSetDefinitionId",
                principalTable: "CardSetDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CardPackDefinitions_CardSetDefinitions_CardSetDefinitionId",
                table: "CardPackDefinitions",
                column: "CardSetDefinitionId",
                principalTable: "CardSetDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardDefinitions_CardSetDefinitions_CardSetDefinitionId",
                table: "CardDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_CardPackDefinitions_CardSetDefinitions_CardSetDefinitionId",
                table: "CardPackDefinitions");

            migrationBuilder.DropTable(
                name: "CardSetDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CardPackDefinitions_CardSetDefinitionId",
                table: "CardPackDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CardDefinitions_CardSetDefinitionId",
                table: "CardDefinitions");

            migrationBuilder.DropColumn(
                name: "CardSetDefinitionId",
                table: "CardPackDefinitions");

            migrationBuilder.DropColumn(
                name: "CardSetDefinitionId",
                table: "CardDefinitions");

            migrationBuilder.AddColumn<string>(
                name: "Environments",
                table: "CardPackDefinitions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
