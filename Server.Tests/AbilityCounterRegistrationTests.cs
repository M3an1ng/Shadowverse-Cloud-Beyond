using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Entities;
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
    }
}
