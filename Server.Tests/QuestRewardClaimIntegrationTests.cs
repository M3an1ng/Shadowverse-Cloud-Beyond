using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class QuestRewardClaimIntegrationTests
{
    [Fact]
    public async Task ClaimQuestReward_IsSeparatedFromCompletion_AndIsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Username = "test-user",
            Email = "test@example.com",
            PasswordHash = "hash"
        });

        db.QuestDefinitions.Add(new QuestDefinition
        {
            Id = 1,
            Name = "Play 1 game",
            Description = "Play a game",
            Type = QuestType.PlayGames,
            Category = QuestCategory.Daily,
            TargetValue = 1,
            CoinReward = 42,
            ExperienceReward = 0,
            RefreshIntervalHours = 24,
            IsActive = true
        });

        await db.SaveChangesAsync();

        var tx = new TransactionService(db, NullLogger<TransactionService>.Instance);
        var economy = new EconomyService(db, NullLogger<EconomyService>.Instance, tx);

        var completed = await economy.UpdateQuestProgressAsync(userId, QuestType.PlayGames, increment: 1);
        Assert.True(completed);

        var coinsBeforeClaim = await economy.GetUserCoinsAsync(userId);
        var quests = await economy.GetUserQuestsAsync(userId);
        Assert.Single(quests);
        Assert.True(quests[0].IsCompleted);
        Assert.False(quests[0].IsRewardClaimed);

        var claim = await economy.ClaimQuestRewardAsync(userId, quests[0].Id);
        Assert.NotNull(claim);
        Assert.Equal(42, claim!.CoinReward);

        var coinsAfterClaim = await economy.GetUserCoinsAsync(userId);
        Assert.Equal(coinsBeforeClaim + 42, coinsAfterClaim);

        var claimAgain = await economy.ClaimQuestRewardAsync(userId, quests[0].Id);
        Assert.Null(claimAgain);

        var transactions = await db.CoinTransactions
            .Where(t => t.UserId == userId && t.Type == CoinTransactionType.QuestCompletion)
            .ToListAsync();
        Assert.Single(transactions);
    }
}

