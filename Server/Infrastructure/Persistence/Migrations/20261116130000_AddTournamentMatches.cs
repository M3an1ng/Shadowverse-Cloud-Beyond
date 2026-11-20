using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261116130000_AddTournamentMatches")]
public sealed class AddTournamentMatches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TournamentMatches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                StageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                PairingNumber = table.Column<int>(type: "INTEGER", nullable: false),
                EntryOneId = table.Column<Guid>(type: "TEXT", nullable: false),
                EntryTwoId = table.Column<Guid>(type: "TEXT", nullable: true),
                BanByOne = table.Column<Guid>(type: "TEXT", nullable: true),
                BanByTwo = table.Column<Guid>(type: "TEXT", nullable: true),
                PickByOne = table.Column<Guid>(type: "TEXT", nullable: true),
                PickByTwo = table.Column<Guid>(type: "TEXT", nullable: true),
                WinnerEntryId = table.Column<Guid>(type: "TEXT", nullable: true),
                CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TournamentMatches", match => match.Id);
                table.ForeignKey("FK_TournamentMatches_TournamentStages_TournamentId_StageNumber",
                    match => new { match.TournamentId, match.StageNumber }, "TournamentStages",
                    new[] { "TournamentId", "Number" }, onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_TournamentMatches_TournamentEntries_EntryOneId", match => match.EntryOneId,
                    "TournamentEntries", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_TournamentMatches_TournamentEntries_EntryTwoId", match => match.EntryTwoId,
                    "TournamentEntries", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_TournamentMatches_TournamentId_StageNumber_RoundNumber_PairingNumber",
            "TournamentMatches", new[] { "TournamentId", "StageNumber", "RoundNumber", "PairingNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_TournamentMatches_EntryOneId", "TournamentMatches", "EntryOneId");
        migrationBuilder.CreateIndex("IX_TournamentMatches_EntryTwoId", "TournamentMatches", "EntryTwoId");

        migrationBuilder.CreateTable(
            name: "TournamentMatchGames",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                Number = table.Column<int>(type: "INTEGER", nullable: false),
                EntryOneDeckId = table.Column<Guid>(type: "TEXT", nullable: false),
                EntryTwoDeckId = table.Column<Guid>(type: "TEXT", nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                WinnerEntryId = table.Column<Guid>(type: "TEXT", nullable: true),
                FirstPlayerEntryId = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TournamentMatchGames", game => game.Id);
                table.ForeignKey("FK_TournamentMatchGames_TournamentMatches_MatchId", game => game.MatchId,
                    "TournamentMatches", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_TournamentMatchGames_TournamentEntryDecks_EntryOneDeckId", game => game.EntryOneDeckId,
                    "TournamentEntryDecks", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_TournamentMatchGames_TournamentEntryDecks_EntryTwoDeckId", game => game.EntryTwoDeckId,
                    "TournamentEntryDecks", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_TournamentMatchGames_MatchId_Number", "TournamentMatchGames",
            new[] { "MatchId", "Number" }, unique: true);
        migrationBuilder.CreateIndex("IX_TournamentMatchGames_EntryOneDeckId", "TournamentMatchGames", "EntryOneDeckId");
        migrationBuilder.CreateIndex("IX_TournamentMatchGames_EntryTwoDeckId", "TournamentMatchGames", "EntryTwoDeckId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("TournamentMatchGames");
        migrationBuilder.DropTable("TournamentMatches");
    }
}
