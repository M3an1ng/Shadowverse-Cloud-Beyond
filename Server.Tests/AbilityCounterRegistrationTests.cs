using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class AbilityCounterRegistrationTests
    {
        [Fact]
        public void CreateAbilitiesForCard_InitializesMissingCounters()
        {
            var card = new CardInstance(System.Guid.NewGuid(), 1, System.Guid.NewGuid());
            var abilityFactory = new Mock<IAbilityFactory>();
            abilityFactory
                .Setup(factory => factory.CreateAbilities(
                    It.IsAny<CardInstance>(),
                    It.IsAny<IEnumerable<AbilityConfig>>(),
                    It.IsAny<System.Guid?>()))
                .Returns(new List<Ability>());

            var abilityManager = new AbilityManager(
                NullLogger<AbilityManager>.Instance,
                abilityFactory.Object,
                new List<IAbility>());

            var abilityConfigs = new List<AbilityConfig>
            {
                new AbilityConfig
                {
                    CounterRegistrations = new List<CounterRegistrationConfig>
                    {
                        new CounterRegistrationConfig { Name = "combo", InitialValue = 2 }
                    }
                }
            };

            Assert.False(card.Counters.ContainsKey("combo"));

            abilityManager.CreateAbilitiesForCard(card, abilityConfigs);

            Assert.True(card.Counters.ContainsKey("combo"));
            Assert.Equal(2, card.GetCounter("combo"));
        }

        [Fact]
        public void CreateAbilitiesForCard_SkipsExistingCounters()
        {
            var card = new CardInstance(System.Guid.NewGuid(), 1, System.Guid.NewGuid());
            card.SetCounter("combo", 1);

            var abilityFactory = new Mock<IAbilityFactory>();
            abilityFactory
                .Setup(factory => factory.CreateAbilities(
                    It.IsAny<CardInstance>(),
                    It.IsAny<IEnumerable<AbilityConfig>>(),
                    It.IsAny<System.Guid?>()))
                .Returns(new List<Ability>());

            var abilityManager = new AbilityManager(
                NullLogger<AbilityManager>.Instance,
                abilityFactory.Object,
                new List<IAbility>());

            var abilityConfigs = new List<AbilityConfig>
            {
                new AbilityConfig
                {
                    CounterRegistrations = new List<CounterRegistrationConfig>
                    {
                        new CounterRegistrationConfig { Name = "combo", InitialValue = 5 }
                    }
                }
            };

            abilityManager.CreateAbilitiesForCard(card, abilityConfigs);

            Assert.Equal(1, card.GetCounter("combo"));
        }

        [Fact]
        public void CardInstanceMapping_BuildsCounterSummaryEntriesInRegistrationOrder()
        {
            var card = new CardInstance(System.Guid.NewGuid(), 1, System.Guid.NewGuid());
            var abilityFactory = new Mock<IAbilityFactory>();
            abilityFactory
                .Setup(factory => factory.CreateAbilities(
                    It.IsAny<CardInstance>(),
                    It.IsAny<IEnumerable<AbilityConfig>>(),
                    It.IsAny<System.Guid?>()))
                .Returns<CardInstance, IEnumerable<AbilityConfig>, System.Guid?>((sourceCard, configs, _) =>
                {
                    var abilities = new List<Ability>();
                    var targetSelectionService = new TargetSelectionService(
                        new NullLogger<TargetSelectionService>(),
                        Mock.Of<IQueryExecutor>(),
                        Mock.Of<IPendingActionService>());

                    foreach (var config in configs)
                    {
                        abilities.Add(new Ability(
                            config,
                            Mock.Of<IConditionFactory>(),
                            Mock.Of<ITargetFactory>(),
                            targetSelectionService,
                            sourceCard));
                    }

                    return abilities;
                });

            var abilityManager = new AbilityManager(
                NullLogger<AbilityManager>.Instance,
                abilityFactory.Object,
                new List<IAbility>());

            var abilityConfigs = new List<AbilityConfig>
            {
                new AbilityConfig
                {
                    CounterRegistrations = new List<CounterRegistrationConfig>
                    {
                        new CounterRegistrationConfig { Name = "water", DisplayName = "水", InitialValue = 1 },
                        new CounterRegistrationConfig { Name = "amber", DisplayName = "琥珀", InitialValue = 2 }
                    }
                }
            };

            abilityManager.CreateAbilitiesForCard(card, abilityConfigs);

            var mapper = new MapperConfiguration(cfg => cfg.AddProfile(new CardInstanceProfile())).CreateMapper();
            var dto = mapper.Map<CardInstanceDto>(card);

            Assert.Collection(
                dto.CounterSummaryEntries,
                entry =>
                {
                    Assert.Equal("water", entry.Key);
                    Assert.Equal("水", entry.Label);
                    Assert.Equal(1, entry.Value);
                },
                entry =>
                {
                    Assert.Equal("amber", entry.Key);
                    Assert.Equal("琥珀", entry.Label);
                    Assert.Equal(2, entry.Value);
                });
        }
    }
}
