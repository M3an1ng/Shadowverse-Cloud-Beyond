using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBuffDefinitionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Rarity",
                table: "CardDefinitions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Class",
                table: "CardDefinitions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.CreateTable(
                name: "BuffDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    BuffType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    JsonConfig = table.Column<string>(type: "TEXT", nullable: false),
                    DslExpression = table.Column<string>(type: "TEXT", nullable: true),
                    IsNonStackable = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultDurationType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DefaultDurationParameter = table.Column<int>(type: "INTEGER", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Rarity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuffDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuffDefinitions_BuffType",
                table: "BuffDefinitions",
                column: "BuffType");

            migrationBuilder.CreateIndex(
                name: "IX_BuffDefinitions_IsActive",
                table: "BuffDefinitions",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuffDefinitions");

            migrationBuilder.AlterColumn<string>(
                name: "Rarity",
                table: "CardDefinitions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "Class",
                table: "CardDefinitions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }
    }
}
