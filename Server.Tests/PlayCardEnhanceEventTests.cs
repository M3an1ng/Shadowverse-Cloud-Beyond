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
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(spellDefinitionId)).Returns(new CardDefinition
            {
                Id = spellDefinitionId,
                Name = "TestSpell",
                Type = CardType.Spell,
                Cost = 2
            });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(It.IsAny<int>())).Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }
    }
}
