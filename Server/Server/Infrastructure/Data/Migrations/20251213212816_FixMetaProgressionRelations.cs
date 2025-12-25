using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixMetaProgressionRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CoinTransactions_UserEconomies_UserEconomyId",
                table: "CoinTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCardPacks_UserEconomies_UserEconomyId",
                table: "UserCardPacks");

            migrationBuilder.DropForeignKey(
                name: "FK_UserDailyRewards_UserEconomies_UserId",
                table: "UserDailyRewards");

            migrationBuilder.DropForeignKey(
                name: "FK_UserQuests_UserEconomies_UserEconomyId",
                table: "UserQuests");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRankings_UserEconomies_UserId",
                table: "UserRankings");

            migrationBuilder.DropIndex(
                name: "IX_UserQuests_UserEconomyId",
                table: "UserQuests");

            migrationBuilder.DropIndex(
                name: "IX_UserCardPacks_UserEconomyId",
                table: "UserCardPacks");

            migrationBuilder.DropIndex(
                name: "IX_CoinTransactions_UserEconomyId",
                table: "CoinTransactions");

            migrationBuilder.DropColumn(
                name: "UserEconomyId",
                table: "UserQuests");

            migrationBuilder.DropColumn(
                name: "UserEconomyId",
                table: "UserCardPacks");

            migrationBuilder.DropColumn(
                name: "UserEconomyId",
                table: "CoinTransactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserEconomyId",
                table: "UserQuests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserEconomyId",
                table: "UserCardPacks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserEconomyId",
                table: "CoinTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserQuests_UserEconomyId",
                table: "UserQuests",
                column: "UserEconomyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCardPacks_UserEconomyId",
                table: "UserCardPacks",
                column: "UserEconomyId");

            migrationBuilder.CreateIndex(
                name: "IX_CoinTransactions_UserEconomyId",
                table: "CoinTransactions",
                column: "UserEconomyId");

            migrationBuilder.AddForeignKey(
                name: "FK_CoinTransactions_UserEconomies_UserEconomyId",
                table: "CoinTransactions",
                column: "UserEconomyId",
                principalTable: "UserEconomies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCardPacks_UserEconomies_UserEconomyId",
                table: "UserCardPacks",
                column: "UserEconomyId",
                principalTable: "UserEconomies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserDailyRewards_UserEconomies_UserId",
                table: "UserDailyRewards",
                column: "UserId",
                principalTable: "UserEconomies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserQuests_UserEconomies_UserEconomyId",
                table: "UserQuests",
                column: "UserEconomyId",
                principalTable: "UserEconomies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserRankings_UserEconomies_UserId",
                table: "UserRankings",
                column: "UserId",
                principalTable: "UserEconomies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
