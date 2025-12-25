using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReplayJournalCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReplayBundles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BundleSeq = table.Column<long>(type: "INTEGER", nullable: false),
                    BundleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartSeq = table.Column<long>(type: "INTEGER", nullable: false),
                    EndSeq = table.Column<long>(type: "INTEGER", nullable: false),
                    EventCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CommittedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayBundles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReplayEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Seq = table.Column<long>(type: "INTEGER", nullable: false),
                    BundleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    DtosJson = table.Column<string>(type: "TEXT", nullable: true),
                    Announcement = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetIdsJson = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReplayGames",
                columns: table => new
                {
                    GameId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HostPlayerId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    GuestPlayerId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    WinnerPlayerId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ServerBuild = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayGames", x => x.GameId);
                });

            migrationBuilder.CreateTable(
                name: "ReplaySnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AtSeq = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    Compression = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Payload = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplaySnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReplayBundles_GameId",
                table: "ReplayBundles",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayBundles_GameId_BundleId",
                table: "ReplayBundles",
                columns: new[] { "GameId", "BundleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReplayBundles_GameId_BundleSeq",
                table: "ReplayBundles",
                columns: new[] { "GameId", "BundleSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReplayEvents_BundleId",
                table: "ReplayEvents",
                column: "BundleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayEvents_EventId",
                table: "ReplayEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReplayEvents_GameId_Seq",
                table: "ReplayEvents",
                columns: new[] { "GameId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReplayGames_CreatedAtUtc",
                table: "ReplayGames",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayGames_EndedAtUtc",
                table: "ReplayGames",
                column: "EndedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReplaySnapshots_GameId",
                table: "ReplaySnapshots",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplaySnapshots_GameId_AtSeq",
                table: "ReplaySnapshots",
                columns: new[] { "GameId", "AtSeq" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReplayBundles");

            migrationBuilder.DropTable(
                name: "ReplayEvents");

            migrationBuilder.DropTable(
                name: "ReplayGames");

            migrationBuilder.DropTable(
                name: "ReplaySnapshots");
        }
    }
}
