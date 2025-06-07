using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCardAbilitiesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardAbilities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AbilityTag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AbilityType = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfigFormat = table.Column<int>(type: "INTEGER", nullable: false),
                    DslSource = table.Column<string>(type: "TEXT", nullable: true),
                    JsonConfig = table.Column<string>(type: "TEXT", nullable: true),
                    TriggerExpression = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ConditionExpression = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TargetExpression = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    EffectExpression = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardAbilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardAbilities_CardDefinitions_CardId",
                        column: x => x.CardId,
                        principalTable: "CardDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardAbilities_AbilityTag",
                table: "CardAbilities",
                column: "AbilityTag");

            migrationBuilder.CreateIndex(
                name: "IX_CardAbilities_CardId",
                table: "CardAbilities",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_CardAbilities_CardId_IsActive",
                table: "CardAbilities",
                columns: new[] { "CardId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardAbilities");
        }
    }
}
