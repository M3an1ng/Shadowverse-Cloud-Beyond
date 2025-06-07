using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorUserEconomySystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyLoginStreak",
                table: "UserEconomies");

            migrationBuilder.DropColumn(
                name: "LastDailyReward",
                table: "UserEconomies");

            migrationBuilder.DropColumn(
                name: "Rank",
                table: "UserEconomies");

            migrationBuilder.DropColumn(
                name: "RankPoints",
                table: "UserEconomies");

            migrationBuilder.CreateTable(
                name: "UserDailyRewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastClaimedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConsecutiveDays = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalLoginDays = table.Column<int>(type: "INTEGER", nullable: false),
                    LongestStreak = table.Column<int>(type: "INTEGER", nullable: false),
                    MonthlyClaimedDays = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyRewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDailyRewards_UserEconomies_UserId",
                        column: x => x.UserId,
                        principalTable: "UserEconomies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserDailyRewards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRankings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    RankPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonStartAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeasonHighestRank = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalWins = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalLosses = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRankings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRankings_UserEconomies_UserId",
                        column: x => x.UserId,
                        principalTable: "UserEconomies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRankings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyRewards_ConsecutiveDays",
                table: "UserDailyRewards",
                column: "ConsecutiveDays");

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyRewards_LastClaimedAt",
                table: "UserDailyRewards",
                column: "LastClaimedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyRewards_UserId",
                table: "UserDailyRewards",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRankings_Rank",
                table: "UserRankings",
                column: "Rank");

            migrationBuilder.CreateIndex(
                name: "IX_UserRankings_RankPoints",
                table: "UserRankings",
                column: "RankPoints");

            migrationBuilder.CreateIndex(
                name: "IX_UserRankings_UserId",
                table: "UserRankings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDailyRewards");

            migrationBuilder.DropTable(
                name: "UserRankings");

            migrationBuilder.AddColumn<int>(
                name: "DailyLoginStreak",
                table: "UserEconomies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDailyReward",
                table: "UserEconomies",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Rank",
                table: "UserEconomies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RankPoints",
                table: "UserEconomies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
