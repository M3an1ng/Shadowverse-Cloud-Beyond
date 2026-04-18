using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260304093000_AddCardCommunity")]
    public partial class AddCardCommunity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CardDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardComments_CardDefinitions_CardDefinitionId",
                        column: x => x.CardDefinitionId,
                        principalTable: "CardDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardComments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CardVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CardDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    VoteType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardVotes", x => x.Id);
                    table.CheckConstraint("CK_CardVotes_VoteType", "VoteType IN (1, -1)");
                    table.ForeignKey(
                        name: "FK_CardVotes_CardDefinitions_CardDefinitionId",
                        column: x => x.CardDefinitionId,
                        principalTable: "CardDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardVotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardComments_CardDefinitionId_CreatedAt",
                table: "CardComments",
                columns: new[] { "CardDefinitionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CardComments_UserId",
                table: "CardComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CardVotes_CardDefinitionId_UserId",
                table: "CardVotes",
                columns: new[] { "CardDefinitionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardVotes_CardDefinitionId_VoteType",
                table: "CardVotes",
                columns: new[] { "CardDefinitionId", "VoteType" });

            migrationBuilder.CreateIndex(
                name: "IX_CardVotes_UserId",
                table: "CardVotes",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardComments");

            migrationBuilder.DropTable(
                name: "CardVotes");
        }
    }
}

