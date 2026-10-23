using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class QuestProgressServiceTests
{
    [Fact]
    public async Task VoteActivity_DeduplicatesSameCardWithinDailyPeriod_AndCountsDifferentCards()
    {
        await using var harness = await QuestProgressHarness.CreateAsync();
        var userId = await harness.AddUserAsync("vote-user");
        harness.Db.QuestDefinitions.Add(new QuestDefinition
        {
            Id = 101,
            Name = "Vote on cards",
            Description = "Vote on two different cards",
            Type = QuestType.CollectCards,
            ActivityType = QuestActivityType.Vote,
            Category = QuestCategory.Daily,
            TargetValue = 2,
            CoinReward = 80,
            DeduplicationPolicy = QuestDeduplicationPolicy.PerSourceEntityPerPeriod,
            IsActive = true
        });
        await harness.Db.SaveChangesAsync();

        var now = new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc);

        var first = await harness.Service.RecordActivityAsync(userId, QuestActivityType.Vote, "Card", "1001", occurredAtUtc: now);
        var duplicate = await harness.Service.RecordActivityAsync(userId, QuestActivityType.Vote, "Card", "1001", occurredAtUtc: now);
        var secondCard = await harness.Service.RecordActivityAsync(userId, QuestActivityType.Vote, "Card", "1002", occurredAtUtc: now);

        Assert.Single(first.UpdatedQuestIds);
        Assert.True(duplicate.IgnoredAsDuplicate);
        Assert.Single(secondCard.CompletedQuestIds);

        var quest = await harness.Db.UserQuests.SingleAsync(q => q.UserId == userId);
        Assert.Equal(2, quest.CurrentProgress);
        Assert.True(quest.IsCompleted);
        Assert.False(quest.IsRewardClaimed);

        var transactions = await harness.Db.CoinTransactions
            .Where(t => t.UserId == userId)
            .ToListAsync();
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task DailyActivity_UsesSeparateUserQuestForNewDay()
    {
        await using var harness = await QuestProgressHarness.CreateAsync();
        var userId = await harness.AddUserAsync("period-user");
        harness.Db.QuestDefinitions.Add(new QuestDefinition
        {
            Id = 102,
            Name = "Daily login",
            Description = "Log in once",
            Type = QuestType.LoginDaily,
            ActivityType = QuestActivityType.Login,
            Category = QuestCategory.Daily,
            TargetValue = 1,
            CoinReward = 50,
            DeduplicationPolicy = QuestDeduplicationPolicy.OncePerPeriod,
            IsActive = true
        });
        await harness.Db.SaveChangesAsync();

        await harness.Service.RecordActivityAsync(
            userId,
            QuestActivityType.Login,
            "Login",
            "ignored",
            occurredAtUtc: new DateTime(2026, 7, 3, 8, 0, 0, DateTimeKind.Utc));
        await harness.Service.RecordActivityAsync(
            userId,
            QuestActivityType.Login,
            "Login",
            "ignored",
            occurredAtUtc: new DateTime(2026, 7, 4, 8, 0, 0, DateTimeKind.Utc));

        var quests = await harness.Db.UserQuests
            .Where(q => q.UserId == userId)
            .OrderBy(q => q.PeriodKey)
            .ToListAsync();

        Assert.Equal(2, quests.Count);
        Assert.Equal("2026-07-03", quests[0].PeriodKey);
        Assert.Equal("2026-07-04", quests[1].PeriodKey);
        Assert.All(quests, quest => Assert.True(quest.IsCompleted));
    }

    [Fact]
    public async Task LoginActivity_CountsOncePerDailyPeriod()
    {
        await using var harness = await QuestProgressHarness.CreateAsync();
        var userId = await harness.AddUserAsync("login-user");
        harness.Db.QuestDefinitions.Add(new QuestDefinition
        {
            Id = 103,
            Name = "Daily login",
            Description = "Log in once",
            Type = QuestType.LoginDaily,
            ActivityType = QuestActivityType.Login,
            Category = QuestCategory.Daily,
            TargetValue = 1,
            CoinReward = 50,
            DeduplicationPolicy = QuestDeduplicationPolicy.OncePerPeriod,
            IsActive = true
        });
        await harness.Db.SaveChangesAsync();

        var now = new DateTime(2026, 7, 3, 8, 0, 0, DateTimeKind.Utc);

        var first = await harness.Service.RecordActivityAsync(userId, QuestActivityType.Login, "Login", "login", occurredAtUtc: now);
        var second = await harness.Service.RecordActivityAsync(userId, QuestActivityType.Login, "Login", "login", occurredAtUtc: now.AddHours(1));

        Assert.Single(first.CompletedQuestIds);
        Assert.True(second.IgnoredAsDuplicate);

        var quest = await harness.Db.UserQuests.SingleAsync(q => q.UserId == userId);
        Assert.Equal(1, quest.CurrentProgress);
    }

    [Fact]
    public async Task CommentActivity_DeduplicatesSameCardWithinDailyPeriod()
    {
        await using var harness = await QuestProgressHarness.CreateAsync();
        var userId = await harness.AddUserAsync("comment-user");
        harness.Db.QuestDefinitions.Add(new QuestDefinition
        {
            Id = 104,
            Name = "Comment on cards",
            Description = "Comment on two different cards",
            Type = QuestType.CollectCards,
            ActivityType = QuestActivityType.Comment,
            Category = QuestCategory.Daily,
            TargetValue = 2,
            CoinReward = 80,
            DeduplicationPolicy = QuestDeduplicationPolicy.PerSourceEntityPerPeriod,
            IsActive = true
        });
        await harness.Db.SaveChangesAsync();

        var now = new DateTime(2026, 7, 3, 8, 0, 0, DateTimeKind.Utc);

        var first = await harness.Service.RecordActivityAsync(userId, QuestActivityType.Comment, "Card", "1001", occurredAtUtc: now);
        var duplicate = await harness.Service.RecordActivityAsync(userId, QuestActivityType.Comment, "Card", "1001", occurredAtUtc: now);
        var secondCard = await harness.Service.RecordActivityAsync(userId, QuestActivityType.Comment, "Card", "1002", occurredAtUtc: now);

        Assert.Single(first.UpdatedQuestIds);
        Assert.True(duplicate.IgnoredAsDuplicate);
        Assert.Single(secondCard.CompletedQuestIds);

        var quest = await harness.Db.UserQuests.SingleAsync(q => q.UserId == userId);
        Assert.Equal(2, quest.CurrentProgress);
        Assert.True(quest.IsCompleted);
        Assert.Empty(await harness.Db.CoinTransactions.Where(t => t.UserId == userId).ToListAsync());
    }

    [Fact]
    public async Task SpectateActivity_DeduplicatesSameMatchWithinDailyPeriod()
    {
        await using var harness = await QuestProgressHarness.CreateAsync();
        var userId = await harness.AddUserAsync("spectate-user");
        harness.Db.QuestDefinitions.Add(new QuestDefinition
        {
            Id = 105,
            Name = "Spectate matches",
            Description = "Spectate two matches",
            Type = QuestType.PlayGames,
            ActivityType = QuestActivityType.SpectateMatch,
            Category = QuestCategory.Daily,
            TargetValue = 2,
            CoinReward = 80,
            DeduplicationPolicy = QuestDeduplicationPolicy.PerSourceEntityPerPeriod,
            IsActive = true
        });
        await harness.Db.SaveChangesAsync();

        var now = new DateTime(2026, 7, 3, 8, 0, 0, DateTimeKind.Utc);

        var first = await harness.Service.RecordActivityAsync(userId, QuestActivityType.SpectateMatch, "Match", "match-1", occurredAtUtc: now);
        var duplicate = await harness.Service.RecordActivityAsync(userId, QuestActivityType.SpectateMatch, "Match", "match-1", occurredAtUtc: now);
        var secondMatch = await harness.Service.RecordActivityAsync(userId, QuestActivityType.SpectateMatch, "Match", "match-2", occurredAtUtc: now);

        Assert.Single(first.UpdatedQuestIds);
        Assert.True(duplicate.IgnoredAsDuplicate);
        Assert.Single(secondMatch.CompletedQuestIds);

        var quest = await harness.Db.UserQuests.SingleAsync(q => q.UserId == userId);
        Assert.Equal(2, quest.CurrentProgress);
        Assert.True(quest.IsCompleted);
        Assert.Equal(2, await harness.Db.QuestProgressEvents.CountAsync(item => item.UserId == userId));
    }

    private sealed class QuestProgressHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private QuestProgressHarness(
            SqliteConnection connection,
            ApplicationDbContext db,
            QuestProgressService service)
        {
            _connection = connection;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public QuestProgressService Service { get; }

        public static async Task<QuestProgressHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var transactionService = new TransactionService(db, NullLogger<TransactionService>.Instance);
            var service = new QuestProgressService(db, transactionService, NullLogger<QuestProgressService>.Instance);

            return new QuestProgressHarness(connection, db, service);
        }

        public async Task<Guid> AddUserAsync(string username)
        {
            var userId = Guid.NewGuid();
            Db.Users.Add(new User
            {
                Id = userId,
                Username = username,
                Email = $"{username}@example.com",
                PasswordHash = "hash",
                IsActive = true
            });
            await Db.SaveChangesAsync();
            return userId;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
