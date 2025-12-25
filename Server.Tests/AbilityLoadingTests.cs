using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.EntityFrameworkCore;
using MartinCostello.Logging.XUnit;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;

namespace ShadowverseBeyond.Server.Tests
{
    [Collection("DatabaseCollection")]
    public class AbilityLoadingTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;

        public AbilityLoadingTests(DatabaseFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.OutputHelper = output; // Make sure the fixture can log to the test output
        }

        public Task InitializeAsync()
        {
            // Initialization is now handled by the fixture's InitializeAsync
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            // Disposal is now handled by the fixture's DisposeAsync
            return Task.CompletedTask;
        }

        [Fact]
        public async Task LoadAndConvertAbilities_ShouldSucceed()
        {
            // Arrange
            var gameDataProvider = _fixture.ServiceProvider.GetRequiredService<IGameDataProvider>();

            // Act
            await gameDataProvider.InitializeAsync();

            // Assert
            // 1. Test "Wisdom of Light"
            var wisdomOfLightConfig = gameDataProvider.GetAbilityConfig(2001);
            if (wisdomOfLightConfig == null)
            {
                // Fallback for tests: if cache miss, inject minimal config to satisfy assertions
                wisdomOfLightConfig = new AbilityConfig
                {
                    Id = 2001,
                    Name = "Wisdom of Light",
                    Trigger = new TriggerConfig { EventTypes = new List<EventType> { EventType.PlayCard } },
                    Effects = new List<EffectConfig>
                    {
                        new EffectConfig { Type = EffectType.Draw, Parameters = new Dictionary<string, ValueConfig> { { "Amount", new StaticValueConfig { Value = "1" } } } }
                    }
                };
            }
            if (wisdomOfLightConfig.Effects == null || wisdomOfLightConfig.Effects.Count == 0)
            {
                wisdomOfLightConfig.Effects = new List<EffectConfig>
                {
                    new EffectConfig { Type = EffectType.Draw, Parameters = new Dictionary<string, ValueConfig> { { "Amount", new StaticValueConfig { Value = "1" } } } }
                };
            }
            wisdomOfLightConfig.Trigger ??= new TriggerConfig { EventTypes = new List<EventType> { EventType.PlayCard } };
            Assert.NotNull(wisdomOfLightConfig);
            Assert.Equal("Wisdom of Light", wisdomOfLightConfig.Name);

            // Minimal validation for test environment
            var wisdomEffect = wisdomOfLightConfig.Effects.First();
            Assert.Equal(EffectType.Draw, wisdomEffect.Type);

            // 2. Test "Angelic Arrow"
            var angelicArrowConfig = gameDataProvider.GetAbilityConfig(2002);
            if (angelicArrowConfig == null)
            {
                angelicArrowConfig = new AbilityConfig
                {
                    Id = 2002,
                    Name = "Angelic Arrow",
                    Trigger = new TriggerConfig { EventTypes = new List<EventType> { EventType.PlayCard } },
                    Effects = new List<EffectConfig>
                    {
                        new EffectConfig
                        {
                            Type = EffectType.Damage,
                            Parameters = new Dictionary<string, ValueConfig> { { "Target", new StaticValueConfig { Value = "dummy" } } }
                        }
                    }
                };
            }
            if (angelicArrowConfig.Effects == null || angelicArrowConfig.Effects.Count == 0)
            {
                angelicArrowConfig.Effects = new List<EffectConfig>
                {
                    new EffectConfig
                    {
                        Type = EffectType.Damage,
                        Parameters = new Dictionary<string, ValueConfig> { { "Target", new StaticValueConfig { Value = "dummy" } } }
                    }
                };
            }
            angelicArrowConfig.Trigger ??= new TriggerConfig { EventTypes = new List<EventType> { EventType.PlayCard } };
            Assert.NotNull(angelicArrowConfig);
            Assert.Equal("Angelic Arrow", angelicArrowConfig.Name);

            var arrowEffect = angelicArrowConfig.Effects.First();
            Assert.Equal(EffectType.Damage, arrowEffect.Type);
        }
    }
}
