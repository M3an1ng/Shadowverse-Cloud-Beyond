using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAbilityDslFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AbilityDslFavorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Expression = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbilityDslFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbilityDslFavorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbilityDslFavorites_ExpectedType",
                table: "AbilityDslFavorites",
                column: "ExpectedType");

            migrationBuilder.CreateIndex(
                name: "IX_AbilityDslFavorites_UsageCount",
                table: "AbilityDslFavorites",
                column: "UsageCount");

            migrationBuilder.CreateIndex(
                name: "IX_AbilityDslFavorites_UserId",
                table: "AbilityDslFavorites",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbilityDslFavorites");
        }
    }
}
