using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAbilityIdToCardDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AbilityId",
                table: "CardDefinitions",
                type: "INTEGER", // SQLite uses INTEGER for long?
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbilityId",
                table: "CardDefinitions");
        }
    }
}