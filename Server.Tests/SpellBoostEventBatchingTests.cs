using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class SpellBoostEventBatchingTests
    {
        [Fact]
        public void ExecutePlayCard_GeneratesSingleSpellBoostEventWithAllTargets()
        {
            const int spellDefinitionId = 100;
            const int boostDefinitionId = 200;
            const int nonBoostDefinitionId = 300;

            SetTestGameDataProvider(spellDefinitionId, boostDefinitionId, nonBoostDefinitionId);

            var playerId = Guid.NewGuid();
            var player = new PlayerState(playerId);

            var spellCard = new CardInstance(Guid.NewGuid(), spellDefinitionId, playerId)
            {
                CurrentZone = CardZone.Hand
            };

            var boostCardA = new CardInstance(Guid.NewGuid(), boostDefinitionId, playerId)
            {
                CurrentZone = CardZone.Hand
            };
            boostCardA.SetAbilities(new[] { CreateSpellBoostAbility(boostCardA) });

            var boostCardB = new CardInstance(Guid.NewGuid(), boostDefinitionId, playerId)
            {
                CurrentZone = CardZone.Hand
            };
            boostCardB.SetAbilities(new[] { CreateSpellBoostAbility(boostCardB) });

            var nonBoostCard = new CardInstance(Guid.NewGuid(), nonBoostDefinitionId, playerId)
            {
                CurrentZone = CardZone.Hand
            };

            player.Hand.AddRange(new[] { spellCard, boostCardA, boostCardB, nonBoostCard });

            var result = player.ExecutePlayCard(spellCard, Guid.NewGuid(), paidCost: 0);

            var spellBoostEvents = result.GeneratedEvents.OfType<SpellBoostEvent>().ToList();
            Assert.Single(spellBoostEvents);

            var spellBoostEvent = spellBoostEvents[0];
            Assert.Equal(2, spellBoostEvent.BoostedCards.Count);
            Assert.Contains(boostCardA, spellBoostEvent.BoostedCards);
            Assert.Contains(boostCardB, spellBoostEvent.BoostedCards);
            Assert.DoesNotContain(nonBoostCard, spellBoostEvent.BoostedCards);
        }

        private static Ability CreateSpellBoostAbility(CardInstance sourceCard)
        {
            var config = new AbilityConfig
            {
                Id = 1,
                CardId = sourceCard.BaseDefinitionId,
                Name = "SpellBoost",
                Type = AbilityType.Triggered,
                Trigger = TriggerConfig.OnEvent(EventType.SpellBoost),
                AbilityKeywords = new List<AbilityKeywordConfig>
                {
                    new AbilityKeywordConfig { Keyword = AbilityKeyword.SpellBoost }
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

        private static void SetTestGameDataProvider(int spellDefinitionId, int boostDefinitionId, int nonBoostDefinitionId)
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);

            mockProvider.Setup(provider => provider.GetCardDefinition(spellDefinitionId)).Returns(new CardDefinition
            {
                Id = spellDefinitionId,
                Name = "TestSpell",
                Type = CardType.Spell,
                Cost = 1
            });

            mockProvider.Setup(provider => provider.GetCardDefinition(boostDefinitionId)).Returns(new CardDefinition
            {
                Id = boostDefinitionId,
                Name = "TestBoostFollower",
                Type = CardType.Follower,
                Cost = 2,
                Attack = 1,
                Health = 1
            });

            mockProvider.Setup(provider => provider.GetCardDefinition(nonBoostDefinitionId)).Returns(new CardDefinition
            {
                Id = nonBoostDefinitionId,
                Name = "TestFollower",
                Type = CardType.Follower,
                Cost = 2,
                Attack = 1,
                Health = 1
            });

            mockProvider.Setup(provider => provider.GetAbilityConfigs(It.IsAny<int>())).Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }
    }
}
