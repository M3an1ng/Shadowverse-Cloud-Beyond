using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class WinrateStatsServiceTests
{
    [Fact]
    public async Task ApplyPostCommitAsync_NonOfficialMatch_WritesFactsOnly()
    {
        await using var harness = await WinrateStatsHarness.CreateAsync();
        var user1 = harness.AddUser("official-false-u1");
        await harness.SaveChangesAsync();

        var aiPlayerId = Guid.NewGuid();
        var gameState = harness.CreateGameState(
            gameId: Guid.NewGuid().ToString(),
            player1Id: user1,
            player2Id: aiPlayerId,
            player2IsAi: true,
            initialDeckCount: GameConstants.Cards.MaxDeckSize);

        var events = harness.BuildStandardEvents(gameState, winnerId: user1, loserId: aiPlayerId);
        await harness.ApplyAsync(gameState, events);

        var facts = await harness.Db.GameParticipantStatsFacts
            .AsNoTracking()
            .Where(item => item.GameId == gameState.GameId)
            .ToListAsync();

        Assert.Equal(2, facts.Count);
        Assert.All(facts, fact => Assert.False(fact.IsOfficial));
        Assert.Contains(facts, fact => fact.PlayerId == user1 && fact.IsFirstPlayer == true);
        Assert.Contains(facts, fact => fact.PlayerId == aiPlayerId && fact.IsFirstPlayer == false);
        Assert.All(facts, fact => Assert.Equal(6, fact.EndTurnNumber));
        Assert.Empty(await harness.Db.CardStatsAggs.ToListAsync());
        Assert.Empty(await harness.Db.DeckStatsAggs.ToListAsync());
        Assert.Empty(await harness.Db.UserClassStatsAggs.ToListAsync());
    }

    [Fact]
    public async Task ApplyPostCommitAsync_OfficialMatch_UpdatesAggs()
    {
        await using var harness = await WinrateStatsHarness.CreateAsync();
        var user1 = harness.AddUser("official-true-u1");
        var user2 = harness.AddUser("official-true-u2");
        await harness.SaveChangesAsync();

        var gameState = harness.CreateGameState(
            gameId: Guid.NewGuid().ToString(),
            player1Id: user1,
            player2Id: user2,
            player2IsAi: false,
            initialDeckCount: GameConstants.Cards.MaxDeckSize);

        var events = harness.BuildStandardEvents(gameState, winnerId: user1, loserId: user2);
        await harness.ApplyAsync(gameState, events);

        var facts = await harness.Db.GameParticipantStatsFacts
            .AsNoTracking()
            .Where(item => item.GameId == gameState.GameId)
            .ToListAsync();
        Assert.Equal(2, facts.Count);
        Assert.All(facts, fact => Assert.True(fact.IsOfficial));
        Assert.Contains(facts, fact => fact.PlayerId == user1 && fact.IsFirstPlayer == true);
        Assert.Contains(facts, fact => fact.PlayerId == user2 && fact.IsFirstPlayer == false);
        Assert.All(facts, fact => Assert.Equal(6, fact.EndTurnNumber));

        var cardAgg = await harness.Db.CardStatsAggs.SingleAsync();
        Assert.Equal(2, cardAgg.DeckGames);
        Assert.Equal(1, cardAgg.DeckWins);
        Assert.Equal(2, cardAgg.DrawnGames);
        Assert.Equal(1, cardAgg.DrawnWins);
        Assert.Equal(1, cardAgg.PlayedGames);
        Assert.Equal(1, cardAgg.PlayedWins);

        var deckAgg = await harness.Db.DeckStatsAggs.SingleAsync();
        Assert.Equal(2, deckAgg.TotalGames);
        Assert.Equal(1, deckAgg.TotalWins);
        Assert.Equal(1, deckAgg.GoingFirstGames);
        Assert.Equal(1, deckAgg.GoingFirstWins);
        Assert.Equal(1, deckAgg.GoingSecondGames);
        Assert.Equal(0, deckAgg.GoingSecondWins);

        var userClassAggs = await harness.Db.UserClassStatsAggs
            .AsNoTracking()
            .OrderBy(item => item.UserId)
            .ToListAsync();
        Assert.Equal(2, userClassAggs.Count);
        Assert.All(userClassAggs, item => Assert.Equal(1, item.TotalGames));
        Assert.Contains(userClassAggs, item => item.UserId == user1 && item.TotalWins == 1);
        Assert.Contains(userClassAggs, item => item.UserId == user2 && item.TotalWins == 0);
    }

    [Fact]
    public async Task ApplyPostCommitAsync_RepeatedSettlement_IsIdempotent()
    {
        await using var harness = await WinrateStatsHarness.CreateAsync();
        var user1 = harness.AddUser("idempotent-u1");
        var user2 = harness.AddUser("idempotent-u2");
        await harness.SaveChangesAsync();

        var gameState = harness.CreateGameState(
            gameId: Guid.NewGuid().ToString(),
            player1Id: user1,
            player2Id: user2,
            player2IsAi: false,
            initialDeckCount: GameConstants.Cards.MaxDeckSize);

        var events = harness.BuildStandardEvents(gameState, winnerId: user1, loserId: user2);
        await harness.ApplyAsync(gameState, events);
        await harness.ApplyAsync(gameState, events);

        Assert.Equal(2, await harness.Db.GameParticipantStatsFacts.CountAsync(item => item.GameId == gameState.GameId));
        var cardAgg = await harness.Db.CardStatsAggs.SingleAsync();
        Assert.Equal(2, cardAgg.DeckGames);
        Assert.Equal(1, cardAgg.DeckWins);
        Assert.Equal(1, cardAgg.PlayedGames);
        Assert.Equal(1, cardAgg.PlayedWins);

        var deckAgg = await harness.Db.DeckStatsAggs.SingleAsync();
        Assert.Equal(2, deckAgg.TotalGames);
        Assert.Equal(1, deckAgg.TotalWins);
        Assert.Equal(1, deckAgg.GoingFirstGames);
        Assert.Equal(1, deckAgg.GoingFirstWins);
        Assert.Equal(1, deckAgg.GoingSecondGames);
        Assert.Equal(0, deckAgg.GoingSecondWins);

        var userClassAggs = await harness.Db.UserClassStatsAggs
            .AsNoTracking()
            .Where(item => item.DeckClass == CardClass.Forestcraft)
            .ToListAsync();
        Assert.Equal(2, userClassAggs.Count);
        Assert.Equal(2, userClassAggs.Sum(item => item.TotalGames));
        Assert.Equal(1, userClassAggs.Sum(item => item.TotalWins));
    }

    private sealed class WinrateStatsHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;
        private readonly GameContextAccessor _contextAccessor = new();
        private readonly IMapper _mapper = Mock.Of<IMapper>();

        private WinrateStatsHarness(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            ApplicationDbContext db,
            WinrateStatsService service)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public WinrateStatsService Service { get; }

        public static async Task<WinrateStatsHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var transactionService = new TransactionService(db, NullLogger<TransactionService>.Instance);
            var runtimeTracker = new GameStatsRuntimeTracker();
            var officialMatchEvaluator = new OfficialMatchEvaluator(db);
            var service = new WinrateStatsService(
                db,
                transactionService,
                runtimeTracker,
                officialMatchEvaluator,
                NullLogger<WinrateStatsService>.Instance);

            var services = new ServiceCollection();
            services.AddSingleton(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService>(new InMemoryEventHistoryService(NullLogger<InMemoryEventHistoryService>.Instance));
            var serviceProvider = services.BuildServiceProvider();

            return new WinrateStatsHarness(connection, serviceProvider, db, service);
        }

        public Guid AddUser(string suffix)
        {
            var id = Guid.NewGuid();
            Db.Users.Add(new User
            {
                Id = id,
                Username = $"test-{suffix}-{id:N}",
                Email = $"test-{suffix}-{id:N}@example.com",
                PasswordHash = "hash"
            });

            return id;
        }

        public Task SaveChangesAsync() => Db.SaveChangesAsync();

        public GameState CreateGameState(
            string gameId,
            Guid player1Id,
            Guid player2Id,
            bool player2IsAi,
            int initialDeckCount)
        {
            var player1 = new PlayerState(player1Id)
            {
                IsFirst = true,
                Class = CardClass.Forestcraft,
                Environment = DeckEnvironment.Open,
                TurnNumber = 6
            };
            var player2 = new PlayerState(player2Id)
            {
                IsFirst = false,
                IsAI = player2IsAi,
                Class = CardClass.Forestcraft,
                Environment = DeckEnvironment.Open,
                TurnNumber = 6
            };

            player1.InitialDeck = BuildDeck(player1Id, initialDeckCount, definitionId: 10001);
            player2.InitialDeck = BuildDeck(player2Id, initialDeckCount, definitionId: 10001);
            player1.InitialDeckDefinitionIds = player1.InitialDeck.Select(card => card.BaseDefinitionId).ToList();
            player2.InitialDeckDefinitionIds = player2.InitialDeck.Select(card => card.BaseDefinitionId).ToList();

            // Opening hand is counted as drawn.
            player1.Hand.Add(player1.InitialDeck[0]);
            player2.Hand.Add(player2.InitialDeck[0]);

            var gameState = new GameState(gameId, player1, player2, player1Id);
            return gameState;
        }

        public IReadOnlyList<IGameEvent> BuildStandardEvents(GameState gameState, Guid winnerId, Guid loserId)
        {
            var player1 = gameState.Player1;
            var player1FirstCard = player1.InitialDeck[0];

            var draw = new DrawEvent(player1, count: 1);
            draw.Target.Add(TargetInfo.ForCard(player1FirstCard));

            var play = new PlayCardEvent(player1, player1FirstCard, PlayMode.Base, paidCost: 0);
            var gameEnd = new GameEndEvent(winnerId, loserId, reason: "测试结算");

            return new IGameEvent[] { draw, play, gameEnd };
        }

        public async Task ApplyAsync(GameState gameState, IReadOnlyList<IGameEvent> events)
        {
            using var context = new GameContext(
                _serviceProvider,
                _mapper,
                _contextAccessor,
                gameState,
                requestingPlayerId: gameState.Player1.PlayerId);

            await Service.ApplyPostCommitAsync(context, events, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
            await _serviceProvider.DisposeAsync();
        }

        private static List<CardInstance> BuildDeck(Guid ownerPlayerId, int count, int definitionId)
        {
            var cards = new List<CardInstance>(count);
            for (int i = 0; i < count; i++)
            {
                cards.Add(new CardInstance(Guid.NewGuid(), definitionId, ownerPlayerId));
            }

            return cards;
        }
    }
}
