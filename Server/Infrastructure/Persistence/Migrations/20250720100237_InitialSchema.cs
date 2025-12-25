using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShadowverseBeyond.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "CardDefinitions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.CreateTable(
                name: "CardAbilities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AbilityTag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AbilityType = table.Column<int>(type: "INTEGER", nullable: false),
                    JsonConfig = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardAbilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardAbilities_CardDefinitions_CardId",
                        column: x => x.CardId,
                        principalTable: "CardDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CardPackDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Price = table.Column<int>(type: "INTEGER", nullable: false),
                    CardCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    GuaranteedRareCount = table.Column<int>(type: "INTEGER", nullable: false),
                    GuaranteedEpicCount = table.Column<int>(type: "INTEGER", nullable: false),
                    GuaranteedLegendaryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardPackDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuestDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetValue = table.Column<int>(type: "INTEGER", nullable: false),
                    CoinReward = table.Column<int>(type: "INTEGER", nullable: false),
                    ExperienceReward = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RefreshIntervalHours = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    AvatarUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Bio = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsEmailVerified = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameStateId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Result = table.Column<int>(type: "INTEGER", nullable: true),
                    Duration = table.Column<long>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserEconomies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Coins = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEconomies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserEconomies_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TotalGames = table.Column<int>(type: "INTEGER", nullable: false),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false),
                    Losses = table.Column<int>(type: "INTEGER", nullable: false),
                    WinRate = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    TotalPlayTime = table.Column<long>(type: "INTEGER", nullable: false),
                    FavoriteCardId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoinTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RelatedEntityId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserEconomyId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoinTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoinTransactions_UserEconomies_UserEconomyId",
                        column: x => x.UserEconomyId,
                        principalTable: "UserEconomies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CoinTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCardPacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardPackDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsOpened = table.Column<bool>(type: "INTEGER", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserEconomyId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCardPacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCardPacks_CardPackDefinitions_CardPackDefinitionId",
                        column: x => x.CardPackDefinitionId,
                        principalTable: "CardPackDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCardPacks_UserEconomies_UserEconomyId",
                        column: x => x.UserEconomyId,
                        principalTable: "UserEconomies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserCardPacks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDailyRewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastClaimedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConsecutiveDays = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalLoginDays = table.Column<int>(type: "INTEGER", nullable: false),
                    LongestStreak = table.Column<int>(type: "INTEGER", nullable: false),
                    MonthlyClaimedDays = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyRewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDailyRewards_UserEconomies_UserId",
                        column: x => x.UserId,
                        principalTable: "UserEconomies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserDailyRewards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserQuests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuestDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentProgress = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRewardClaimed = table.Column<bool>(type: "INTEGER", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RewardClaimedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserEconomyId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserQuests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserQuests_QuestDefinitions_QuestDefinitionId",
                        column: x => x.QuestDefinitionId,
                        principalTable: "QuestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserQuests_UserEconomies_UserEconomyId",
                        column: x => x.UserEconomyId,
                        principalTable: "UserEconomies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserQuests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRankings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    RankPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonStartAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeasonHighestRank = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalWins = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalLosses = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRankings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRankings_UserEconomies_UserId",
                        column: x => x.UserId,
                        principalTable: "UserEconomies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRankings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CardDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    ObtainedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCards_CardDefinitions_CardDefinitionId",
                        column: x => x.CardDefinitionId,
                        principalTable: "CardDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCards_UserCardPacks_CardPackId",
                        column: x => x.CardPackId,
                        principalTable: "UserCardPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserCards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardAbilities_AbilityTag",
                table: "CardAbilities",
                column: "AbilityTag");

            migrationBuilder.CreateIndex(
                name: "IX_CardAbilities_CardId",
                table: "CardAbilities",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_CardAbilities_CardId_IsActive",
                table: "CardAbilities",
                columns: new[] { "CardId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CoinTransactions_CreatedAt",
                table: "CoinTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CoinTransactions_UserEconomyId",
                table: "CoinTransactions",
                column: "UserEconomyId");

            migrationBuilder.CreateIndex(
                name: "IX_CoinTransactions_UserId",
                table: "CoinTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_GameStateId",
                table: "GameSessions",
                column: "GameStateId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_UserId",
                table: "GameSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCardPacks_CardPackDefinitionId",
                table: "UserCardPacks",
                column: "CardPackDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCardPacks_UserEconomyId",
                table: "UserCardPacks",
                column: "UserEconomyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCardPacks_UserId",
                table: "UserCardPacks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCards_CardDefinitionId",
                table: "UserCards",
                column: "CardDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCards_CardPackId",
                table: "UserCards",
                column: "CardPackId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCards_UserId",
                table: "UserCards",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyRewards_ConsecutiveDays",
                table: "UserDailyRewards",
                column: "ConsecutiveDays");

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyRewards_LastClaimedAt",
                table: "UserDailyRewards",
                column: "LastClaimedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyRewards_UserId",
                table: "UserDailyRewards",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserEconomies_UserId",
                table: "UserEconomies",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserQuests_ExpiresAt",
                table: "UserQuests",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserQuests_QuestDefinitionId",
                table: "UserQuests",
                column: "QuestDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserQuests_UserEconomyId",
                table: "UserQuests",
                column: "UserEconomyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserQuests_UserId",
                table: "UserQuests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRankings_Rank",
                table: "UserRankings",
                column: "Rank");

            migrationBuilder.CreateIndex(
                name: "IX_UserRankings_RankPoints",
                table: "UserRankings",
                column: "RankPoints");

            migrationBuilder.CreateIndex(
                name: "IX_UserRankings_UserId",
                table: "UserRankings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardAbilities");

            migrationBuilder.DropTable(
                name: "CoinTransactions");

            migrationBuilder.DropTable(
                name: "GameSessions");

            migrationBuilder.DropTable(
                name: "UserCards");

            migrationBuilder.DropTable(
                name: "UserDailyRewards");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "UserQuests");

            migrationBuilder.DropTable(
                name: "UserRankings");

            migrationBuilder.DropTable(
                name: "UserCardPacks");

            migrationBuilder.DropTable(
                name: "QuestDefinitions");

            migrationBuilder.DropTable(
                name: "CardPackDefinitions");

            migrationBuilder.DropTable(
                name: "UserEconomies");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "CardDefinitions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
