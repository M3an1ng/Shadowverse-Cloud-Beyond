using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public sealed class TribeSemanticsTests
    {
        [Fact]
        public async Task TribeRegistry_NormalizeAndRegisterAsync_ResolvesAliasAndRegistersUnknownTribe()
        {
            var (serviceProvider, registry, dbName) = await BuildRegistryAsync(
                new TribeDefinition
                {
                    Name = "亡者",
                    Aliases = new List<string> { "Reanimate" },
                    Behavior = TribeBehavior.Normal,
                    IsEnabled = true
                },
                new CardDefinition
                {
                    Id = 1,
                    Name = "Soldier",
                    Tribe = new List<string> { "士兵" }
                });

            await registry.RefreshAsync();

            Assert.Equal("亡者", registry.Normalize("Reanimate"));
            Assert.Contains("士兵", registry.GetKnownNormalTribes());
            Assert.True(registry.TryGetDefinition(GameConstants.Tribes.MatchAll, out var matchAllDefinition));
            Assert.Equal(TribeBehavior.MatchAllKnownTribes, matchAllDefinition.Behavior);

            var normalized = await registry.NormalizeAndRegisterAsync(new[] { "星灵", " Reanimate " });

            Assert.Equal(2, normalized.Count);
            Assert.Contains("星灵", normalized);
            Assert.Contains("亡者", normalized);

            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await dbContext.TribeDefinitions.AnyAsync(definition => definition.Name == "星灵"));
        }

        [Fact]
        public async Task DslEquality_AndContains_UseTribeSemantics()
        {
            var (serviceProvider, registry, _) = await BuildRegistryAsync(
                new TribeDefinition
                {
                    Name = "亡者",
                    Aliases = new List<string> { "Reanimate" },
                    Behavior = TribeBehavior.Normal,
                    IsEnabled = true
                },
                new CardDefinition
                {
                    Id = 1,
                    Name = "Pocket",
                    Tribe = new List<string> { "士兵" }
                },
                new CardDefinition
                {
                    Id = 2,
                    Name = "AllTribe",
                    Tribe = new List<string> { GameConstants.Tribes.MatchAll }
                });

            await registry.RefreshAsync();

            var compiler = new DslCompiler(
                NullLogger<DslCompiler>.Instance,
                new FunctionRegistry(NullLogger<FunctionRegistry>.Instance),
                Mock.Of<IDslExpressionCache>(),
                Mock.Of<IGameDataProvider>());

            var allTribeCard = new CardDefinition
            {
                Id = 100,
                Name = "All",
                Tribe = new List<string> { GameConstants.Tribes.MatchAll }
            };
            var undeadCard = new CardDefinition
            {
                Id = 101,
                Name = "Undead",
                Tribe = new List<string> { "亡者" }
            };

            using var context = CreateGameContext(serviceProvider);

            var equalityPredicate = compiler.CompilePredicate<CardDefinition>("self.Tribe == \"士兵\"");
            var containsPredicate = compiler.CompilePredicate<CardDefinition>("contains(self.Tribe, \"Reanimate\")");

            Assert.True(equalityPredicate(allTribeCard, context));
            Assert.True(containsPredicate(undeadCard, context));
        }

        private static async Task<(ServiceProvider ServiceProvider, TribeRegistry Registry, string DatabaseName)> BuildRegistryAsync(
            params object[] seeds)
        {
            var databaseName = Guid.NewGuid().ToString("N");
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var cards = seeds.OfType<CardDefinition>().ToList();
            var gameDataProvider = new Mock<IGameDataProvider>();
            gameDataProvider.SetupGet(provider => provider.Cards).Returns(cards);
            services.AddSingleton(gameDataProvider.Object);
            services.AddSingleton<IGameDataProvider>(gameDataProvider.Object);
            services.AddSingleton<ITribeRegistry, TribeRegistry>();
            services.AddSingleton<ITribeSemantics, TribeSemantics>();

            var serviceProvider = services.BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                dbContext.TribeDefinitions.AddRange(seeds.OfType<TribeDefinition>());
                await dbContext.SaveChangesAsync();
            }

            return (serviceProvider, (TribeRegistry)serviceProvider.GetRequiredService<ITribeRegistry>(), databaseName);
        }

        private static GameContext CreateGameContext(IServiceProvider serviceProvider)
        {
            var mapper = Mock.Of<IMapper>();
            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("tribe-semantics-tests", player, opponent, player.PlayerId);

            return new GameContext(
                serviceProvider,
                mapper,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId);
        }
    }
}
