using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class DeckInsertionTests
    {
        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            return services.BuildServiceProvider();
        }

        private static GameContext BuildContext(GameState gameState, Guid requestingPlayerId, IServiceProvider serviceProvider)
        {
            var mapper = new Mock<IMapper>().Object;
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            return new GameContext(serviceProvider, mapper, accessor, gameState, requestingPlayerId);
        }

        private static void SetTestGameDataProvider()
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(It.IsAny<int>()))
                .Returns((int definitionId) => new CardDefinition
                {
                    Id = definitionId,
                    Name = $"DeckInsertionTestCard{definitionId}",
                    Type = CardType.Follower,
                    Attack = 1,
                    Health = 1
                });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(It.IsAny<int>()))
                .Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }

        [Fact]
        public void AddCardsToDeck_DefaultMode_ProducesValidRandomIndexes()
        {
            SetTestGameDataProvider();
            var player = new PlayerState(Guid.NewGuid());
            var deckCard1 = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Deck };
            var deckCard2 = new CardInstance(Guid.NewGuid(), 2, player.PlayerId) { CurrentZone = CardZone.Deck };
            var deckCard3 = new CardInstance(Guid.NewGuid(), 3, player.PlayerId) { CurrentZone = CardZone.Deck };
            player.Deck.AddRange(new[] { deckCard1, deckCard2, deckCard3 });

            var handCard1 = new CardInstance(Guid.NewGuid(), 10, player.PlayerId) { CurrentZone = CardZone.Hand };
            var handCard2 = new CardInstance(Guid.NewGuid(), 11, player.PlayerId) { CurrentZone = CardZone.Hand };
            player.Hand.AddRange(new[] { handCard1, handCard2 });

            var result = player.AddCardsToDeck(new List<CardInstance> { handCard1, handCard2 });

            Assert.True(result.Success);
            var moveEvents = result.GeneratedEvents.OfType<CardMovedEvent>().ToList();
            Assert.Equal(2, moveEvents.Count);
            Assert.All(moveEvents, moveEvent => Assert.True(moveEvent.DestinationIndex.HasValue));
            Assert.InRange(moveEvents[0].DestinationIndex!.Value, 0, 3);
            Assert.InRange(moveEvents[1].DestinationIndex!.Value, 0, 4);
        }

        [Fact]
        public void AddCardsToDeck_BottomMode_UsesDeckEndIndex()
        {
            SetTestGameDataProvider();
            var player = new PlayerState(Guid.NewGuid());
            var deckCard1 = new CardInstance(Guid.NewGuid(), 1, player.PlayerId) { CurrentZone = CardZone.Deck };
            var deckCard2 = new CardInstance(Guid.NewGuid(), 2, player.PlayerId) { CurrentZone = CardZone.Deck };
            player.Deck.AddRange(new[] { deckCard1, deckCard2 });

            var handCard = new CardInstance(Guid.NewGuid(), 10, player.PlayerId) { CurrentZone = CardZone.Hand };
            player.Hand.Add(handCard);

            var result = player.AddCardsToDeck(new List<CardInstance> { handCard }, DeckInsertMode.Bottom);

            Assert.True(result.Success);
            var moveEvent = Assert.Single(result.GeneratedEvents.OfType<CardMovedEvent>());
            Assert.Equal(2, moveEvent.DestinationIndex);
        }

        [Fact]
        public void CardMovedEvent_Commit_UsesDestinationIndexForDeckInsert()
        {
            SetTestGameDataProvider();
            var serviceProvider = BuildServiceProvider();
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId)
            {
                AbilityManager = new Mock<IAbilityManager>().Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var deckCard1 = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId) { CurrentZone = CardZone.Deck };
            var deckCard2 = new CardInstance(Guid.NewGuid(), 2, player1.PlayerId) { CurrentZone = CardZone.Deck };
            player1.Deck.AddRange(new[] { deckCard1, deckCard2 });

            var handCard = new CardInstance(Guid.NewGuid(), 10, player1.PlayerId) { CurrentZone = CardZone.Hand };
            player1.Hand.Add(handCard);

            using var context = BuildContext(gameState, player1.PlayerId, serviceProvider);
            var moveEvent = new CardMovedEvent(
                handCard,
                CardZone.Hand,
                CardZone.Deck,
                CardMoveReason.AddedToDeck,
                destinationIndex: 1);

            var commitResult = moveEvent.Commit(context);

            Assert.True(commitResult.Success);
            Assert.Equal(3, player1.Deck.Count);
            Assert.Equal(deckCard1.InstanceId, player1.Deck[0].InstanceId);
            Assert.Equal(handCard.InstanceId, player1.Deck[1].InstanceId);
            Assert.Equal(deckCard2.InstanceId, player1.Deck[2].InstanceId);
            Assert.DoesNotContain(handCard, player1.Hand);
            Assert.Equal(CardZone.Deck, handCard.CurrentZone);
        }

        [Fact]
        public void ShuffleEffect_UsesConfiguredDeckInsertMode()
        {
            SetTestGameDataProvider();
            var serviceProvider = BuildServiceProvider();
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId)
            {
                AbilityManager = new Mock<IAbilityManager>().Object,
                BuffManager = new Mock<IBuffManager>().Object
            };

            var deckCard1 = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId) { CurrentZone = CardZone.Deck };
            var deckCard2 = new CardInstance(Guid.NewGuid(), 2, player1.PlayerId) { CurrentZone = CardZone.Deck };
            player1.Deck.AddRange(new[] { deckCard1, deckCard2 });

            var handCard = new CardInstance(Guid.NewGuid(), 10, player1.PlayerId) { CurrentZone = CardZone.Hand };
            player1.Hand.Add(handCard);

            using var context = BuildContext(gameState, player1.PlayerId, serviceProvider);
            var handler = new ShuffleEffectHandler(NullLogger<ShuffleEffectHandler>.Instance);
            var effect = new Effect(
                new EffectConfig { Type = EffectType.Shuffle },
                player1.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(handCard) },
                null,
                Guid.NewGuid(),
                null);

            var result = handler.Apply(effect, context, new ShuffleEffectParams { InsertMode = DeckInsertMode.Bottom });

            Assert.True(result.Success);
            var shuffleEvent = Assert.Single(result.GeneratedEvents.OfType<ShuffleEvent>());
            var commitResult = shuffleEvent.Commit(context);
            var moveEvent = Assert.Single(commitResult.GeneratedEvents.OfType<CardMovedEvent>());
            Assert.Equal(2, moveEvent.DestinationIndex);
        }
    }
}
