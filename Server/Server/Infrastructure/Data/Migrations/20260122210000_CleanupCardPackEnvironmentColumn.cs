using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CleanupCardPackEnvironmentColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Environments",
                table: "CardPackDefinitions",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.Sql(
                "UPDATE CardPackDefinitions SET Environments = '[' || Environment || ']' " +
                "WHERE Environments IS NULL OR Environments = '' OR Environments = '[]';");

            migrationBuilder.DropColumn(
                name: "Environment",
                table: "CardPackDefinitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Environment",
                table: "CardPackDefinitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                @"UPDATE CardPackDefinitions
SET Environment = CAST(
    CASE
        WHEN Environments IS NULL OR Environments = '' OR Environments = '[]' THEN '0'
        WHEN Environments LIKE '[%' THEN
            CASE
                WHEN instr(Environments, ',') > 0 THEN substr(Environments, 2, instr(Environments, ',') - 2)
                ELSE substr(Environments, 2, length(Environments) - 2)
            END
        ELSE Environments
    END AS INTEGER);");
        }
    }
}
