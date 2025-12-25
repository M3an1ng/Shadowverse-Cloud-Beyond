using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using MartinCostello.Logging.XUnit;
using ShadowverseBeyond.Server.Domain.Effects;
using Scrutor;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using Xunit;
using Xunit.Abstractions;
using Microsoft.AspNetCore.SignalR;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
using Moq;

namespace ShadowverseBeyond.Server.Tests
{
    /// <summary>
    /// Manages the lifecycle of a unique in-memory database for a test collection.
    /// This class is responsible for creating the database, seeding it with initial data,
    /// and ensuring it is deleted after the tests are complete.
    /// It no longer creates the ServiceProvider, leaving that responsibility to the test class.
    /// </summary>
    public class DatabaseFixture : IAsyncLifetime
    {
        public string DbName { get; } = $"TestDb_{Guid.NewGuid()}";
        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public ITestOutputHelper? OutputHelper { get; set; }

        public async Task InitializeAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                if (OutputHelper != null)
                {
                    builder.AddXUnit(OutputHelper);
                }
            });

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(DbName));

            // Register all services from Program.cs to mimic the real application environment
            services.AddAutoMapper(typeof(ApplicationDbContext).Assembly);
            services.AddScoped<ICardRepository, EFCardRepository>();
            services.AddSingleton<IGameDataProvider, GameDataProvider>();
            services.AddSingleton<IGameStateManager, InMemoryGameStateManager>();
            services.AddScoped<IAiService, AiService>();
            services.AddMemoryCache();
            services.AddScoped<ITransactionService, TransactionService>();
            services.AddScoped<IEconomyService, EconomyService>();
            services.AddScoped<IMetaProgressionService, MetaProgressionService>();
            services.AddDslFunctionSystem();
            services.AddScoped<IQueryDslParser, QueryDslParser>();
            services.AddScoped<IQueryExecutor, QueryExecutor>();
            services.AddSingleton<IDslExpressionCache, DslExpressionCache>(); // Add the missing cache registration
            services.AddScoped<IDslCompiler, DslCompiler>();
            services.AddScoped<AbilityLoader>();
            services.AddScoped<IValueSourceResolver, ValueSourceResolver>();
            services.AddScoped<IConditionFactory, ConditionFactory>();
            services.AddScoped<IConditionEvaluator, DslConditionEvaluator>();
            services.AddScoped<IBuffEvaluator, BuffEvaluator>();
            services.AddScoped<IEffectExecutor, EffectExecutor>();
            services.AddScoped<TargetSelectionService>();
            services.AddScoped<ITargetFactory, TargetFactory>();
            services.AddScoped<ITargetProcessingService, TargetProcessingService>();
            services.AddScoped<IPendingActionService, PendingActionService>();
            services.Scan(scan => scan
                .FromAssemblyOf<ApplicationDbContext>()
                .AddClasses(classes => classes.AssignableTo(typeof(EffectHandlerBase<>)))
                .AsSelf()
                .WithScopedLifetime());
            services.AddSingleton<IAbilityFactory, AbilityFactory>();
            services.AddSingleton<AbilityManager>();
            services.AddSingleton<IAbilityManager>(provider => provider.GetRequiredService<AbilityManager>());
            services.AddScoped<IBuffManager, BuffManager>();
            services.AddScoped<IBuffFactory, BuffFactory>();
            services.AddScoped<ICardFactory, CardFactory>();
            services.AddSingleton<BuffStackingEngine>();
            services.AddScoped<IBuffService, BuffService>();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddScoped<IEventProcessor, EventProcessor>();
            services.AddScoped<IEffectQueue, EffectQueue>();
            services.AddSingleton<IEffectQueueService, EffectQueueService>();
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IGuestDeckStore, GuestDeckStore>();
            services.AddSingleton<IDeckCodeService, DeckCodeService>();
            services.AddScoped<IDeckValidationService, DeckValidationService>();
            services.AddScoped<IDeckService, DeckService>();
            // Provide a stubbed HubContext for PendingActionService dependencies in tests
            var hubContextMock = new Mock<IHubContext<GameHub, IGameClient>>();
            services.AddSingleton<IHubContext<GameHub, IGameClient>>(hubContextMock.Object);

            ServiceProvider = services.BuildServiceProvider();

            await SeedDatabaseAsync();

            // Pre-initialize caches for tests
            var gdp = ServiceProvider.GetRequiredService<IGameDataProvider>();
            await gdp.RefreshCacheAsync();
        }

        private async Task SeedDatabaseAsync()
        {
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            OutputHelper?.WriteLine($"---- [DatabaseFixture] Initializing and Seeding Database for {DbName} ----");

            await dbContext.Database.EnsureCreatedAsync();
            OutputHelper?.WriteLine("[DatabaseFixture] Database ensured to be created.");

            // --- Seed Card Definitions from card_data.json ---
            var jsonFilePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "card_data.json");
            jsonFilePath = Path.GetFullPath(jsonFilePath);
            OutputHelper?.WriteLine($"[DatabaseFixture] Attempting to seed CardDefinitions from: {jsonFilePath}");

            if (File.Exists(jsonFilePath))
            {
                var jsonData = await File.ReadAllTextAsync(jsonFilePath);
                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
                var cardDtos = JsonSerializer.Deserialize<List<CardDefinitionDto>>(jsonData, jsonOptions);

                if (cardDtos != null)
                {
                    var cardEntities = cardDtos.Select(dto => new CardDefinition
                    {
                        Id = dto.CardId,
                        Name = dto.Name,
                        Cost = dto.Cost,
                        Type = dto.Type,
                        Rarity = Enum.TryParse<CardRarity>(dto.Rarity, true, out var rarity) ? rarity : CardRarity.Bronze,
                        CardSet = dto.CardSet,
                        Attack = dto.Attack,
                        Health = dto.Health,
                        Description = dto.Description ?? string.Empty,
                        Keywords = dto.Keywords,
                        EvolveId = dto.EvolveId,
                        Tribe = dto.Tribe,
                        Class = Enum.TryParse<CardClass>(dto.Class, true, out var cardClass) ? cardClass : CardClass.Neutral,
                        AbilityIds = new List<long>()
                    }).ToList();

                    await dbContext.CardDefinitions.AddRangeAsync(cardEntities);
                    OutputHelper?.WriteLine($"[DatabaseFixture] Added {cardEntities.Count} CardDefinitions to the context.");
                }
            }
            else
            {
                OutputHelper?.WriteLine($"[DatabaseFixture] Seed file not found at {jsonFilePath}. Skipping CardDefinition seeding.");
            }

            // --- Seed Ability Definitions (Manual for now) ---
            var wisdomOfLight = new AbilityDefinition
            {
                Id = 2001,
                CardId = 10001,
                Name = "Wisdom of Light",
                Description = "Draw a card.",
                AbilityTag = "Fanfare",
                AbilityType = 1,
                IsActive = true,
                JsonConfig = @"{ ""Trigger"": { ""EventTypes"": [""PlayCard""] }, ""Effects"": [ { ""Type"": ""Draw"", ""Parameters"": { ""Amount"": { ""Type"": ""Static"", ""Value"": ""1"" } } } ] }"
            };

            var angelicArrow = new AbilityDefinition
            {
                Id = 2002,
                CardId = 10002,
                Name = "Angelic Arrow",
                Description = "Deal 1 damage to an enemy follower or enemy leader.",
                AbilityTag = "Fanfare",
                AbilityType = 1,
                IsActive = true,
                JsonConfig = @"{ ""Trigger"": { ""EventTypes"": [""PlayCard""] }, ""Effects"": [ { ""Type"": ""Damage"", ""Parameters"": { ""Amount"": { ""Type"": ""Static"", ""Value"": 1 }, ""Target"": { ""SelectorType"": ""PlayerChoice"", ""Query"": { ""SourcePoolExpression"": ""opponent.fieldAndLeader.query(self.IsFollower or self.IsLeader)"" } } } } ] }"
            };

            dbContext.CardAbilities.AddRange(wisdomOfLight, angelicArrow);
            OutputHelper?.WriteLine("[DatabaseFixture] Added 2 AbilityDefinitions to the context.");

            var changes = await dbContext.SaveChangesAsync();
            OutputHelper?.WriteLine($"[DatabaseFixture] Saved {changes} changes to the database.");

            var cardCount = await dbContext.CardDefinitions.CountAsync();
            var abilityCount = await dbContext.CardAbilities.CountAsync();
            OutputHelper?.WriteLine($"[DatabaseFixture] Verified {cardCount} cards and {abilityCount} abilities in the database.");
            OutputHelper?.WriteLine("---- [DatabaseFixture] Database Initialization Finished ----");
        }

        public async Task DisposeAsync()
        {
            if (ServiceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    [CollectionDefinition("DatabaseCollection")]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
