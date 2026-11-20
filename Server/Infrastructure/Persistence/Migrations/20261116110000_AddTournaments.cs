using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261116110000_AddTournaments")]
public sealed class AddTournaments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Tournaments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                CardVersion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                Format = table.Column<int>(type: "INTEGER", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                PlayerLimit = table.Column<int>(type: "INTEGER", nullable: true),
                RegistrationOpensAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                RegistrationClosesAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Revision = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tournaments", tournament => tournament.Id);
                table.ForeignKey("FK_Tournaments_Users_CreatedByUserId", tournament => tournament.CreatedByUserId,
                    "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_Tournaments_CreatedByUserId", "Tournaments", "CreatedByUserId");

        migrationBuilder.CreateTable(
            name: "TournamentStages",
            columns: table => new
            {
                TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                Number = table.Column<int>(type: "INTEGER", nullable: false),
                Kind = table.Column<int>(type: "INTEGER", nullable: false),
                Rounds = table.Column<int>(type: "INTEGER", nullable: true),
                AdvancingPlayers = table.Column<int>(type: "INTEGER", nullable: true),
                MatchFormat = table.Column<int>(type: "INTEGER", nullable: false),
                AllowDeckResubmission = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TournamentStages", stage => new { stage.TournamentId, stage.Number });
                table.ForeignKey("FK_TournamentStages_Tournaments_TournamentId", stage => stage.TournamentId,
                    "Tournaments", "Id", onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("TournamentStages");
        migrationBuilder.DropTable("Tournaments");
    }
}
