using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class ReplaceDeckEffectHandlerTests
    {
        [Fact]
        public void EffectHandlerRegistry_Registers_ReplaceDeck()
        {
            var registry = new EffectHandlerRegistry(NullLogger<EffectHandlerRegistry>.Instance);

            Assert.True(registry.TryGetHandler(EffectType.ReplaceDeck, out var registration));
            Assert.NotNull(registration);
            Assert.Equal(typeof(ReplaceDeckEffectHandler), registration?.handlerType);
        }

        [Fact]
        public void Apply_WithEmptyDefinitionList_FailsWithoutChangingDeck()
        {
            SetTestGameDataProvider(1001);

            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, Guid.NewGuid());
            var existingCard = AddDeckCard(gameState.Player1, gameState, 1001);

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var handler = new ReplaceDeckEffectHandler(
                NullLogger<ReplaceDeckEffectHandler>.Instance,
                new TrackingCardFactory());

            var effect = new Effect(new EffectConfig { Type = EffectType.ReplaceDeck }, player1Id, new List<ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetInfo>(), null, Guid.NewGuid(), null);
            var result = handler.Apply(effect, context, new ReplaceDeckEffectParams());

            Assert.False(result.Success);
            Assert.Empty(result.GeneratedEvents);
            Assert.Single(gameState.Player1.Deck);
            Assert.Same(existingCard, gameState.Player1.Deck[0]);
        }

        [Fact]
        public void Apply_SelfScope_RebuildsDeckInOrder_AndMovesOldCardsToRemoved()
        {
            SetTestGameDataProvider(1001, 1002, 2001, 2002);

            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, player2Id);
            var oldCard1 = AddDeckCard(gameState.Player1, gameState, 1001);
            var oldCard2 = AddDeckCard(gameState.Player1, gameState, 1002);
            var opponentCard = AddDeckCard(gameState.Player2, gameState, 1001);
            var cardFactory = new TrackingCardFactory();

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var handler = new ReplaceDeckEffectHandler(
                NullLogger<ReplaceDeckEffectHandler>.Instance,
                cardFactory);

            var effect = new Effect(new EffectConfig { Type = EffectType.ReplaceDeck }, player1Id, new List<ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetInfo>(), null, Guid.NewGuid(), null);
            var parameters = new ReplaceDeckEffectParams
            {
                PlayerScope = PlayerScope.Self,
                CardDefinitionIds = new List<int> { 2001, 2002 }
            };

            var applyResult = handler.Apply(effect, context, parameters);

            Assert.True(applyResult.Success);
            Assert.Equal(4, applyResult.GeneratedEvents.OfType<CardMovedEvent>().Count());

            var commitEvents = CommitMoveEvents(applyResult.GeneratedEvents, context);

            Assert.Equal(CardZone.Removed, oldCard1.CurrentZone);
            Assert.Equal(CardZone.Removed, oldCard2.CurrentZone);
            Assert.Equal(CardMoveReason.DeckReplaced, oldCard1.LastMoveReason);
            Assert.Equal(CardMoveReason.DeckReplaced, oldCard2.LastMoveReason);
            Assert.DoesNotContain(oldCard1, gameState.Player1.Deck);
            Assert.DoesNotContain(oldCard2, gameState.Player1.Deck);

            Assert.Equal(new[] { 2001, 2002 }, gameState.Player1.Deck.Select(card => card.BaseDefinitionId).ToArray());
            Assert.All(gameState.Player1.Deck, card => Assert.Equal(CardZone.Deck, card.CurrentZone));

            Assert.Single(gameState.Player2.Deck);
            Assert.Same(opponentCard, gameState.Player2.Deck[0]);
            Assert.DoesNotContain(commitEvents, e => e is CardVanishedEvent);
        }

        [Fact]
        public void Apply_BothScope_RebuildsEachTargetPlayerIndependently()
        {
            SetTestGameDataProvider(1001, 1002, 3001, 3002, 3003);

            var serviceProvider = BuildServiceProvider();
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var gameState = BuildGameState(player1Id, player2Id);
            AddDeckCard(gameState.Player1, gameState, 1001);
            AddDeckCard(gameState.Player2, gameState, 1002);
            var cardFactory = new TrackingCardFactory();

            using var context = BuildContext(gameState, player1Id, serviceProvider);
            var handler = new ReplaceDeckEffectHandler(
                NullLogger<ReplaceDeckEffectHandler>.Instance,
                cardFactory);

            var effect = new Effect(new EffectConfig { Type = EffectType.ReplaceDeck }, player1Id, new List<ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetInfo>(), null, Guid.NewGuid(), null);
            var parameters = new ReplaceDeckEffectParams
            {
                PlayerScope = PlayerScope.Both,
                CardDefinitionIds = new List<int> { 3001, 3002, 3003 }
            };

            var applyResult = handler.Apply(effect, context, parameters);

            Assert.True(applyResult.Success);
            Assert.Equal(8, applyResult.GeneratedEvents.OfType<CardMovedEvent>().Count());

            CommitMoveEvents(applyResult.GeneratedEvents, context);

            Assert.Equal(new[] { 3001, 3002, 3003 }, gameState.Player1.Deck.Select(card => card.BaseDefinitionId).ToArray());
            Assert.Equal(new[] { 3001, 3002, 3003 }, gameState.Player2.Deck.Select(card => card.BaseDefinitionId).ToArray());
            Assert.All(gameState.Player1.Deck, card => Assert.Equal(player1Id, card.OwnerPlayerId));
            Assert.All(gameState.Player2.Deck, card => Assert.Equal(player2Id, card.OwnerPlayerId));
        }

        private static void SetTestGameDataProvider(params int[] definitionIds)
        {
            var definitions = definitionIds.Distinct().ToDictionary(
                id => id,
                id => new CardDefinition
                {
                    Id = id,
                    Name = $"Card-{id}",
                    Type = CardType.Follower,
                    Attack = 1,
                    Health = 1
                });

            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(It.IsAny<int>()))
                .Returns((int id) => definitions.TryGetValue(id, out var definition) ? definition : null);
            mockProvider.Setup(provider => provider.GetAbilityConfigs(It.IsAny<int>()))
                .Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }

        private static ServiceProvider BuildServiceProvider()
        {
            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseAttack);
            buffService.Setup(service => service.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseHealth);
            buffService.Setup(service => service.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseCost);
            buffService.Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, GameContext _) => card.BaseKeywords);
            buffService.Setup(service => service.GetEffectiveActionUsageLimit(It.IsAny<CardInstance>(), It.IsAny<CardActionUsageType>(), It.IsAny<GameContext>()))
                .Returns((CardInstance card, CardActionUsageType actionType, GameContext _) => card.GetBaseActionUsageLimit(actionType));
            buffService.Setup(service => service.RemoveAllBuffs(
                    It.IsAny<Guid>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Func<Buff, bool>>()))
                .Returns(new DomainOperationResult { Success = true });

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            return services.BuildServiceProvider();
        }

        private static GameState BuildGameState(Guid player1Id, Guid player2Id)
        {
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(manager => manager.GetAbilitiesForCard(It.IsAny<Guid>()))
                .Returns(new List<ShadowverseBeyond.Server.Domain.Abilities.Common.IAbility>());
            abilityManager.Setup(manager => manager.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<ShadowverseBeyond.Server.Domain.Abilities.Common.IAbility>());
            abilityManager.Setup(manager => manager.CreateAbilitiesForCard(
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<AbilityConfig>>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<GameState>()))
                .Returns(new List<Ability>());

            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            return new GameState("replace-deck-test", player1, player2, player1Id)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>()
            };
        }

        private static GameContext BuildContext(GameState gameState, Guid requestingPlayerId, IServiceProvider serviceProvider)
        {
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.ContextAccessor = accessor;
            GameServiceProvider.Logger = NullLogger.Instance;
            return new GameContext(serviceProvider, Mock.Of<IMapper>(), accessor, gameState, requestingPlayerId);
        }

        private static CardInstance AddDeckCard(PlayerState player, GameState gameState, int definitionId)
        {
            var card = new CardInstance(Guid.NewGuid(), definitionId, player.PlayerId)
            {
                CurrentZone = CardZone.Deck
            };
            player.Deck.Add(card);
            gameState.RegisterCardInstance(card);
            return card;
        }

        private static List<IGameEvent> CommitMoveEvents(IEnumerable<IGameEvent> generatedEvents, GameContext context)
        {
            var followUps = new List<IGameEvent>();
            foreach (var moveEvent in generatedEvents.OfType<CardMovedEvent>())
            {
                var commitResult = moveEvent.Commit(context);
                Assert.True(commitResult.Success);
                followUps.AddRange(commitResult.GeneratedEvents);
            }

            return followUps;
        }

        private sealed class TrackingCardFactory : ICardFactory
        {
            public CardInstance CreateAndRegisterNewCard(GameState gameState, int cardDefinitionId, Guid ownerPlayerId, CardZone initialZone)
            {
                var card = new CardInstance(Guid.NewGuid(), cardDefinitionId, ownerPlayerId)
                {
                    CurrentZone = initialZone
                };
                gameState.RegisterCardInstance(card);
                return card;
            }

            public CardInstance CreateAndRegisterCardCopy(GameState gameState, CardInstance originalCardToCopy, Guid newOwnerPlayerId, CardZone initialZone, bool exactCopy, CardInstance? copyInstance = null)
            {
                throw new NotSupportedException();
            }
        }
    }
}
