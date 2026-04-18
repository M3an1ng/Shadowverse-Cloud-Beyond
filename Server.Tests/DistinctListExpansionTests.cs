using System.Reflection;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public sealed class DistinctListExpansionTests
    {
        [Fact]
        public async Task DslDistinct_WithScalarKey_PreservesFirstOccurrenceOrder()
        {
            var environment = await CreateEnvironmentAsync();
            using var context = environment.CreateGameContext();

            var cards = new List<TestScalarCard>
            {
                new() { Cost = 2, Name = "A" },
                new() { Cost = 2, Name = "B" },
                new() { Cost = 4, Name = "C" }
            };

            var getter = environment.Compiler.CompileGetter<List<TestScalarCard>>("self.distinct(self.Cost)");
            var result = Assert.IsAssignableFrom<IEnumerable<TestScalarCard>>(getter(cards, context)).ToList();

            Assert.Equal(new[] { "A", "C" }, result.Select(card => card.Name));
        }

        [Fact]
        public async Task DslDistinct_WithGenericListKey_UsesMaximumCardinalityMatching()
        {
            var environment = await CreateEnvironmentAsync();
            using var context = environment.CreateGameContext();

            var cards = new List<TestTagCard>
            {
                new() { Name = "A", Tags = new List<string> { "士兵", "海洋" } },
                new() { Name = "B", Tags = new List<string> { "海洋", "妖精" } },
                new() { Name = "C", Tags = new List<string> { "妖精", "士兵" } }
            };

            var getter = environment.Compiler.CompileGetter<List<TestTagCard>>("self.distinct(self.Tags)");
            var result = Assert.IsAssignableFrom<IEnumerable<TestTagCard>>(getter(cards, context)).ToList();

            Assert.Equal(new[] { "A", "B", "C" }, result.Select(card => card.Name));
        }

        [Fact]
        public async Task DslDistinct_WithTribeKey_ExpandsMatchAllAndUsesMaximumCardinalityMatching()
        {
            var environment = await CreateEnvironmentAsync(
                new[]
                {
                    new TribeDefinition
                    {
                        Name = GameConstants.Tribes.MatchAll,
                        Behavior = TribeBehavior.MatchAllKnownTribes,
                        IsEnabled = true,
                        IsReserved = true
                    }
                },
                new CardDefinition { Id = 1001, Name = "SoldierSeed", Tribe = new List<string> { "士兵" } },
                new CardDefinition { Id = 1002, Name = "FairySeed", Tribe = new List<string> { "妖精" } });

            await environment.TribeRegistry.RefreshAsync();

            using var context = environment.CreateGameContext();
            var cards = new List<CardDefinition>
            {
                new() { Id = 1, Name = "All", Tribe = new List<string> { GameConstants.Tribes.MatchAll } },
                new() { Id = 2, Name = "Soldier", Tribe = new List<string> { "士兵" } },
                new() { Id = 3, Name = "Fairy", Tribe = new List<string> { "妖精" } }
            };

            var getter = environment.Compiler.CompileGetter<List<CardDefinition>>("self.distinct(self.Tribe)");
            var result = Assert.IsAssignableFrom<IEnumerable<CardDefinition>>(getter(cards, context)).ToList();

            Assert.Equal(new[] { "All", "Fairy" }, result.Select(card => card.Name));
        }

        [Fact]
        public async Task DslDistinct_WithTribeCycle_RetainsAllThreeCards()
        {
            var environment = await CreateEnvironmentAsync(
                Array.Empty<TribeDefinition>(),
                new CardDefinition { Id = 2001, Name = "SoldierSeed", Tribe = new List<string> { "士兵" } },
                new CardDefinition { Id = 2002, Name = "OceanSeed", Tribe = new List<string> { "海洋" } },
                new CardDefinition { Id = 2003, Name = "FairySeed", Tribe = new List<string> { "妖精" } });

            await environment.TribeRegistry.RefreshAsync();

            using var context = environment.CreateGameContext();
            var cards = new List<CardDefinition>
            {
                new() { Id = 11, Name = "A", Tribe = new List<string> { "士兵", "海洋" } },
                new() { Id = 12, Name = "B", Tribe = new List<string> { "海洋", "妖精" } },
                new() { Id = 13, Name = "C", Tribe = new List<string> { "妖精", "士兵" } }
            };

            var getter = environment.Compiler.CompileGetter<List<CardDefinition>>("self.distinct(self.Tribe)");
            var result = Assert.IsAssignableFrom<IEnumerable<CardDefinition>>(getter(cards, context)).ToList();

            Assert.Equal(new[] { "A", "B", "C" }, result.Select(card => card.Name));
        }

        [Fact]
        public async Task QueryConfigDistinct_WithScalarKey_MatchesDslBehavior()
        {
            var definitions = new[]
            {
                new CardDefinition { Id = 1, Name = "A", Cost = 2, Type = Dtos.Enums.CardType.Follower },
                new CardDefinition { Id = 2, Name = "B", Cost = 2, Type = Dtos.Enums.CardType.Follower },
                new CardDefinition { Id = 3, Name = "C", Cost = 4, Type = Dtos.Enums.CardType.Follower }
            };

            var environment = await CreateEnvironmentAsync(definitions);
            using var context = environment.CreateGameContext();
            AddCardsToHand(context, definitions.Select(definition => definition.Id).ToArray());

            var query = new QueryConfig
            {
                SourcePoolExpression = "player.Hand",
                Operations = new List<IQueryOperation>
                {
                    new DistinctOperation(new FormulaValueConfig { Formula = "self.Cost" })
                }
            };

            var result = environment.QueryExecutor.ExecuteQuery(query, context);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(new[] { 1, 3 }, result.Results.Cast<CardInstance>().Select(card => card.CurrentDefinitionId));
        }

        [Fact]
        public async Task QueryConfigDistinct_WithGenericListKey_MatchesExpandedMaximumCardinalityBehavior()
        {
            var definitions = new[]
            {
                new CardDefinition { Id = 11, Name = "A", Type = Dtos.Enums.CardType.Follower, Keywords = new List<string> { "Ward", "Storm" } },
                new CardDefinition { Id = 12, Name = "B", Type = Dtos.Enums.CardType.Follower, Keywords = new List<string> { "Storm", "Bane" } },
                new CardDefinition { Id = 13, Name = "C", Type = Dtos.Enums.CardType.Follower, Keywords = new List<string> { "Bane", "Ward" } }
            };

            var environment = await CreateEnvironmentAsync(definitions);
            using var context = environment.CreateGameContext();
            AddCardsToHand(context, definitions.Select(definition => definition.Id).ToArray());

            var query = new QueryConfig
            {
                SourcePoolExpression = "player.Hand",
                Operations = new List<IQueryOperation>
                {
                    new DistinctOperation(new FormulaValueConfig { Formula = "self.Keywords" })
                }
            };

            var result = environment.QueryExecutor.ExecuteQuery(query, context);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(new[] { 11, 12, 13 }, result.Results.Cast<CardInstance>().Select(card => card.CurrentDefinitionId));
        }

        [Fact]
        public async Task QueryConfigDistinct_WithTribeKey_UsesMatchAllExpansion()
        {
            var definitions = new[]
            {
                new CardDefinition { Id = 21, Name = "All", Type = Dtos.Enums.CardType.Follower, Tribe = new List<string> { GameConstants.Tribes.MatchAll } },
                new CardDefinition { Id = 22, Name = "Soldier", Type = Dtos.Enums.CardType.Follower, Tribe = new List<string> { "士兵" } },
                new CardDefinition { Id = 23, Name = "Fairy", Type = Dtos.Enums.CardType.Follower, Tribe = new List<string> { "妖精" } },
                new CardDefinition { Id = 24, Name = "SoldierSeed", Type = Dtos.Enums.CardType.Follower, Tribe = new List<string> { "士兵" } },
                new CardDefinition { Id = 25, Name = "FairySeed", Type = Dtos.Enums.CardType.Follower, Tribe = new List<string> { "妖精" } }
            };

            var environment = await CreateEnvironmentAsync(definitions);
            await environment.TribeRegistry.RefreshAsync();

            using var context = environment.CreateGameContext();
            AddCardsToHand(context, 21, 22, 23);

            var query = new QueryConfig
            {
                SourcePoolExpression = "player.Hand",
                Operations = new List<IQueryOperation>
                {
                    new DistinctOperation(new FormulaValueConfig { Formula = "self.Tribe" })
                }
            };

            var result = environment.QueryExecutor.ExecuteQuery(query, context);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(new[] { 21, 23 }, result.Results.Cast<CardInstance>().Select(card => card.CurrentDefinitionId));
        }

        [Fact]
        public async Task QueryConfigDistinct_WithEntityKey_FailsFast()
        {
            var definitions = new[]
            {
                new CardDefinition { Id = 31, Name = "A", Type = Dtos.Enums.CardType.Follower },
                new CardDefinition { Id = 32, Name = "B", Type = Dtos.Enums.CardType.Follower }
            };

            var environment = await CreateEnvironmentAsync(definitions);
            using var context = environment.CreateGameContext();
            AddCardsToHand(context, 31, 32);

            var query = new QueryConfig
            {
                SourcePoolExpression = "player.Hand",
                Operations = new List<IQueryOperation>
                {
                    new DistinctOperation(new FormulaValueConfig { Formula = "self" })
                }
            };

            var result = environment.QueryExecutor.ExecuteQuery(query, context);
            Assert.False(result.Success);
            Assert.Contains("not supported", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddCardsToHand(GameContext context, params int[] definitionIds)
        {
            var player = context.GetRequestingPlayer();
            foreach (var definitionId in definitionIds)
            {
                player.Hand.Add(new CardInstance(Guid.NewGuid(), definitionId, player.PlayerId));
            }
        }

        private static async Task<TestEnvironment> CreateEnvironmentAsync(params CardDefinition[] definitions)
        {
            return await CreateEnvironmentAsync(Array.Empty<TribeDefinition>(), definitions);
        }

        private static async Task<TestEnvironment> CreateEnvironmentAsync(IEnumerable<TribeDefinition> tribeDefinitions, params CardDefinition[] definitions)
        {
            var databaseName = Guid.NewGuid().ToString("N");
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();

            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseAttack);
            buffService.Setup(service => service.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseHealth);
            buffService.Setup(service => service.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseCost);
            buffService.Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseKeywords);

            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var definitionList = definitions.ToList();
            var definitionLookup = definitionList.ToDictionary(definition => definition.Id);
            var gameDataProvider = new Mock<IGameDataProvider>();
            gameDataProvider.SetupGet(provider => provider.Cards).Returns(definitionList);
            gameDataProvider.Setup(provider => provider.GetCardDefinition(It.IsAny<int>()))
                .Returns((int id) => definitionLookup.TryGetValue(id, out var definition) ? definition : null);
            services.AddSingleton(gameDataProvider.Object);
            services.AddSingleton<IGameDataProvider>(gameDataProvider.Object);
            services.AddSingleton<ITribeRegistry, TribeRegistry>();
            services.AddSingleton<ITribeSemantics, TribeSemantics>();

            var serviceProvider = services.BuildServiceProvider();
            GameServiceProvider.ContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.Logger = NullLogger.Instance;
            SetGameDataProviderInstance(gameDataProvider.Object);

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                dbContext.TribeDefinitions.AddRange(tribeDefinitions);
                await dbContext.SaveChangesAsync();
            }

            var compiler = new DslCompiler(
                NullLogger<DslCompiler>.Instance,
                new FunctionRegistry(NullLogger<FunctionRegistry>.Instance),
                Mock.Of<IDslExpressionCache>(),
                gameDataProvider.Object);

            var queryExecutor = new QueryExecutor(
                NullLogger<QueryExecutor>.Instance,
                compiler,
                Mock.Of<IMapper>(),
                Mock.Of<IQueryDslParser>());

            return new TestEnvironment(
                serviceProvider,
                compiler,
                queryExecutor,
                (TribeRegistry)serviceProvider.GetRequiredService<ITribeRegistry>());
        }

        private static void SetGameDataProviderInstance(IGameDataProvider gameDataProvider)
        {
            var field = typeof(GameDataProvider).GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
            field!.SetValue(null, gameDataProvider);
        }

        private sealed record TestEnvironment(
            ServiceProvider ServiceProvider,
            DslCompiler Compiler,
            QueryExecutor QueryExecutor,
            TribeRegistry TribeRegistry)
        {
            public GameContext CreateGameContext()
            {
                var player = new PlayerState(Guid.NewGuid());
                var opponent = new PlayerState(Guid.NewGuid());
                var gameState = new GameState("distinct-list-expansion-tests", player, opponent, player.PlayerId);

                return new GameContext(
                    ServiceProvider,
                    Mock.Of<IMapper>(),
                    ServiceProvider.GetRequiredService<IGameContextAccessor>(),
                    gameState,
                    player.PlayerId);
            }
        }

        private sealed class TestScalarCard
        {
            public string Name { get; set; } = string.Empty;

            public int Cost { get; set; }
        }

        private sealed class TestTagCard
        {
            public string Name { get; set; } = string.Empty;

            public List<string> Tags { get; set; } = new();
        }
    }
}
