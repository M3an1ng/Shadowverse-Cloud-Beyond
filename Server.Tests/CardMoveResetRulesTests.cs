using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Domain.Constants;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class CardMoveResetRulesTests
    {
        private static void SetTestGameDataProvider(int definitionId)
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(definitionId)).Returns(new CardDefinition
            {
                Id = definitionId,
                Name = "TestCard",
                Type = CardType.Follower,
                Attack = 1,
                Health = 1
            });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(definitionId)).Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }

        private static ServiceProvider BuildServiceProvider(Mock<IBuffService> buffService)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            return services.BuildServiceProvider();
        }

        private static GameState BuildGameState(Guid player1Id, Guid player2Id, Mock<IAbilityManager> abilityManager)
        {
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            return new GameState("test-game", player1, player2, player1Id)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = new Mock<IBuffManager>().Object
            };
        }

        private static GameContext BuildContext(GameState gameState, Guid requestingPlayerId, IServiceProvider serviceProvider)
        {
            var mapper = new Mock<IMapper>().Object;
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            return new GameContext(serviceProvider, mapper, accessor, gameState, requestingPlayerId);
        }

        [Fact]
        public void CardMoved_PreserveTransition_DoesNotResetState()
        {
            const int definitionId = 1;
            SetTestGameDataProvider(definitionId);

            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(manager => manager.CreateAbilitiesForCard(
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<AbilityConfig>>(),
                    It.IsAny<Guid?>()))
                .Returns(new List<Ability>());

            var gameState = BuildGameState(player1Id, player2Id, abilityManager);

            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.RemoveAllBuffs(
                    It.IsAny<Guid>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Func<Buff, bool>>()))
                .Returns(new DomainOperationResult { Success = true });

            var serviceProvider = BuildServiceProvider(buffService);
            using var context = BuildContext(gameState, player1Id, serviceProvider);

            var card = new CardInstance(Guid.NewGuid(), definitionId, player1Id)
            {
                Damage = 3,
                IsEvolved = true,
                FieldTurn = 2,
                AttacksThisTurn = 1,
                MaxAttacksPerTurn = 2,
                CurrentZone = CardZone.Deck
            };
            card.AbilityState["act.lastUsedTurnNumber"] = 4;
            card.AbilityState["custom"] = 9;
            card.ApplySilence();
            gameState.Player1.Deck.Add(card);

            var evt = new CardMovedEvent(card, CardZone.Deck, CardZone.Hand);
            var result = evt.Commit(context);

            Assert.True(result.Success);
            Assert.Equal(CardZone.Hand, card.CurrentZone);
            Assert.Equal(3, card.Damage);
            Assert.True(card.IsEvolved);
            Assert.True(card.IsSilenced);
            Assert.Equal(2, card.FieldTurn);
            Assert.Equal(1, card.AttacksThisTurn);
            Assert.Equal(2, card.MaxAttacksPerTurn);
            Assert.True(card.AbilityState.ContainsKey("act.lastUsedTurnNumber"));
            Assert.Equal(9, card.AbilityState["custom"]);
            Assert.Contains(card, gameState.Player1.Hand);
        }

        [Fact]
        public void CardMoved_ResetTransition_ResetsState()
        {
            const int definitionId = 1;
            SetTestGameDataProvider(definitionId);

            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(manager => manager.CreateAbilitiesForCard(
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<AbilityConfig>>(),
                    It.IsAny<Guid?>()))
                .Returns(new List<Ability>());

            var gameState = BuildGameState(player1Id, player2Id, abilityManager);

            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.RemoveAllBuffs(
                    It.IsAny<Guid>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Func<Buff, bool>>()))
                .Returns(new DomainOperationResult { Success = true });

            var serviceProvider = BuildServiceProvider(buffService);
            using var context = BuildContext(gameState, player1Id, serviceProvider);

            var card = new CardInstance(Guid.NewGuid(), definitionId, player1Id)
            {
                Damage = 3,
                IsEvolved = true,
                FieldTurn = 2,
                AttacksThisTurn = 1,
                MaxAttacksPerTurn = 2,
                CurrentZone = CardZone.Field
            };
            card.AbilityState["act.lastUsedTurnNumber"] = 4;
            card.AbilityState["custom"] = 9;
            card.ApplySilence();
            gameState.Player1.Field.Add(card);

            var evt = new CardMovedEvent(card, CardZone.Field, CardZone.Graveyard);
            var result = evt.Commit(context);

            Assert.True(result.Success);
            Assert.Equal(CardZone.Graveyard, card.CurrentZone);
            Assert.Equal(0, card.Damage);
            Assert.False(card.IsEvolved);
            Assert.False(card.IsSilenced);
            Assert.Equal(0, card.FieldTurn);
            Assert.Equal(0, card.AttacksThisTurn);
            Assert.Equal(1, card.MaxAttacksPerTurn);
            Assert.Empty(card.AbilityState);
            Assert.Contains(card, gameState.Player1.Graveyard);
        }

        [Fact]
        public void Transform_UsesResetLevelCleanup_AndRebuildsNewDefinitionAbilities()
        {
            const int originalDefinitionId = 1;
            const int transformedDefinitionId = 2;

            var transformedAbilityConfigs = new List<AbilityConfig>
            {
                new AbilityConfig { Id = 2001, CardId = transformedDefinitionId, Name = "TransformAbility" }
            };

            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(originalDefinitionId)).Returns(new CardDefinition
            {
                Id = originalDefinitionId,
                Name = "OriginalCard",
                Type = CardType.Follower,
                Attack = 1,
                Health = 1
            });
            mockProvider.Setup(provider => provider.GetCardDefinition(transformedDefinitionId)).Returns(new CardDefinition
            {
                Id = transformedDefinitionId,
                Name = "TransformedCard",
                Type = CardType.Follower,
                Attack = 3,
                Health = 4
            });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(originalDefinitionId)).Returns(new List<AbilityConfig>());
            mockProvider.Setup(provider => provider.GetAbilityConfigs(transformedDefinitionId)).Returns(transformedAbilityConfigs);
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);

            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();

            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(manager => manager.CreateAbilitiesForCard(
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<AbilityConfig>>(),
                    It.IsAny<Guid?>()))
                .Returns(new List<Ability>());

            var gameState = BuildGameState(player1Id, player2Id, abilityManager);

            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.RemoveAllBuffs(
                    It.IsAny<Guid>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Func<Buff, bool>>()))
                .Returns(new DomainOperationResult { Success = true });

            var serviceProvider = BuildServiceProvider(buffService);
            using var context = BuildContext(gameState, player1Id, serviceProvider);

            var card = new CardInstance(Guid.NewGuid(), originalDefinitionId, player1Id)
            {
                Damage = 5,
                IsEvolved = true,
                FieldTurn = 2,
                AttacksThisTurn = 1,
                MaxAttacksPerTurn = 2,
                ActsThisTurn = 1,
                MaxActsPerTurn = 2,
                CurrentZone = CardZone.Field
            };
            card.AbilityState["act.lastUsedTurnNumber"] = 4;
            card.AbilityState["custom"] = 9;
            card.SetCounter("custom.counter", 7);
            card.ApplySilence();

            var transformedDefinition = mockProvider.Object.GetCardDefinition(transformedDefinitionId)!;
            var result = card.Transform(transformedDefinition, context);

            Assert.True(result.Success);
            Assert.Equal(transformedDefinitionId, card.BaseDefinitionId);
            Assert.Equal(0, card.Damage);
            Assert.False(card.IsEvolved);
            Assert.False(card.IsSilenced);
            Assert.Equal(0, card.FieldTurn);
            Assert.Equal(0, card.AttacksThisTurn);
            Assert.Equal(1, card.MaxAttacksPerTurn);
            Assert.Equal(0, card.ActsThisTurn);
            Assert.Equal(1, card.MaxActsPerTurn);
            Assert.Empty(card.AbilityState);
            Assert.Equal(0, card.GetCounter("custom.counter"));
            Assert.Contains(result.GeneratedEvents, gameEvent => gameEvent is TransformEvent);

            buffService.Verify(service => service.RemoveAllBuffs(
                    card.InstanceId,
                    It.IsAny<GameContext>(),
                    It.IsAny<Func<Buff, bool>>()),
                Times.Once);
            abilityManager.Verify(manager => manager.UnregisterCardAbilities(card.InstanceId), Times.Once);
            abilityManager.Verify(manager => manager.CreateAbilitiesForCard(
                    card,
                    It.Is<List<AbilityConfig>>(configs => configs.Count == transformedAbilityConfigs.Count),
                    It.IsAny<Guid?>()),
                Times.Once);
        }

        [Fact]
        public void CardMoved_ToHand_WhenHandFull_BurnsCardAndEmitsCardBurnedEvent()
        {
            const int definitionId = 1;
            SetTestGameDataProvider(definitionId);

            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(manager => manager.CreateAbilitiesForCard(
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<AbilityConfig>>(),
                    It.IsAny<Guid?>()))
                .Returns(new List<Ability>());

            var gameState = BuildGameState(player1Id, player2Id, abilityManager);

            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.RemoveAllBuffs(
                    It.IsAny<Guid>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Func<Buff, bool>>()))
                .Returns(new DomainOperationResult { Success = true });

            var serviceProvider = BuildServiceProvider(buffService);
            using var context = BuildContext(gameState, player1Id, serviceProvider);

            for (var i = 0; i < GameConstants.Hand.MaxHandSize; i++)
            {
                gameState.Player1.Hand.Add(new CardInstance(Guid.NewGuid(), definitionId, player1Id)
                {
                    CurrentZone = CardZone.Hand
                });
            }

            var card = new CardInstance(Guid.NewGuid(), definitionId, player1Id)
            {
                CurrentZone = CardZone.Deck
            };
            gameState.Player1.Deck.Add(card);

            var evt = new CardMovedEvent(card, CardZone.Deck, CardZone.Hand, CardMoveReason.Drawn);
            var result = evt.Commit(context);

            Assert.True(result.Success);
            Assert.Equal(CardZone.Graveyard, card.CurrentZone);
            Assert.DoesNotContain(card, gameState.Player1.Hand);
            Assert.DoesNotContain(card, gameState.Player1.Deck);
            Assert.Contains(card, gameState.Player1.Graveyard);
            Assert.Single(result.GeneratedEvents.OfType<CardBurnedEvent>());
        }

        [Fact]
        public void CardMoved_StagingToHand_WhenHandFull_BurnsCardAndEmitsCardBurnedEvent()
        {
            const int definitionId = 1;
            SetTestGameDataProvider(definitionId);

            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(manager => manager.CreateAbilitiesForCard(
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<AbilityConfig>>(),
                    It.IsAny<Guid?>()))
                .Returns(new List<Ability>());

            var gameState = BuildGameState(player1Id, player2Id, abilityManager);

            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.RemoveAllBuffs(
                    It.IsAny<Guid>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Func<Buff, bool>>()))
                .Returns(new DomainOperationResult { Success = true });

            var serviceProvider = BuildServiceProvider(buffService);
            using var context = BuildContext(gameState, player1Id, serviceProvider);

            for (var i = 0; i < GameConstants.Hand.MaxHandSize; i++)
            {
                gameState.Player1.Hand.Add(new CardInstance(Guid.NewGuid(), definitionId, player1Id)
                {
                    CurrentZone = CardZone.Hand
                });
            }

            var card = new CardInstance(Guid.NewGuid(), definitionId, player1Id)
            {
                CurrentZone = CardZone.Staging
            };

            var evt = new CardMovedEvent(card, CardZone.Staging, CardZone.Hand, CardMoveReason.AddedToHand);
            var result = evt.Commit(context);

            Assert.True(result.Success);
            Assert.Equal(CardZone.Graveyard, card.CurrentZone);
            Assert.DoesNotContain(card, gameState.Player1.Hand);
            Assert.Contains(card, gameState.Player1.Graveyard);
            Assert.Single(result.GeneratedEvents.OfType<CardBurnedEvent>());
        }

        [Fact]
        public void AddCardsToHand_WhenNearLimit_EmitsOverflowMoveAsBurned()
        {
            const int definitionId = 1;
            SetTestGameDataProvider(definitionId);

            var player = new PlayerState(Guid.NewGuid());
            for (var i = 0; i < GameConstants.Hand.MaxHandSize - 1; i++)
            {
                player.Hand.Add(new CardInstance(Guid.NewGuid(), definitionId, player.PlayerId)
                {
                    CurrentZone = CardZone.Hand
                });
            }

            var first = new CardInstance(Guid.NewGuid(), definitionId, player.PlayerId) { CurrentZone = CardZone.Staging };
            var second = new CardInstance(Guid.NewGuid(), definitionId, player.PlayerId) { CurrentZone = CardZone.Staging };

            var result = player.AddCardsToHand(new List<CardInstance> { first, second });
            var movedEvents = result.GeneratedEvents.OfType<CardMovedEvent>().ToList();

            Assert.True(result.Success);
            Assert.Equal(2, movedEvents.Count);
            Assert.Equal(CardZone.Hand, movedEvents[0].ToZone);
            Assert.Equal(CardMoveReason.AddedToHand, movedEvents[0].MoveReason);
            Assert.Equal(CardZone.Graveyard, movedEvents[1].ToZone);
            Assert.Equal(CardMoveReason.Burned, movedEvents[1].MoveReason);
        }

        [Fact]
        public void DrawCards_WhenNearLimit_EmitsOverflowMoveAsBurned()
        {
            const int definitionId = 1;
            SetTestGameDataProvider(definitionId);

            var player = new PlayerState(Guid.NewGuid());
            for (var i = 0; i < GameConstants.Hand.MaxHandSize - 1; i++)
            {
                player.Hand.Add(new CardInstance(Guid.NewGuid(), definitionId, player.PlayerId)
                {
                    CurrentZone = CardZone.Hand
                });
            }

            var first = new CardInstance(Guid.NewGuid(), definitionId, player.PlayerId) { CurrentZone = CardZone.Deck };
            var second = new CardInstance(Guid.NewGuid(), definitionId, player.PlayerId) { CurrentZone = CardZone.Deck };
            player.Deck.Add(first);
            player.Deck.Add(second);

            var result = player.DrawCards(new List<Guid> { first.InstanceId, second.InstanceId });
            var movedEvents = result.GeneratedEvents.OfType<CardMovedEvent>().ToList();

            Assert.True(result.Success);
            Assert.Equal(2, movedEvents.Count);
            Assert.Equal(CardZone.Hand, movedEvents[0].ToZone);
            Assert.Equal(CardMoveReason.Drawn, movedEvents[0].MoveReason);
            Assert.Equal(CardZone.Graveyard, movedEvents[1].ToZone);
            Assert.Equal(CardMoveReason.Burned, movedEvents[1].MoveReason);
            Assert.Empty(result.GeneratedEvents.OfType<CardBurnedEvent>());
        }
    }
}
