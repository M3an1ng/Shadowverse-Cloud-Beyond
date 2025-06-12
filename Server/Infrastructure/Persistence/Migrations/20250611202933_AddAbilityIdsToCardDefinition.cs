using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAbilityIdsToCardDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbilityId",
                table: "CardDefinitions");

            migrationBuilder.DropColumn(
                name: "IsEvolved",
                table: "CardDefinitions");

            migrationBuilder.RenameColumn(
                name: "EffectsJson",
                table: "CardDefinitions",
                newName: "Tribe");

            migrationBuilder.AddColumn<string>(
                name: "AbilityIds",
                table: "CardDefinitions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CardSet",
                table: "CardDefinitions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Class",
                table: "CardDefinitions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rarity",
                table: "CardDefinitions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbilityIds",
                table: "CardDefinitions");

            migrationBuilder.DropColumn(
                name: "CardSet",
                table: "CardDefinitions");

            migrationBuilder.DropColumn(
                name: "Class",
                table: "CardDefinitions");

            migrationBuilder.DropColumn(
                name: "Rarity",
                table: "CardDefinitions");

            migrationBuilder.RenameColumn(
                name: "Tribe",
                table: "CardDefinitions",
                newName: "EffectsJson");

            migrationBuilder.AddColumn<long>(
                name: "AbilityId",
                table: "CardDefinitions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEvolved",
                table: "CardDefinitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
