using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261117003000_AddTournamentScheduling")]
public sealed class AddTournamentScheduling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>("NextScheduleCheckAtUtc", "Tournaments", "TEXT", nullable: true);
        migrationBuilder.AddColumn<int>("DeckSubmissionMinutes", "TournamentStages", "INTEGER", nullable: true);
        migrationBuilder.AddColumn<DateTime>("DeckSubmissionClosesAtUtc", "TournamentStages", "TEXT", nullable: true);
        migrationBuilder.AddColumn<DateTime>("StartedAtUtc", "TournamentStages", "TEXT", nullable: true);
        migrationBuilder.AddColumn<DateTime>("CompletedAtUtc", "TournamentStages", "TEXT", nullable: true);
        migrationBuilder.AddColumn<Guid>("WinnerEntryId", "TournamentStages", "TEXT", nullable: true);
        migrationBuilder.AddColumn<int>("Bracket", "TournamentMatches", "INTEGER", nullable: false, defaultValue: 0);
        migrationBuilder.DropIndex("IX_TournamentMatches_TournamentId_StageNumber_RoundNumber_PairingNumber", "TournamentMatches");
        migrationBuilder.CreateIndex("IX_TournamentMatches_TournamentId_StageNumber_Bracket_RoundNumber_PairingNumber",
            "TournamentMatches", new[] { "TournamentId", "StageNumber", "Bracket", "RoundNumber", "PairingNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_Tournaments_Status_NextScheduleCheckAtUtc", "Tournaments",
            new[] { "Status", "NextScheduleCheckAtUtc" });
        migrationBuilder.CreateTable(
            name: "TournamentStageEntries",
            columns: table => new
            {
                TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                StageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                Seed = table.Column<int>(type: "INTEGER", nullable: false),
                CanPlay = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TournamentStageEntries", entry => new { entry.TournamentId, entry.StageNumber, entry.EntryId });
                table.ForeignKey("FK_TournamentStageEntries_TournamentStages_TournamentId_StageNumber",
                    entry => new { entry.TournamentId, entry.StageNumber }, "TournamentStages",
                    new[] { "TournamentId", "Number" }, onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_TournamentStageEntries_TournamentEntries_EntryId",
                    entry => entry.EntryId, "TournamentEntries", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_TournamentStageEntries_EntryId", "TournamentStageEntries", "EntryId");
        migrationBuilder.CreateIndex("IX_TournamentStageEntries_TournamentId_StageNumber_Seed", "TournamentStageEntries",
            new[] { "TournamentId", "StageNumber", "Seed" }, unique: true);
        migrationBuilder.Sql("""
            UPDATE Tournaments SET NextScheduleCheckAtUtc = UpdatedAtUtc WHERE Status = 2;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("TournamentStageEntries");
        migrationBuilder.DropIndex("IX_TournamentMatches_TournamentId_StageNumber_Bracket_RoundNumber_PairingNumber", "TournamentMatches");
        migrationBuilder.DropIndex("IX_Tournaments_Status_NextScheduleCheckAtUtc", "Tournaments");
        migrationBuilder.Sql("""
            ALTER TABLE TournamentMatches DROP COLUMN Bracket;
            ALTER TABLE TournamentStages DROP COLUMN WinnerEntryId;
            ALTER TABLE TournamentStages DROP COLUMN CompletedAtUtc;
            ALTER TABLE TournamentStages DROP COLUMN StartedAtUtc;
            ALTER TABLE TournamentStages DROP COLUMN DeckSubmissionClosesAtUtc;
            ALTER TABLE TournamentStages DROP COLUMN DeckSubmissionMinutes;
            ALTER TABLE Tournaments DROP COLUMN NextScheduleCheckAtUtc;
            """);
        migrationBuilder.CreateIndex("IX_TournamentMatches_TournamentId_StageNumber_RoundNumber_PairingNumber",
            "TournamentMatches", new[] { "TournamentId", "StageNumber", "RoundNumber", "PairingNumber" }, unique: true);
    }
}
