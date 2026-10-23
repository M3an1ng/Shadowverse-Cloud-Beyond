using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public sealed class MetaProgressionServiceCoinPolicyTests
{
    [Fact]
    public async Task ApplyPostCommitAsync_OfficialMatch_GrantsCoinsToBothPlayers()
    {
        await using var harness = await MetaProgressionHarness.CreateAsync();
        var user1 = harness.AddUser("user1");
        var user2 = harness.AddUser("user2");
        await harness.SaveChangesAsync();

        var gameState = harness.CreateGameState(
            gameId: Guid.NewGuid().ToString(),
            player1Id: user1,
            player2Id: user2,
            player2IsAi: false,
            player1InitialDeckCount: GameConstants.Cards.MaxDeckSize,
            player2InitialDeckCount: GameConstants.Cards.MaxDeckSize);

        await harness.ApplyAsync(gameState, winnerId: user1, loserId: user2, reason: null);

        var transactions = await harness.Db.CoinTransactions
            .OrderBy(t => t.Amount)
            .ToListAsync();

        Assert.Equal(2, transactions.Count);
        Assert.Contains(transactions, t => t.UserId == user1 && t.Type == CoinTransactionType.GameWin && t.Amount > 0);
        Assert.Contains(transactions, t => t.UserId == user2 && t.Type == CoinTransactionType.GameLoss && t.Amount > 0);
    }

    [Fact]
    public async Task ApplyPostCommitAsync_OfficialMatch_GrantsDirectCoinsAndCompletesMatchQuestsWithoutClaiming()
    {
        await using var harness = await MetaProgressionHarness.CreateAsync();
        var winnerId = harness.AddUser("winner");
        var loserId = harness.AddUser("loser");
        harness.Db.QuestDefinitions.AddRange(
            new QuestDefinition
            {
                Id = 301,
                Name = "Play official match",
                Description = "Play one official match",
                Type = QuestType.PlayGames,
                ActivityType = QuestActivityType.PlayMatch,
                Category = QuestCategory.Daily,
                TargetValue = 1,
                CoinReward = 500,
                DeduplicationPolicy = QuestDeduplicationPolicy.PerSourceEntityPerPeriod,
                IsActive = true
            },
            new QuestDefinition
            {
                Id = 302,
                Name = "Win official match",
                Description = "Win one official match",
                Type = QuestType.WinGames,
                ActivityType = QuestActivityType.WinMatch,
                Category = QuestCategory.Daily,
                TargetValue = 1,
                CoinReward = 700,
                DeduplicationPolicy = QuestDeduplicationPolicy.PerSourceEntityPerPeriod,
                IsActive = true
            });
        await harness.SaveChangesAsync();

        var gameId = Guid.NewGuid().ToString();
        var gameState = harness.CreateGameState(
            gameId: gameId,
            player1Id: winnerId,
            player2Id: loserId,
            player2IsAi: false,
            player1InitialDeckCount: GameConstants.Cards.MaxDeckSize,
            player2InitialDeckCount: GameConstants.Cards.MaxDeckSize);

        await harness.ApplyAsync(gameState, winnerId: winnerId, loserId: loserId, reason: null);

        var directCoinTransactions = await harness.Db.CoinTransactions
            .Where(item => item.UserId == winnerId || item.UserId == loserId)
            .ToListAsync();
        Assert.Equal(2, directCoinTransactions.Count);
        Assert.Contains(directCoinTransactions, item => item.UserId == winnerId && item.Type == CoinTransactionType.GameWin);
        Assert.Contains(directCoinTransactions, item => item.UserId == loserId && item.Type == CoinTransactionType.GameLoss);
        Assert.DoesNotContain(directCoinTransactions, item => item.Type == CoinTransactionType.QuestCompletion);

        var quests = await harness.Db.UserQuests
            .Where(item => item.UserId == winnerId || item.UserId == loserId)
            .ToListAsync();
        Assert.Equal(3, quests.Count);
        Assert.All(quests, quest =>
        {
            Assert.True(quest.IsCompleted);
            Assert.False(quest.IsRewardClaimed);
        });
        Assert.Equal(2, quests.Count(item => item.UserId == winnerId));
        Assert.Single(quests.Where(item => item.UserId == loserId));

        var progressEvents = await harness.Db.QuestProgressEvents.ToListAsync();
        Assert.Equal(3, progressEvents.Count);
        Assert.All(progressEvents, item =>
        {
            Assert.Equal("Match", item.SourceEntityType);
            Assert.Equal(gameId, item.SourceEntityId);
        });
    }

    [Fact]
    public async Task ApplyPostCommitAsync_AiMatch_DoesNotGrantCoins()
    {
        await using var harness = await MetaProgressionHarness.CreateAsync();
        var user1 = harness.AddUser("user1");
        var aiPlayerId = Guid.NewGuid();
        await harness.SaveChangesAsync();

        var gameState = harness.CreateGameState(
            gameId: Guid.NewGuid().ToString(),
            player1Id: user1,
            player2Id: aiPlayerId,
            player2IsAi: true,
            player1InitialDeckCount: GameConstants.Cards.MaxDeckSize,
            player2InitialDeckCount: GameConstants.Cards.MaxDeckSize);

        await harness.ApplyAsync(gameState, winnerId: user1, loserId: aiPlayerId, reason: null);

        var transactions = await harness.Db.CoinTransactions
            .Where(t => t.UserId == user1)
            .ToListAsync();

        Assert.Empty(transactions);
    }

    [Fact]
    public async Task ApplyPostCommitAsync_IncompleteDeck_DoesNotGrantCoins()
    {
        await using var harness = await MetaProgressionHarness.CreateAsync();
        var user1 = harness.AddUser("user1");
        var user2 = harness.AddUser("user2");
        await harness.SaveChangesAsync();

        var gameState = harness.CreateGameState(
            gameId: Guid.NewGuid().ToString(),
            player1Id: user1,
            player2Id: user2,
            player2IsAi: false,
            player1InitialDeckCount: GameConstants.Cards.MaxDeckSize - 1,
            player2InitialDeckCount: GameConstants.Cards.MaxDeckSize);

        await harness.ApplyAsync(gameState, winnerId: user1, loserId: user2, reason: null);

        var transactions = await harness.Db.CoinTransactions.ToListAsync();
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task ApplyPostCommitAsync_EarlySurrender_DoesNotGrantCoins()
    {
        await using var harness = await MetaProgressionHarness.CreateAsync();
        var user1 = harness.AddUser("user1");
        var user2 = harness.AddUser("user2");
        await harness.SaveChangesAsync();

        var gameState = harness.CreateGameState(
            gameId: Guid.NewGuid().ToString(),
            player1Id: user1,
            player2Id: user2,
            player2IsAi: false,
            player1InitialDeckCount: GameConstants.Cards.MaxDeckSize,
            player2InitialDeckCount: GameConstants.Cards.MaxDeckSize);

        gameState.Player1.TurnNumber = GameConstants.SuperEvolution.FirstPlayerSuperEvolutionStartTurn - 1;
        gameState.Player2.TurnNumber = GameConstants.SuperEvolution.SecondPlayerSuperEvolutionStartTurn - 1;

        await harness.ApplyAsync(gameState, winnerId: user2, loserId: user1, reason: "投降");

        var transactions = await harness.Db.CoinTransactions.ToListAsync();
        Assert.Empty(transactions);
    }

    private sealed class MetaProgressionHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;
        private readonly GameContextAccessor _contextAccessor = new();
        private readonly IMapper _mapper = Mock.Of<IMapper>();

        private MetaProgressionHarness(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            ApplicationDbContext db,
            MetaProgressionService service)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public MetaProgressionService Service { get; }

        public static async Task<MetaProgressionHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var transactionService = new TransactionService(db, NullLogger<TransactionService>.Instance);
            var economyService = new EconomyService(db, NullLogger<EconomyService>.Instance, transactionService);
            var questProgressService = new QuestProgressService(db, transactionService, NullLogger<QuestProgressService>.Instance);
            var officialMatchEvaluator = new OfficialMatchEvaluator(db);
            var service = new MetaProgressionService(
                db,
                economyService,
                transactionService,
                officialMatchEvaluator,
                questProgressService,
                NullLogger<MetaProgressionService>.Instance);

            var services = new ServiceCollection();
            services.AddSingleton(Mock.Of<IBuffService>());
            services.AddSingleton(Mock.Of<IEventHistoryService>());
            var serviceProvider = services.BuildServiceProvider();

            return new MetaProgressionHarness(connection, serviceProvider, db, service);
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
            int player1InitialDeckCount,
            int player2InitialDeckCount)
        {
            var player1 = new PlayerState(player1Id) { IsFirst = true };
            var player2 = new PlayerState(player2Id) { IsFirst = false, IsAI = player2IsAi };
            player1.InitialDeck = BuildDeck(player1Id, player1InitialDeckCount);
            player2.InitialDeck = BuildDeck(player2Id, player2InitialDeckCount);

            return new GameState(gameId, player1, player2, player1Id);
        }

        public async Task ApplyAsync(GameState gameState, Guid winnerId, Guid loserId, string? reason)
        {
            using var context = new GameContext(
                _serviceProvider,
                _mapper,
                _contextAccessor,
                gameState,
                requestingPlayerId: winnerId);

            var events = new IGameEvent[] { new GameEndEvent(winnerId, loserId, reason) };
            await Service.ApplyPostCommitAsync(context, events, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
            await _serviceProvider.DisposeAsync();
        }

        private static List<CardInstance> BuildDeck(Guid ownerPlayerId, int count)
        {
            var cards = new List<CardInstance>(count);
            for (int i = 0; i < count; i++)
            {
                cards.Add(new CardInstance(Guid.NewGuid(), GameConstants.Cards.LeaderCardId, ownerPlayerId));
            }

            return cards;
        }
    }
}
