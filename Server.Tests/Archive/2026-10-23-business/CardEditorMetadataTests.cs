using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.Cards;

public class CardEditorMetadataTests
{
    private const string EditorUserIdHeader = "X-Editor-UserId";

    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static CardsController CreateCardsController(ApplicationDbContext db)
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
            new StubGameDataProvider(),
            Mock.Of<ITribeRegistry>());
    }

    private static CardsController CreateCardsController(ApplicationDbContext db, IGameDataProvider gameDataProvider)
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
            gameDataProvider,
            Mock.Of<ITribeRegistry>());
    }

    private static AbilitiesController CreateAbilitiesController(ApplicationDbContext db)
    {
        return new AbilitiesController(
            db,
            NullLogger<AbilitiesController>.Instance,
            new StubGameDataProvider());
    }

    private static AbilitiesController CreateAbilitiesController(ApplicationDbContext db, IGameDataProvider gameDataProvider)
    {
        return new AbilitiesController(
            db,
            NullLogger<AbilitiesController>.Instance,
            gameDataProvider);
    }

    private static void SetEditorUserId(ControllerBase controller, Guid userId)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Request.Headers[EditorUserIdHeader] = userId.ToString();
    }

    private static void SetHttpContext(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task UpdateCard_WithEditorUserId_PersistsAndReturnsRecentEditorMetadata()
    {
        var dbName = $"card-editor-meta-card-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Username = "EditorOne",
            Email = "editor-one@test.local",
            PasswordHash = "hash",
            IsActive = true
        });
        db.CardDefinitions.Add(new CardDefinition
        {
            Id = 9001,
            Name = "Before Update",
            Type = CardType.Follower,
            Cost = 1,
            Attack = 1,
            Health = 1,
            Class = CardClass.Neutral
        });
        await db.SaveChangesAsync();

        var controller = CreateCardsController(db);
        SetEditorUserId(controller, userId);

        var result = await controller.UpdateCard(9001, new CardUpsertDto
        {
            Id = 9001,
            Name = "After Update",
            Type = "Follower",
            Rarity = "Bronze",
            Class = "Neutral",
            CardSet = "Legacy",
            Cost = 2,
            Attack = 2,
            Health = 2,
            Description = "updated"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CardDefinitionDto>(ok.Value);
        Assert.Equal("EditorOne", payload.UpdatedBy);
        Assert.NotNull(payload.UpdatedAt);

        var card = await db.CardDefinitions.SingleAsync();
        Assert.Equal("EditorOne", card.UpdatedBy);
        Assert.NotNull(card.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAbility_WithEditorUserId_RefreshesParentCardRecentEditorMetadata()
    {
        var dbName = $"card-editor-meta-ability-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Username = "AbilityEditor",
            Email = "ability-editor@test.local",
            PasswordHash = "hash",
            IsActive = true
        });

        var previousTimestamp = DateTime.UtcNow.AddDays(-2);
        db.CardDefinitions.Add(new CardDefinition
        {
            Id = 9101,
            Name = "Ability Card",
            Type = CardType.Follower,
            Cost = 3,
            Attack = 3,
            Health = 3,
            Class = CardClass.Neutral,
            UpdatedAt = previousTimestamp,
            UpdatedBy = "OldEditor"
        });
        db.CardAbilities.Add(new AbilityDefinition
        {
            Id = 8101,
            CardId = 9101,
            Name = "Old Ability",
            Description = "before",
            AbilityType = 1,
            IsActive = true,
            Priority = 0,
            JsonConfig = "{\"type\":\"Triggered\",\"effects\":[]}"
        });
        await db.SaveChangesAsync();

        var controller = CreateAbilitiesController(db);
        SetEditorUserId(controller, userId);

        using var document = JsonDocument.Parse("""
            {
              "name": "New Ability",
              "description": "after",
              "priority": 1,
              "jsonConfig": {
                "type": "Triggered",
                "trigger": {
                  "eventTypes": ["CardPlayed"]
                },
                "effects": []
              }
            }
            """);

        var result = await controller.UpdateAbility(8101, document.RootElement.Clone());

        Assert.IsType<OkObjectResult>(result);
        var card = await db.CardDefinitions.SingleAsync();
        Assert.Equal("AbilityEditor", card.UpdatedBy);
        Assert.NotNull(card.UpdatedAt);
        Assert.True(card.UpdatedAt > previousTimestamp);
    }

    [Fact]
    public async Task UpdateAbility_RefreshesOnlyParentCardCache()
    {
        var dbName = $"card-editor-meta-ability-cache-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        db.CardDefinitions.Add(new CardDefinition
        {
            Id = 9201,
            Name = "Ability Cache Card",
            Type = CardType.Follower,
            Cost = 3,
            Attack = 3,
            Health = 3,
            Class = CardClass.Neutral
        });
        db.CardAbilities.Add(new AbilityDefinition
        {
            Id = 8201,
            CardId = 9201,
            Name = "Old Ability",
            Description = "before",
            AbilityType = 1,
            IsActive = true,
            Priority = 0,
            JsonConfig = "{\"type\":\"Triggered\",\"effects\":[]}"
        });
        await db.SaveChangesAsync();

        var gameDataProvider = new CountingGameDataProvider();
        var controller = CreateAbilitiesController(db, gameDataProvider);
        SetHttpContext(controller);

        using var document = JsonDocument.Parse("""
            {
              "name": "New Ability",
              "description": "after",
              "priority": 1,
              "jsonConfig": {
                "type": "Triggered",
                "effects": []
              }
            }
            """);

        var result = await controller.UpdateAbility(8201, document.RootElement.Clone());

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, gameDataProvider.FullRefreshCount);
        Assert.Equal(new[] { 9201 }, gameDataProvider.RefreshedCardIds);
    }

    [Fact]
    public async Task DeleteCard_RefreshesOnlyDeletedCardCache()
    {
        var dbName = $"card-editor-delete-cache-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        db.CardDefinitions.Add(new CardDefinition
        {
            Id = 9301,
            Name = "Delete Cache Card",
            Type = CardType.Follower,
            Cost = 4,
            Attack = 4,
            Health = 4,
            Class = CardClass.Neutral
        });
        db.CardAbilities.Add(new AbilityDefinition
        {
            Id = 8301,
            CardId = 9301,
            Name = "Delete Ability",
            Description = "before",
            AbilityType = 1,
            IsActive = true,
            Priority = 0,
            JsonConfig = "{\"type\":\"Triggered\",\"effects\":[]}"
        });
        await db.SaveChangesAsync();

        var gameDataProvider = new CountingGameDataProvider();
        var controller = CreateCardsController(db, gameDataProvider);
        SetHttpContext(controller);

        var result = await controller.DeleteCard(9301);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, gameDataProvider.FullRefreshCount);
        Assert.Equal(new[] { 9301 }, gameDataProvider.RefreshedCardIds);
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

    private sealed class CountingGameDataProvider : IGameDataProvider, IIncrementalGameDataProvider
    {
        private readonly List<int> _refreshedCardIds = new();

        public int FullRefreshCount { get; private set; }
        public IReadOnlyList<int> RefreshedCardIds => _refreshedCardIds;
        public bool IsInitialized => true;
        public Guid DataRevision { get; private set; } = Guid.Empty;
        public IReadOnlyCollection<CardDefinition> Cards => Array.Empty<CardDefinition>();

        public Task InitializeAsync() => Task.CompletedTask;
        public CardDefinition? GetCardDefinition(int cardId) => null;
        public IReadOnlyDictionary<int, CardDefinition> GetAllCardDefinitions() => new Dictionary<int, CardDefinition>();
        public List<AbilityConfig> GetAbilityConfigs(int cardId) => new();
        public AbilityConfig? GetAbilityConfig(long abilityId) => null;
        public IReadOnlyDictionary<int, List<AbilityConfig>> GetAllAbilityConfigs() => new Dictionary<int, List<AbilityConfig>>();
        public BuffDefinition? GetBuffDefinition(string buffId) => null;

        public Task RefreshCacheAsync()
        {
            FullRefreshCount++;
            return Task.CompletedTask;
        }

        public Task RefreshCardAsync(int cardId)
        {
            _refreshedCardIds.Add(cardId);
            DataRevision = Guid.NewGuid();
            return Task.CompletedTask;
        }

        public void ClearCache() { }
        public CacheStatistics GetCacheStatistics() => new();
    }
}
