using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class DevToolsWinrateStatsControllerTests
{
    [Fact]
    public async Task GetSummary_ReturnsSplitWinrateAndAverageTurnForDeckAndClass()
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
            Username = $"tester-{userId:N}",
            Email = $"tester-{userId:N}@example.com",
            PasswordHash = "hash"
        });

        var now = DateTime.UtcNow;
        db.DeckStatsAggs.AddRange(
            new DeckStatsAgg
            {
                Id = Guid.NewGuid(),
                Environment = DeckEnvironment.Open,
                DeckShapeHash = "DECK_HASH_1",
                DeckClass = CardClass.Forestcraft,
                TotalGames = 4,
                TotalWins = 3,
                GoingFirstGames = 2,
                GoingFirstWins = 2,
                GoingSecondGames = 2,
                GoingSecondWins = 1,
                UpdatedAtUtc = now
            },
            new DeckStatsAgg
            {
                Id = Guid.NewGuid(),
                Environment = DeckEnvironment.Open,
                DeckShapeHash = "DECK_HASH_2",
                DeckClass = CardClass.Runecraft,
                TotalGames = 2,
                TotalWins = 1,
                GoingFirstGames = 1,
                GoingFirstWins = 0,
                GoingSecondGames = 1,
                GoingSecondWins = 1,
                UpdatedAtUtc = now.AddMinutes(-1)
            });

        db.GameParticipantStatsFacts.AddRange(
            BuildFact("g1", userId, CardClass.Forestcraft, "DECK_HASH_1", isWinner: true, isFirstPlayer: true, endTurn: 7, now),
            BuildFact("g2", userId, CardClass.Forestcraft, "DECK_HASH_1", isWinner: true, isFirstPlayer: true, endTurn: 6, now),
            BuildFact("g3", userId, CardClass.Forestcraft, "DECK_HASH_1", isWinner: true, isFirstPlayer: false, endTurn: 8, now),
            BuildFact("g4", userId, CardClass.Forestcraft, "DECK_HASH_1", isWinner: false, isFirstPlayer: false, endTurn: 10, now),
            BuildFact("g5", userId, CardClass.Runecraft, "DECK_HASH_2", isWinner: false, isFirstPlayer: true, endTurn: 5, now),
            BuildFact("g6", userId, CardClass.Runecraft, "DECK_HASH_2", isWinner: true, isFirstPlayer: false, endTurn: 7, now));

        await db.SaveChangesAsync();

        var controller = new DevToolsWinrateStatsController(db, NullLogger<DevToolsWinrateStatsController>.Instance);
        var result = await controller.GetSummary(userId, "open", top: 50, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<DevToolsWinrateStatsController.WinrateStatsSummaryResponse>(ok.Value);

        var forestDeck = Assert.Single(payload.DeckStats.Where(item => item.DeckShapeHash == "DECK_HASH_1"));
        Assert.Equal(75d, forestDeck.WinRate);
        Assert.Equal(100d, forestDeck.GoingFirstWinRate);
        Assert.Equal(50d, forestDeck.GoingSecondWinRate);
        Assert.Equal(7.75d, forestDeck.AverageEndTurn);
        Assert.Equal(6.5d, forestDeck.GoingFirstAverageEndTurn);
        Assert.Equal(9d, forestDeck.GoingSecondAverageEndTurn);

        var forestClass = Assert.Single(payload.ClassStats.Where(item => item.DeckClass == CardClass.Forestcraft.ToString()));
        Assert.Equal(4, forestClass.TotalGames);
        Assert.Equal(3, forestClass.TotalWins);
        Assert.Equal(75d, forestClass.WinRate);
        Assert.Equal(2, forestClass.GoingFirstGames);
        Assert.Equal(2, forestClass.GoingFirstWins);
        Assert.Equal(100d, forestClass.GoingFirstWinRate);
        Assert.Equal(2, forestClass.GoingSecondGames);
        Assert.Equal(1, forestClass.GoingSecondWins);
        Assert.Equal(50d, forestClass.GoingSecondWinRate);
        Assert.Equal(7.75d, forestClass.AverageEndTurn);
        Assert.Equal(6.5d, forestClass.GoingFirstAverageEndTurn);
        Assert.Equal(9d, forestClass.GoingSecondAverageEndTurn);

        var runeClass = Assert.Single(payload.ClassStats.Where(item => item.DeckClass == CardClass.Runecraft.ToString()));
        Assert.Equal(2, runeClass.TotalGames);
        Assert.Equal(1, runeClass.TotalWins);
        Assert.Equal(50d, runeClass.WinRate);
        Assert.Equal(0d, runeClass.GoingFirstWinRate);
        Assert.Equal(100d, runeClass.GoingSecondWinRate);
        Assert.Equal(6d, runeClass.AverageEndTurn);
        Assert.Equal(5d, runeClass.GoingFirstAverageEndTurn);
        Assert.Equal(7d, runeClass.GoingSecondAverageEndTurn);
    }

    [Fact]
    public async Task GetSummary_AppliesDateRangeToCardDeckAndClassStats()
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
            Username = $"tester-{userId:N}",
            Email = $"tester-{userId:N}@example.com",
            PasswordHash = "hash"
        });

        db.CardDefinitions.AddRange(
            new CardDefinition
            {
                Id = 10001,
                Name = "远古精灵",
                Type = CardType.Follower,
                Rarity = CardRarity.Bronze
            },
            new CardDefinition
            {
                Id = 20002,
                Name = "火焰魔术",
                Type = CardType.Spell,
                Rarity = CardRarity.Silver
            });

        var day1 = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc);
        var day2 = day1.AddDays(1);

        db.GameParticipantStatsFacts.AddRange(
            BuildFact("d1-g1", userId, CardClass.Forestcraft, "FOREST_HASH", isWinner: true, isFirstPlayer: true, endTurn: 7, day1, "{\"10001\":3}", "[10001]", "[10001]"),
            BuildFact("d1-g2", userId, CardClass.Forestcraft, "FOREST_HASH", isWinner: false, isFirstPlayer: false, endTurn: 8, day1.AddHours(1), "{\"10001\":3}", "[10001]", "[]"),
            BuildFact("d2-g1", userId, CardClass.Runecraft, "RUNE_HASH", isWinner: true, isFirstPlayer: true, endTurn: 6, day2, "{\"20002\":3}", "[20002]", "[20002]"),
            BuildFact("d2-g2", userId, CardClass.Runecraft, "RUNE_HASH", isWinner: true, isFirstPlayer: false, endTurn: 5, day2.AddHours(2), "{\"20002\":3}", "[20002]", "[20002]"));

        await db.SaveChangesAsync();

        var controller = new DevToolsWinrateStatsController(db, NullLogger<DevToolsWinrateStatsController>.Instance);
        var result = await controller.GetSummary(
            userId,
            environment: "open",
            top: 50,
            startDate: "2026-02-01",
            endDate: "2026-02-01",
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<DevToolsWinrateStatsController.WinrateStatsSummaryResponse>(ok.Value);

        var card = Assert.Single(payload.CardStats);
        Assert.Equal(10001, card.CardDefinitionId);
        Assert.Equal(2, card.DeckGames);
        Assert.Equal(1, card.DeckWins);
        Assert.Equal(50d, card.DeckWinRate);

        var deck = Assert.Single(payload.DeckStats);
        Assert.Equal("FOREST_HASH", deck.DeckShapeHash);
        Assert.Equal(2, deck.TotalGames);
        Assert.Equal(1, deck.TotalWins);
        Assert.Equal(50d, deck.WinRate);

        var deckClass = Assert.Single(payload.ClassStats);
        Assert.Equal(CardClass.Forestcraft.ToString(), deckClass.DeckClass);
        Assert.Equal(2, deckClass.TotalGames);
        Assert.Equal(1, deckClass.TotalWins);
        Assert.Equal(50d, deckClass.WinRate);
    }

    private static GameParticipantStatsFact BuildFact(
        string gameId,
        Guid playerId,
        CardClass deckClass,
        string deckShapeHash,
        bool isWinner,
        bool isFirstPlayer,
        int endTurn,
        DateTime endedAtUtc,
        string? initialDeckCardCountsJson = null,
        string? drawnCardIdsJson = null,
        string? playedCardIdsJson = null)
    {
        return new GameParticipantStatsFact
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            PlayerId = playerId,
            IsWinner = isWinner,
            IsFirstPlayer = isFirstPlayer,
            EndTurnNumber = endTurn,
            EndedAtUtc = endedAtUtc,
            DeckClass = deckClass,
            Environment = DeckEnvironment.Open,
            DeckShapeHash = deckShapeHash,
            InitialDeckCardCountsJson = initialDeckCardCountsJson ?? "{\"10001\":40}",
            DrawnCardIdsJson = drawnCardIdsJson ?? "[10001]",
            PlayedCardIdsJson = playedCardIdsJson ?? "[10001]",
            IsOfficial = true
        };
    }
}
