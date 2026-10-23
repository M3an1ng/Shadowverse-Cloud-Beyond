using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class EconomyServiceMissingUserTests
{
    [Fact]
    public async Task GetMetaProgressionStateAsync_MissingUser_DoesNotCreateRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tx = new TransactionService(db, NullLogger<TransactionService>.Instance);
        var economy = new EconomyService(db, NullLogger<EconomyService>.Instance, tx);

        var missingUserId = Guid.NewGuid();
        var state = await economy.GetMetaProgressionStateAsync(missingUserId);

        Assert.Equal(missingUserId, state.UserId);
        Assert.Null(state.Rank);
        Assert.Empty(state.Quests);

        Assert.Equal(0, await db.UserEconomies.CountAsync());
        Assert.Equal(0, await db.UserRankings.CountAsync());
        Assert.Equal(0, await db.UserDailyRewards.CountAsync());
    }

    [Fact]
    public async Task AddCoinsAsync_MissingUser_ReturnsFalse_AndWritesNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tx = new TransactionService(db, NullLogger<TransactionService>.Instance);
        var economy = new EconomyService(db, NullLogger<EconomyService>.Instance, tx);

        var missingUserId = Guid.NewGuid();
        var success = await economy.AddCoinsAsync(
            missingUserId,
            amount: 100,
            CoinTransactionType.AdminGrant,
            description: "test");

        Assert.False(success);
        Assert.Equal(0, await db.UserEconomies.CountAsync());
        Assert.Equal(0, await db.CoinTransactions.CountAsync());
    }
}
