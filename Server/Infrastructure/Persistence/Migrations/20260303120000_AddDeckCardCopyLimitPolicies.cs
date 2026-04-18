using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    public partial class AddDeckCardCopyLimitPolicies : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeckCardCopyLimitPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Environment = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Rule = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CardDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxCopies = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 3),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckCardCopyLimitPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeckCardCopyLimitPolicies_CardDefinitions_CardDefinitionId",
                        column: x => x.CardDefinitionId,
                        principalTable: "CardDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeckCardCopyLimitPolicies_Environment_Rule_CardDefinitionId",
                table: "DeckCardCopyLimitPolicies",
                columns: new[] { "Environment", "Rule", "CardDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeckCardCopyLimitPolicies_UpdatedAtUtc",
                table: "DeckCardCopyLimitPolicies",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DeckCardCopyLimitPolicies_CardDefinitionId",
                table: "DeckCardCopyLimitPolicies",
                column: "CardDefinitionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeckCardCopyLimitPolicies");
        }
    }
}
