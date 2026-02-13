using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.DSL
{
    public class EventHistoryByTypeDslTests
    {
        [Fact]
        public void ContextEventHistory_ByType_Query_ReturnsTypedFilteredEvents()
        {
            var (context, historyService) = CreateGameContext();
            SeedEvents(context, historyService);

            var compiler = CreateCompilerWithRealRegistry();
            var getter = compiler.CompileGetter<object>(
                "context.eventHistory.events.byType(\"Damage\").query(self.Damage >= 3)");

            var result = getter(null!, context);
            var events = Assert.IsAssignableFrom<IEnumerable<IGameEvent>>(result).ToList();

            Assert.Single(events);
            Assert.All(events, evt => Assert.Equal(EventType.Damage, evt.EventType));
        }

        [Fact]
        public void GenericEnumerable_ByType_Query_WorksOutsideEventHistory()
        {
            var (context, _) = CreateGameContext();

            var owner = context.GetRequestingPlayer();
            var opponent = context.GetIdlePlayer();
            var source = new CardInstance(Guid.NewGuid(), 1, owner.PlayerId);
            var target = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId);

            var sourceEvents = new List<IGameEvent>
            {
                new DamageEvent(target, 1, source, DamageKind.Ability),
                new TurnStartEvent(owner),
                new DamageEvent(target, 5, source, DamageKind.Ability)
            };

            var compiler = CreateCompilerWithRealRegistry();
            var getter = compiler.CompileGetter<List<IGameEvent>>(
                "self.byType(\"Damage\").query(self.Damage > 1)");

            var result = getter(sourceEvents, context);
            var events = Assert.IsAssignableFrom<IEnumerable<IGameEvent>>(result).ToList();

            Assert.Single(events);
            Assert.Equal(EventType.Damage, events[0].EventType);
        }

        [Fact]
        public void ByType_WithUnknownEventType_ReturnsEmptyEnumerable()
        {
            var (context, _) = CreateGameContext();
            var sourceEvents = new List<IGameEvent>
            {
                new TurnStartEvent(context.GetRequestingPlayer())
            };

            var compiler = CreateCompilerWithRealRegistry();
            var getter = compiler.CompileGetter<List<IGameEvent>>("self.byType(\"NotARealType\")");

            var result = getter(sourceEvents, context);
            var events = Assert.IsAssignableFrom<IEnumerable<IGameEvent>>(result).ToList();

            Assert.Empty(events);
        }

        private static DslCompiler CreateCompilerWithRealRegistry()
        {
            var registry = new FunctionRegistry(NullLogger<FunctionRegistry>.Instance);
            var cache = new Mock<IDslExpressionCache>();
            var gameDataProvider = new Mock<IGameDataProvider>();
            return new DslCompiler(
                NullLogger<DslCompiler>.Instance,
                registry,
                cache.Object,
                gameDataProvider.Object);
        }

        private static (GameContext Context, InMemoryEventHistoryService HistoryService) CreateGameContext()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);

            var historyService = new InMemoryEventHistoryService(NullLogger<InMemoryEventHistoryService>.Instance);
            services.AddSingleton<IEventHistoryService>(historyService);
            var serviceProvider = services.BuildServiceProvider();

            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game-bytype", player1, player2, player1.PlayerId);

            var context = new GameContext(
                serviceProvider,
                new Mock<IMapper>().Object,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player1.PlayerId);

            return (context, historyService);
        }

        private static void SeedEvents(GameContext context, IEventHistoryService historyService)
        {
            var owner = context.GetRequestingPlayer();
            var opponent = context.GetIdlePlayer();
            var source = new CardInstance(Guid.NewGuid(), 1, owner.PlayerId);
            var target = new CardInstance(Guid.NewGuid(), 2, opponent.PlayerId);

            var gameId = context.CurrentGameState.GameId;
            historyService.StoreEventAsync(gameId, new DamageEvent(target, 2, source, DamageKind.Ability)).GetAwaiter().GetResult();
            historyService.StoreEventAsync(gameId, new TurnStartEvent(owner)).GetAwaiter().GetResult();
            historyService.StoreEventAsync(gameId, new DamageEvent(target, 4, source, DamageKind.Ability)).GetAwaiter().GetResult();
        }
    }
}
