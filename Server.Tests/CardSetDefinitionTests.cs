using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.CardSets;

public class CardSetDefinitionTests
{
    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static CardsController CreateController(ApplicationDbContext db)
    {
        var repository = new EFCardRepository(db);
        var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<CardDefinitionProfile>());
        var mapper = mapperConfig.CreateMapper();
        return new CardsController(
            repository,
            NullLogger<CardsController>.Instance,
            mapper,
            db,
            new StubHostEnvironment(),
            new StubGameService(),
            new StubGameDataProvider());
    }

    [Fact]
    public async Task CreateCard_AutoRegistersCardSetByName()
    {
        var dbName = $"cardset-create-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var controller = CreateController(db);

        var dto = new CardUpsertDto
        {
            Name = "Test Follower",
            Type = "Follower",
            Cost = 1,
            Attack = 1,
            Health = 1,
            CardSet = "New Set"
        };

        var result = await controller.CreateCard(dto);
        var created = Assert.IsType<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>(result.Result);
        var payload = Assert.IsType<CardDefinitionDto>(created.Value);

        var set = Assert.Single(db.CardSetDefinitions);
        Assert.Equal("New Set", set.Name);
        Assert.Equal(string.Empty, set.Abbreviation);
        Assert.Equal(0, set.Region);
        Assert.Equal(2, set.Sequence);
        Assert.Contains(DeckEnvironment.Open, set.Environments);

        var card = await db.CardDefinitions.SingleAsync();
        Assert.Equal(set.Id, card.CardSetDefinitionId);
        Assert.Equal("New Set", card.CardSet);
        Assert.Equal("New Set", payload.CardSet);
        Assert.Empty(db.CardPackDefinitions);
    }

    [Fact]
    public async Task CreateCard_ResolvesCardSetByNameCaseInsensitive()
    {
        var dbName = $"cardset-resolve-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var existingSet = new CardSetDefinition
        {
            Name = "Beta Set",
            Abbreviation = "BETA",
            Region = 0,
            Sequence = 2,
            Environments = new List<DeckEnvironment> { DeckEnvironment.Open }
        };
        db.CardSetDefinitions.Add(existingSet);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var dto = new CardUpsertDto
        {
            Name = "Test Spell",
            Type = "Spell",
            Cost = 2,
            CardSet = "beta set"
        };

        var result = await controller.CreateCard(dto);
        Assert.IsType<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>(result.Result);

        Assert.Single(db.CardSetDefinitions);
        var card = await db.CardDefinitions.SingleAsync();
        Assert.Equal(existingSet.Id, card.CardSetDefinitionId);
        Assert.Equal("Beta Set", card.CardSet);
    }

    [Fact]
    public async Task DeckValidation_UsesCardSetEnvironments()
    {
        var dbName = $"cardset-env-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var set = new CardSetDefinition
        {
            Name = "Open Set",
            Abbreviation = "OPEN",
            Region = 0,
            Sequence = 2,
            Environments = new List<DeckEnvironment> { DeckEnvironment.Open }
        };
        db.CardSetDefinitions.Add(set);
        db.CardDefinitions.Add(new CardDefinition
        {
            Id = 1,
            Name = "Rotation Follower",
            Type = CardType.Follower,
            Cost = 1,
            Attack = 1,
            Health = 1,
            Class = CardClass.Neutral,
            CardSetDefinition = set,
            CardSetDefinitionId = set.Id
        });
        await db.SaveChangesAsync();

        var service = new DeckValidationService(db);
        var cards = new List<DeckCardEntryDto>
        {
            new DeckCardEntryDto { CardDefinitionId = 1, Count = 1 }
        };

        var invalid = await service.ValidateAsync(CardClass.Neutral, cards, (DeckEnvironment)1, true, CancellationToken.None);
        Assert.False(invalid.Success);
        Assert.Equal("Deck contains cards outside the selected environment.", invalid.Error);

        var valid = await service.ValidateAsync(CardClass.Neutral, cards, DeckEnvironment.Open, true, CancellationToken.None);
        Assert.True(valid.Success);
    }

    [Fact]
    public async Task DeckValidation_AllowsCardsWithoutSetDefinition()
    {
        var dbName = $"cardset-legacy-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        db.CardDefinitions.Add(new CardDefinition
        {
            Id = 1,
            Name = "Legacy Follower",
            Type = CardType.Follower,
            Cost = 1,
            Attack = 1,
            Health = 1,
            Class = CardClass.Neutral
        });
        await db.SaveChangesAsync();

        var service = new DeckValidationService(db);
        var cards = new List<DeckCardEntryDto>
        {
            new DeckCardEntryDto { CardDefinitionId = 1, Count = 1 }
        };

        var result = await service.ValidateAsync(CardClass.Neutral, cards, DeckEnvironment.Open, true, CancellationToken.None);
        Assert.True(result.Success);
    }

    private sealed class StubHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ShadowverseBeyond.Tests";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StubGameService : IGameService
    {
        public Task<GameState?> GetOrCreateGameStateAsync(string gameId, Guid? hostPlayerId, Guid? guestPlayerId, bool useAiFallback, IServiceProvider scopeServiceProvider)
        {
            return Task.FromResult<GameState?>(null);
        }

        public GameState? GetGameState(string gameId)
        {
            return null;
        }

        public Task EnqueuePlayerActionAsync(string gameId, PlayerActionWrapper action)
        {
            return Task.CompletedTask;
        }

        public Task SubmitTargetChoiceAsync(string gameId, Guid playerId, SubmitTargetChoiceActionDto dto)
        {
            return Task.CompletedTask;
        }

        public Task BroadcastEventsAsync(string gameId, List<GameEventDto> events)
        {
            return Task.CompletedTask;
        }

        public Task NotifyCardDefinitionUpdatedAsync(int cardId)
        {
            return Task.CompletedTask;
        }

        public Task RefreshHandPlayabilityAsync(string gameId, Guid playerId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RefreshActPlayabilityAsync(string gameId, Guid playerId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<PlayabilityResultDto> GetCardPlayabilityAsync(string gameId, Guid playerId, Guid cardInstanceId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PlayabilityResultDto());
        }

        public void MarkPlayerDisconnected(string gameId, Guid playerId)
        {
        }

        public void MarkPlayerReconnected(string gameId, Guid playerId)
        {
        }
    }

    private sealed class StubGameDataProvider : IGameDataProvider
    {
        public bool IsInitialized => false;
        public Guid DataRevision => Guid.Empty;
        public IReadOnlyCollection<CardDefinition> Cards => Array.Empty<CardDefinition>();

        public Task InitializeAsync() => Task.CompletedTask;
        public CardDefinition? GetCardDefinition(int cardId) => null;
        public IReadOnlyDictionary<int, CardDefinition> GetAllCardDefinitions() => new Dictionary<int, CardDefinition>();
        public List<AbilityConfig> GetAbilityConfigs(int cardId) => new();
        public AbilityConfig? GetAbilityConfig(long abilityId) => null;
        public IReadOnlyDictionary<int, List<AbilityConfig>> GetAllAbilityConfigs() => new Dictionary<int, List<AbilityConfig>>();
        public BuffDefinition? GetBuffDefinition(string buffId) => null;
        public Task RefreshCacheAsync() => Task.CompletedTask;
        public void ClearCache() { }
        public CacheStatistics GetCacheStatistics() => new();
    }
}
