using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260215131500_AddUserClassStatsAgg")]
    public partial class AddUserClassStatsAgg : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserClassStatsAggs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeckClass = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalGames = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalWins = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClassStatsAggs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserClassStatsAggs_UpdatedAtUtc",
                table: "UserClassStatsAggs",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserClassStatsAggs_UserId",
                table: "UserClassStatsAggs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserClassStatsAggs_UserId_DeckClass",
                table: "UserClassStatsAggs",
                columns: new[] { "UserId", "DeckClass" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserClassStatsAggs");
        }
    }
}
