using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260216160000_AddFirstSecondWinrateSplit")]
    public partial class AddFirstSecondWinrateSplit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GoingFirstGames",
                table: "DeckStatsAggs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GoingFirstWins",
                table: "DeckStatsAggs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GoingSecondGames",
                table: "DeckStatsAggs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GoingSecondWins",
                table: "DeckStatsAggs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFirstPlayer",
                table: "GameParticipantStatsFacts",
                type: "INTEGER",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoingFirstGames",
                table: "DeckStatsAggs");

            migrationBuilder.DropColumn(
                name: "GoingFirstWins",
                table: "DeckStatsAggs");

            migrationBuilder.DropColumn(
                name: "GoingSecondGames",
                table: "DeckStatsAggs");

            migrationBuilder.DropColumn(
                name: "GoingSecondWins",
                table: "DeckStatsAggs");

            migrationBuilder.DropColumn(
                name: "IsFirstPlayer",
                table: "GameParticipantStatsFacts");
        }
    }
}

