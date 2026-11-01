using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ShadowverseBeyond.Server.Infrastructure.Data;
namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations;
[DbContext(typeof(ApplicationDbContext)), Migration("20261029093000_AddFeatureDocuments")]
public sealed class AddFeatureDocuments : Migration
{
    protected override void Up(MigrationBuilder b) => b.Sql("CREATE TABLE IF NOT EXISTS FeatureDocuments (Id TEXT PRIMARY KEY NOT NULL, Json TEXT NOT NULL, Revision INTEGER NOT NULL, UpdatedAt TEXT NOT NULL);");
    protected override void Down(MigrationBuilder b) => b.DropTable("FeatureDocuments");
}
