using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    public partial class AddTribeDefinitions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TribeDefinitions",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Aliases = table.Column<string>(type: "TEXT", nullable: false),
                    Behavior = table.Column<int>(type: "INTEGER", nullable: false),
                    IsReserved = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TribeDefinitions", x => x.Name);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TribeDefinitions_IsEnabled",
                table: "TribeDefinitions",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_TribeDefinitions_IsReserved_IsEnabled",
                table: "TribeDefinitions",
                columns: new[] { "IsReserved", "IsEnabled" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TribeDefinitions");
        }
    }
}
