using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCardDefinitionPackLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardDefinitions_CardPackDefinitions_CardPackDefinitionId",
                table: "CardDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CardDefinitions_CardPackDefinitionId",
                table: "CardDefinitions");

            migrationBuilder.DropColumn(
                name: "CardPackDefinitionId",
                table: "CardDefinitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CardPackDefinitionId",
                table: "CardDefinitions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardDefinitions_CardPackDefinitionId",
                table: "CardDefinitions",
                column: "CardPackDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_CardDefinitions_CardPackDefinitions_CardPackDefinitionId",
                table: "CardDefinitions",
                column: "CardPackDefinitionId",
                principalTable: "CardPackDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
