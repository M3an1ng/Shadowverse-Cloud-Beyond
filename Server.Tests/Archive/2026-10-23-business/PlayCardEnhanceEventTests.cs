using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class PlayCardEnhanceEventTests
    {
        [Fact]
        public void PlayCardRequestedEvent_emits_only_play_event_for_enhance_request()
        {
            const int spellDefinitionId = 5001;
            SetTestGameDataProvider(spellDefinitionId);

            using var context = CreateContext(out var player, out _, out var abilityManager);
            player.CurrentPlayPoints = 6;
            player.MaxPlayPoints = 6;

            var card = new CardInstance(Guid.NewGuid(), spellDefinitionId, player.PlayerId)
            {
                CurrentZone = CardZone.Hand
            };
            var enhanceAbility = CreateEnhanceAbility(card, 6);
            card.SetAbilities(new[] { enhanceAbility });
            player.Hand.Add(card);

            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(card.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { enhanceAbility });
            abilityManager
                .Setup(manager => manager.GetAbilityByRuntimeId(enhanceAbility.RuntimeId))
                .Returns(enhanceAbility);

            var request = new PlayCardRequestedEvent(
                player.PlayerId,
                new PlayCardActionDto
                {
                    CardUid = card.InstanceId,
                    PlayMode = PlayMode.Enhance,
                    AbilityRuntimeId = enhanceAbility.RuntimeId,
                    PaidCost = 6
                });

            var result = request.Commit(context);

            Assert.True(result.Success, result.ErrorMessage);
            var playEvent = Assert.Single(result.GeneratedEvents.OfType<PlayCardEvent>());
            Assert.Equal(PlayMode.Enhance, playEvent.PlayMode);
            Assert.Equal(6, playEvent.PaidCost);
            Assert.Equal(enhanceAbility.RuntimeId, playEvent.TriggeredAbilityRuntimeId);
            Assert.DoesNotContain(result.GeneratedEvents, evt => evt is EnhanceEvent);
        }

        [Fact]
        public void PlayCardEvent_commit_emits_enhance_signal_after_state_changes()
        {
            const int spellDefinitionId = 5002;
            SetTestGameDataProvider(spellDefinitionId);

            using var context = CreateContext(out var player, out _, out _);
            player.CurrentPlayPoints = 6;
            player.MaxPlayPoints = 6;

            var card = new CardInstance(Guid.NewGuid(), spellDefinitionId, player.PlayerId)
            {
                CurrentZone = CardZone.Hand
            };
            player.Hand.Add(card);

            var triggeredAbilityRuntimeId = Guid.NewGuid();
            var playEvent = new PlayCardEvent(
                player,
                card,
                PlayMode.Enhance,
                paidCost: 6,
                triggeredAbilityRuntimeId: triggeredAbilityRuntimeId);

            var result = playEvent.Commit(context);

            Assert.True(result.Success, result.ErrorMessage);
            var enhanceEvent = Assert.Single(result.GeneratedEvents.OfType<EnhanceEvent>());
            Assert.Same(enhanceEvent, result.GeneratedEvents.Last());
            Assert.Equal(6, enhanceEvent.PaidCost);
            Assert.Equal(triggeredAbilityRuntimeId, enhanceEvent.TriggeredAbilityRuntimeId);
        }

        [Fact]
        public void PlayCardRequestedEvent_prepares_alternate_play_event_with_replacement_definition()
        {
            const int baseDefinitionId = 5101;
            const int accelerateDefinitionId = 5102;
            SetTestGameDataProvider(
                new CardDefinition { Id = baseDefinitionId, Name = "LargeFollower", Type = CardType.Follower, Cost = 7 },
                new CardDefinition { Id = accelerateDefinitionId, Name = "SmallSpell", Type = CardType.Spell, Cost = 2 });

            using var context = CreateContext(out var player, out _, out var abilityManager);
            player.CurrentPlayPoints = 2;
            player.MaxPlayPoints = 2;

            var card = new CardInstance(Guid.NewGuid(), baseDefinitionId, player.PlayerId)
            {
                CurrentZone = CardZone.Hand
            };
            var accelerateAbility = CreateAlternatePlayAbility(card, AbilityKeyword.Accelerate, 2, accelerateDefinitionId);
            card.SetAbilities(new[] { accelerateAbility });
            player.Hand.Add(card);

            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(card.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { accelerateAbility });
            abilityManager
                .Setup(manager => manager.GetAbilityByRuntimeId(accelerateAbility.RuntimeId))
                .Returns(accelerateAbility);

            var request = new PlayCardRequestedEvent(
                player.PlayerId,
                new PlayCardActionDto
                {
                    CardUid = card.InstanceId
                });

            var result = request.Commit(context);

            Assert.True(result.Success, result.ErrorMessage);
            var playEvent = Assert.Single(result.GeneratedEvents.OfType<PlayCardEvent>());
            Assert.Equal(PlayMode.Accelerate, playEvent.PlayMode);
            Assert.Equal(2, playEvent.PaidCost);
            Assert.Equal(accelerateAbility.RuntimeId, playEvent.TriggeredAbilityRuntimeId);
            Assert.Equal(accelerateDefinitionId, playEvent.AlternateCardDefinitionId);
            Assert.DoesNotContain(result.GeneratedEvents, evt => evt is AccelerateEvent);
        }

        [Fact]
        public void PlayCardRequestedEvent_rejects_alternate_ability_that_does_not_match_priority()
        {
            const int baseDefinitionId = 5111;
            const int crystalizeDefinitionId = 5112;
            const int accelerateDefinitionId = 5113;
            SetTestGameDataProvider(
                new CardDefinition { Id = baseDefinitionId, Name = "LargeFollower", Type = CardType.Follower, Cost = 7 },
                new CardDefinition { Id = crystalizeDefinitionId, Name = "SmallAmulet", Type = CardType.Amulet, Cost = 3 },
                new CardDefinition { Id = accelerateDefinitionId, Name = "SmallSpell", Type = CardType.Spell, Cost = 3 });

            using var context = CreateContext(out var player, out _, out var abilityManager);
            player.CurrentPlayPoints = 3;
            player.MaxPlayPoints = 3;

            var card = new CardInstance(Guid.NewGuid(), baseDefinitionId, player.PlayerId)
            {
                CurrentZone = CardZone.Hand
            };
            var earlierCrystalize = CreateAlternatePlayAbility(card, AbilityKeyword.Crystalize, 3, crystalizeDefinitionId);
            var laterAccelerate = CreateAlternatePlayAbility(card, AbilityKeyword.Accelerate, 3, accelerateDefinitionId);
            card.SetAbilities(new[] { earlierCrystalize, laterAccelerate });
            player.Hand.Add(card);

            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(card.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { earlierCrystalize, laterAccelerate });
            abilityManager
                .Setup(manager => manager.GetAbilityByRuntimeId(earlierCrystalize.RuntimeId))
                .Returns(earlierCrystalize);
            abilityManager
                .Setup(manager => manager.GetAbilityByRuntimeId(laterAccelerate.RuntimeId))
                .Returns(laterAccelerate);

            var request = new PlayCardRequestedEvent(
                player.PlayerId,
                new PlayCardActionDto
                {
                    CardUid = card.InstanceId,
                    PlayMode = PlayMode.Accelerate,
                    AbilityRuntimeId = laterAccelerate.RuntimeId,
                    PaidCost = 3
                });

            var result = request.Commit(context);

            Assert.False(result.Success);
            Assert.Contains("priority", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.GeneratedEvents);
        }

        [Fact]
        public void PlayCardEvent_commit_transforms_to_alternate_definition_before_execution_and_emits_signal()
        {
            const int baseDefinitionId = 5201;
            const int accelerateDefinitionId = 5202;
            SetTestGameDataProvider(
                new CardDefinition { Id = baseDefinitionId, Name = "LargeFollower", Type = CardType.Follower, Cost = 7 },
                new CardDefinition { Id = accelerateDefinitionId, Name = "SmallSpell", Type = CardType.Spell, Cost = 2 });

            using var context = CreateContext(out var player, out _, out var abilityManager);
            player.CurrentPlayPoints = 2;
            player.MaxPlayPoints = 2;

            var card = new CardInstance(Guid.NewGuid(), baseDefinitionId, player.PlayerId)
            {
                CurrentZone = CardZone.Hand
            };
            player.Hand.Add(card);

            abilityManager
                .Setup(manager => manager.GetAbilitiesForCard(card.InstanceId))
                .Returns(new List<IAbility>());
            abilityManager
                .Setup(manager => manager.CreateAbilitiesForCard(
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<AbilityConfig>>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<GameState>()))
                .Returns(new List<Ability>());

            var triggeredAbilityRuntimeId = Guid.NewGuid();
            var playEvent = new PlayCardEvent(
                player,
                card,
                PlayMode.Accelerate,
                paidCost: 2,
                triggeredAbilityRuntimeId: triggeredAbilityRuntimeId,
                alternateCardDefinitionId: accelerateDefinitionId);

            var result = playEvent.Commit(context);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(accelerateDefinitionId, card.CurrentDefinitionId);
            Assert.Equal(CardType.Spell, card.CardType);
            Assert.Contains(result.GeneratedEvents.OfType<CardMovedEvent>(), evt => evt.ToZone == CardZone.Playing);
            var accelerateEvent = Assert.Single(result.GeneratedEvents.OfType<AccelerateEvent>());
            Assert.Same(accelerateEvent, result.GeneratedEvents.Last());
            Assert.Equal(2, accelerateEvent.PaidCost);
            Assert.Equal(triggeredAbilityRuntimeId, accelerateEvent.TriggeredAbilityRuntimeId);
        }

        [Theory]
        [InlineData(PlayMode.Accelerate, 5302, CardType.Follower, "Spell")]
        [InlineData(PlayMode.Crystalize, 5303, CardType.Spell, "Amulet")]
        public void PlayCardEvent_commit_rejects_invalid_alternate_replacement_type(
            PlayMode playMode,
            int replacementDefinitionId,
            CardType replacementType,
            string expectedMessage)
        {
            const int baseDefinitionId = 5301;
            SetTestGameDataProvider(
                new CardDefinition { Id = baseDefinitionId, Name = "LargeFollower", Type = CardType.Follower, Cost = 7 },
                new CardDefinition { Id = replacementDefinitionId, Name = "WrongReplacement", Type = replacementType, Cost = 2 });

            using var context = CreateContext(out var player, out _, out _);
            player.CurrentPlayPoints = 2;
            player.MaxPlayPoints = 2;

            var card = new CardInstance(Guid.NewGuid(), baseDefinitionId, player.PlayerId)
            {
                CurrentZone = CardZone.Hand
            };
            player.Hand.Add(card);

            var playEvent = new PlayCardEvent(
                player,
                card,
                playMode,
                paidCost: 2,
                triggeredAbilityRuntimeId: Guid.NewGuid(),
                alternateCardDefinitionId: replacementDefinitionId);

            var result = playEvent.Commit(context);

            Assert.False(result.Success);
            Assert.Contains(expectedMessage, result.ErrorMessage);
            Assert.Equal(baseDefinitionId, card.CurrentDefinitionId);
            Assert.Equal(CardZone.Hand, card.CurrentZone);
            Assert.Equal(2, player.CurrentPlayPoints);
            Assert.DoesNotContain(result.GeneratedEvents, evt => evt is AccelerateEvent or CrystalizeEvent);
        }

        [Fact]
        public void PlayCardEvent_commit_rejects_missing_alternate_replacement_definition()
        {
            const int baseDefinitionId = 5311;
            const int missingDefinitionId = 5312;
            SetTestGameDataProvider(new CardDefinition
            {
                Id = baseDefinitionId,
                Name = "LargeFollower",
                Type = CardType.Follower,
                Cost = 7
            });

            using var context = CreateContext(out var player, out _, out _);
            player.CurrentPlayPoints = 2;
            player.MaxPlayPoints = 2;

            var card = new CardInstance(Guid.NewGuid(), baseDefinitionId, player.PlayerId)
            {
                CurrentZone = CardZone.Hand
            };
            player.Hand.Add(card);

            var playEvent = new PlayCardEvent(
                player,
                card,
                PlayMode.Accelerate,
                paidCost: 2,
                triggeredAbilityRuntimeId: Guid.NewGuid(),
                alternateCardDefinitionId: missingDefinitionId);

            var result = playEvent.Commit(context);

            Assert.False(result.Success);
            Assert.Contains(missingDefinitionId.ToString(), result.ErrorMessage);
            Assert.Equal(baseDefinitionId, card.CurrentDefinitionId);
            Assert.Equal(CardZone.Hand, card.CurrentZone);
            Assert.Equal(2, player.CurrentPlayPoints);
            Assert.Empty(result.GeneratedEvents);
        }

        [Fact]
        public void PlayCardEvent_commit_base_play_ignores_alternate_replacement_validation()
        {
            const int spellDefinitionId = 5321;
            SetTestGameDataProvider(spellDefinitionId);

            using var context = CreateContext(out var player, out _, out _);
            player.CurrentPlayPoints = 2;
            player.MaxPlayPoints = 2;

            var card = new CardInstance(Guid.NewGuid(), spellDefinitionId, player.PlayerId)
            {
                CurrentZone = CardZone.Hand
            };
            player.Hand.Add(card);

            var playEvent = new PlayCardEvent(player, card, PlayMode.Base, paidCost: 2);

            var result = playEvent.Commit(context);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(spellDefinitionId, card.CurrentDefinitionId);
            Assert.Contains(result.GeneratedEvents.OfType<CardMovedEvent>(), evt => evt.ToZone == CardZone.Playing);
        }

        private static Ability CreateEnhanceAbility(CardInstance sourceCard, int enhanceCost)
        {
            var config = new AbilityConfig
            {
                Id = 1,
                CardId = sourceCard.BaseDefinitionId,
                Name = $"Enhance {enhanceCost}",
                Type = AbilityType.Triggered,
                Trigger = TriggerConfig.OnEvent(EventType.Enhance),
                AbilityKeywords = new List<AbilityKeywordConfig>
                {
                    new() { Keyword = AbilityKeyword.Enhance, Value = enhanceCost }
                }
            };

            var conditionFactory = new Mock<IConditionFactory>();
            var targetFactory = new Mock<ITargetFactory>();
            var targetSelectionService = new TargetSelectionService(
                new Mock<ILogger<TargetSelectionService>>().Object,
                new Mock<IQueryExecutor>().Object,
                new Mock<IPendingActionService>().Object);

            return new Ability(config, conditionFactory.Object, targetFactory.Object, targetSelectionService, sourceCard);
        }

        private static Ability CreateAlternatePlayAbility(
            CardInstance sourceCard,
            AbilityKeyword keyword,
            int cost,
            int playAsCardDefinitionId)
        {
            var config = new AbilityConfig
            {
                Id = 2,
                CardId = sourceCard.BaseDefinitionId,
                Name = $"{keyword} {cost}",
                Type = AbilityType.Triggered,
                Trigger = TriggerConfig.OnEvent(keyword == AbilityKeyword.Accelerate
                    ? EventType.Accelerate
                    : EventType.Crystalize),
                AbilityKeywords = new List<AbilityKeywordConfig>
                {
                    new() { Keyword = keyword, Value = cost }
                },
                AlternatePlay = new AlternatePlayConfig
                {
                    PlayAsCardDefinitionId = playAsCardDefinitionId
                }
            };

            var conditionFactory = new Mock<IConditionFactory>();
            var targetFactory = new Mock<ITargetFactory>();
            var targetSelectionService = new TargetSelectionService(
                new Mock<ILogger<TargetSelectionService>>().Object,
                new Mock<IQueryExecutor>().Object,
                new Mock<IPendingActionService>().Object);

            return new Ability(config, conditionFactory.Object, targetFactory.Object, targetSelectionService, sourceCard);
        }

        private static GameContext CreateContext(
            out PlayerState player,
            out PlayerState opponent,
            out Mock<IAbilityManager> abilityManager)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var accessor = new GameContextAccessor();
            services.AddSingleton<IGameContextAccessor>(accessor);

            var buffService = new Mock<IBuffService>();
            buffService
                .Setup(service => service.RemoveAllBuffs(It.IsAny<Guid>(), It.IsAny<GameContext>(), null))
                .Returns(new DomainOperationResult { Success = true });
            buffService
                .Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns(Array.Empty<CardKeyword>().ToList());
            buffService
                .Setup(service => service.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns<CardInstance, GameContext>((card, _) => card.BaseCost);
            buffService
                .Setup(service => service.GetEffectiveAbilityKeywordValue(
                    It.IsAny<CardInstance>(),
                    It.IsAny<AbilityKeyword>(),
                    It.IsAny<int>(),
                    It.IsAny<GameContext>()))
                .Returns<CardInstance, AbilityKeyword, int, GameContext>((_, _, value, _) => value);
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton(_ => GameDataProvider.Instance);

            var serviceProvider = services.BuildServiceProvider();
            var mapper = new MapperConfiguration(_ => { }).CreateMapper();

            player = new PlayerState(Guid.NewGuid())
            {
                TurnNumber = 1
            };
            opponent = new PlayerState(Guid.NewGuid())
            {
                TurnNumber = 1
            };

            abilityManager = new Mock<IAbilityManager>();
            abilityManager
                .Setup(manager => manager.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());
            abilityManager
                .Setup(manager => manager.GetAbilityByRuntimeId(It.IsAny<Guid>()))
                .Returns((IAbility?)null);

            var gameState = new GameState("play-enhance-test", player, opponent, player.PlayerId)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = Mock.Of<IBuffManager>(),
                Phase = GamePhase.Main,
                CurrentPlayerId = player.PlayerId
            };

            GameServiceProvider.ContextAccessor = accessor;
            return new GameContext(serviceProvider, mapper, accessor, gameState, player.PlayerId);
        }

        private static void SetTestGameDataProvider(int spellDefinitionId)
        {
            SetTestGameDataProvider(new CardDefinition
            {
                Id = spellDefinitionId,
                Name = "TestSpell",
                Type = CardType.Spell,
                Cost = 2
            });
        }

        private static void SetTestGameDataProvider(params CardDefinition[] definitions)
        {
            var definitionsById = definitions.ToDictionary(definition => definition.Id);
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider
                .Setup(provider => provider.GetCardDefinition(It.IsAny<int>()))
                .Returns<int>(cardId => definitionsById.TryGetValue(cardId, out var definition) ? definition : null);
            mockProvider.Setup(provider => provider.GetAbilityConfigs(It.IsAny<int>())).Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }
    }
}
