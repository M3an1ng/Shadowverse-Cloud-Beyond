using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ShadowverseBeyond.Server.Infrastructure.Data;
namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations;
[DbContext(typeof(ApplicationDbContext)), Migration("20261029090000_AddCardRevisions")]
public sealed class AddCardRevisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS CardRevisions (Id INTEGER PRIMARY KEY AUTOINCREMENT, CardId INTEGER NOT NULL, Revision INTEGER NOT NULL, Editor TEXT NOT NULL, ModifiedAt TEXT NOT NULL, Note TEXT NOT NULL, Operation TEXT NOT NULL, Snapshot TEXT NOT NULL); CREATE UNIQUE INDEX IF NOT EXISTS IX_CardRevisions_CardId_Revision ON CardRevisions(CardId,Revision);");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("CardRevisions");
}
