using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261116120000_AddTournamentRegistration")]
public sealed class AddTournamentRegistration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("DeckEnvironment", "Tournaments", "INTEGER", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("DeckRule", "Tournaments", "INTEGER", nullable: false, defaultValue: 0);
        migrationBuilder.CreateIndex("IX_Tournaments_Status", "Tournaments", "Status");
        migrationBuilder.CreateTable(
            name: "TournamentEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                PlayerName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                RegisteredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Revision = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TournamentEntries", entry => entry.Id);
                table.ForeignKey("FK_TournamentEntries_Tournaments_TournamentId", entry => entry.TournamentId,
                    "Tournaments", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_TournamentEntries_Users_UserId", entry => entry.UserId,
                    "Users", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_TournamentEntries_TournamentId_UserId", "TournamentEntries",
            new[] { "TournamentId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_TournamentEntries_UserId", "TournamentEntries", "UserId");

        migrationBuilder.CreateTable(
            name: "TournamentEntryDecks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                StageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Slot = table.Column<int>(type: "INTEGER", nullable: false),
                SourceDeckId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Class = table.Column<int>(type: "INTEGER", nullable: false),
                SecondaryClass = table.Column<int>(type: "INTEGER", nullable: true),
                DeckCode = table.Column<string>(type: "TEXT", nullable: false),
                CardsJson = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TournamentEntryDecks", deck => deck.Id);
                table.ForeignKey("FK_TournamentEntryDecks_TournamentEntries_EntryId", deck => deck.EntryId,
                    "TournamentEntries", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_TournamentEntryDecks_EntryId_StageNumber_Slot", "TournamentEntryDecks",
            new[] { "EntryId", "StageNumber", "Slot" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("TournamentEntryDecks");
        migrationBuilder.DropTable("TournamentEntries");
        migrationBuilder.DropIndex("IX_Tournaments_Status", "Tournaments");
        migrationBuilder.DropColumn("DeckRule", "Tournaments");
        migrationBuilder.DropColumn("DeckEnvironment", "Tournaments");
    }
}
