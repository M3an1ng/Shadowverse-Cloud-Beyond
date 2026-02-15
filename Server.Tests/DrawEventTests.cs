using System;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class DrawEventTests
    {
        [Fact]
        public void Commit_CountMode_UsesCurrentDeckAndBackfillsDrawnCards()
        {
            var (serviceProvider, gameState, player) = CreateContext();

            var first = new CardInstance(Guid.NewGuid(), 1001, player.PlayerId) { CurrentZone = CardZone.Deck };
            var second = new CardInstance(Guid.NewGuid(), 1002, player.PlayerId) { CurrentZone = CardZone.Deck };
            player.Deck.Add(first);
            player.Deck.Add(second);
            gameState.RegisterCardInstance(first);
            gameState.RegisterCardInstance(second);

            var drawEvent = new DrawEvent(player, 1);

            // Change deck order after event creation; Commit should use this latest order.
            player.Deck.Clear();
            player.Deck.Add(second);
            player.Deck.Add(first);

            using var context = new GameContext(
                serviceProvider,
                new Mock<IMapper>().Object,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId);

            var commitResult = drawEvent.Commit(context);

            Assert.True(commitResult.Success, commitResult.ErrorMessage);
            Assert.Single(drawEvent.DrawnCards);
            Assert.Equal(second.InstanceId, drawEvent.DrawnCards[0].InstanceId);
        }

        [Fact]
        public void Commit_CountMode_DeckInsufficient_EmitsDeckEmptyEventAndBackfillsAvailableCards()
        {
            var (serviceProvider, gameState, player) = CreateContext();

            var onlyCard = new CardInstance(Guid.NewGuid(), 1001, player.PlayerId) { CurrentZone = CardZone.Deck };
            player.Deck.Add(onlyCard);
            gameState.RegisterCardInstance(onlyCard);

            var drawEvent = new DrawEvent(player, 2);

            using var context = new GameContext(
                serviceProvider,
                new Mock<IMapper>().Object,
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player.PlayerId);

            var commitResult = drawEvent.Commit(context);

            Assert.True(commitResult.Success, commitResult.ErrorMessage);
            Assert.Single(drawEvent.DrawnCards);
            Assert.Equal(onlyCard.InstanceId, drawEvent.DrawnCards[0].InstanceId);
            Assert.Contains(commitResult.GeneratedEvents, e => e is DeckEmptyOnDrawEvent);
            Assert.Contains(commitResult.GeneratedEvents, e => e is CardMovedEvent);
            Assert.IsType<DeckEmptyOnDrawEvent>(commitResult.GeneratedEvents.First());
        }

        private static (IServiceProvider ServiceProvider, GameState GameState, PlayerState Player) CreateContext()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();

            var player = new PlayerState(Guid.NewGuid());
            var opponent = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("draw-event-tests", player, opponent, player.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            return (serviceProvider, gameState, player);
        }
    }
}
