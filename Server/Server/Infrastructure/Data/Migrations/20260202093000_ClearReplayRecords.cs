using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShadowverseBeyond.Server.Infrastructure.Data;

#nullable disable

namespace ShadowverseBeyond.Server.Server.Infrastructure.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260202093000_ClearReplayRecords")]
    public partial class ClearReplayRecords : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ReplayEvents;");
            migrationBuilder.Sql("DELETE FROM ReplayBundles;");
            migrationBuilder.Sql("DELETE FROM ReplaySnapshots;");
            migrationBuilder.Sql("DELETE FROM ReplayGames;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
