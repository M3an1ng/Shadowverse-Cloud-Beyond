using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261117061500_AddTournamentGrandFinalReset")]
public sealed class AddTournamentGrandFinalReset : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("AllowGrandFinalReset", "TournamentStages", "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.Sql($"""
            UPDATE TournamentStages
            SET AllowGrandFinalReset = 1
            WHERE Kind = {(int)TournamentStageKind.DoubleElimination}
            AND TournamentId IN (SELECT Id FROM Tournaments WHERE Status <> {(int)TournamentStatus.Draft})
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER TABLE TournamentStages DROP COLUMN AllowGrandFinalReset");
}
