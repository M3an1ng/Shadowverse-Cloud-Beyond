using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardPackMetadataAndLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Abbreviation",
                table: "CardPackDefinitions",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Environment",
                table: "CardPackDefinitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Region",
                table: "CardPackDefinitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "CardPackDefinitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CardPackDefinitionId",
                table: "CardDefinitions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardDefinitions_CardPackDefinitionId",
                table: "CardDefinitions",
                column: "CardPackDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CardPackDefinitions_Region_Sequence",
                table: "CardPackDefinitions",
                columns: new[] { "Region", "Sequence" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CardPackDefinitions_Region",
                table: "CardPackDefinitions",
                sql: "Region >= 0 AND Region <= 9");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CardPackDefinitions_Sequence",
                table: "CardPackDefinitions",
                sql: "Sequence >= 0 AND Sequence <= 99");

            migrationBuilder.AddForeignKey(
                name: "FK_CardDefinitions_CardPackDefinitions_CardPackDefinitionId",
                table: "CardDefinitions",
                column: "CardPackDefinitionId",
                principalTable: "CardPackDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardDefinitions_CardPackDefinitions_CardPackDefinitionId",
                table: "CardDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CardPackDefinitions_Region",
                table: "CardPackDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CardPackDefinitions_Sequence",
                table: "CardPackDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CardDefinitions_CardPackDefinitionId",
                table: "CardDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CardPackDefinitions_Region_Sequence",
                table: "CardPackDefinitions");

            migrationBuilder.DropColumn(
                name: "CardPackDefinitionId",
                table: "CardDefinitions");

            migrationBuilder.DropColumn(
                name: "Abbreviation",
                table: "CardPackDefinitions");

            migrationBuilder.DropColumn(
                name: "Environment",
                table: "CardPackDefinitions");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "CardPackDefinitions");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "CardPackDefinitions");
        }
    }
}
