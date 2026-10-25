using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityQuestSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActivityType",
                table: "QuestDefinitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "DeduplicationPolicy",
                table: "QuestDefinitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE QuestDefinitions
                SET ActivityType = CASE Type
                    WHEN 2 THEN 2
                    WHEN 8 THEN 4
                    WHEN 10 THEN 5
                    ELSE 1
                END,
                DeduplicationPolicy = CASE Type
                    WHEN 10 THEN 2
                    ELSE 1
                END;
                """);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEndUtc",
                table: "UserQuests",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<string>(
                name: "PeriodKey",
                table: "UserQuests",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStartUtc",
                table: "UserQuests",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.Sql(
                """
                UPDATE UserQuests
                SET PeriodStartUtc = AssignedAt,
                    PeriodEndUtc = ExpiresAt,
                    PeriodKey = 'legacy';
                """);

            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_UserQuests_UserId_QuestDefinitionId;");

            migrationBuilder.CreateTable(
                name: "QuestProgressEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuestDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityType = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SourceEntityType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SourceEntityId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Increment = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestProgressEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestProgressEvents_QuestDefinitions_QuestDefinitionId",
                        column: x => x.QuestDefinitionId,
                        principalTable: "QuestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestProgressEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserQuests_UserId_QuestDefinitionId_PeriodKey",
                table: "UserQuests",
                columns: new[] { "UserId", "QuestDefinitionId", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestProgressEvents_CreatedAtUtc",
                table: "QuestProgressEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_QuestProgressEvents_QuestDefinitionId",
                table: "QuestProgressEvents",
                column: "QuestDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestProgressEvents_UserId",
                table: "QuestProgressEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestProgressEvents_UserId_QuestDefinitionId_PeriodKey_SourceEntityType_SourceEntityId",
                table: "QuestProgressEvents",
                columns: new[] { "UserId", "QuestDefinitionId", "PeriodKey", "SourceEntityType", "SourceEntityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestProgressEvents");

            migrationBuilder.DropIndex(
                name: "IX_UserQuests_UserId_QuestDefinitionId_PeriodKey",
                table: "UserQuests");

            migrationBuilder.DropColumn(
                name: "PeriodEndUtc",
                table: "UserQuests");

            migrationBuilder.DropColumn(
                name: "PeriodKey",
                table: "UserQuests");

            migrationBuilder.DropColumn(
                name: "PeriodStartUtc",
                table: "UserQuests");

            migrationBuilder.DropColumn(
                name: "ActivityType",
                table: "QuestDefinitions");

            migrationBuilder.DropColumn(
                name: "DeduplicationPolicy",
                table: "QuestDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_UserQuests_UserId_QuestDefinitionId",
                table: "UserQuests",
                columns: new[] { "UserId", "QuestDefinitionId" },
                unique: true);
        }
    }
}
