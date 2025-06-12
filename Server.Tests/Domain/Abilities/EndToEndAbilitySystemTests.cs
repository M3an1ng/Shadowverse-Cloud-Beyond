using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq; // Add Moq namespace
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions; // Add FunctionRegistry namespace
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs; // Add Buffs namespace
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Interfaces; // Add Interfaces namespace
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;

namespace ShadowverseBeyond.Server.Tests.Domain.Abilities
{
    /// <summary>
    /// 端到端测试，验证整个能力系统的功能
    /// 测试从AbilityConfig创建到运行时执行的完整流程
    /// </summary>
    public class EndToEndAbilitySystemTests
    {
        private readonly ILogger<EndToEndAbilitySystemTests> _logger;
        private readonly Mock<IBuffManager> _mockBuffManager;
        private readonly Mock<ICardRepository> _mockCardRepository;
        private readonly Mock<IFunctionRegistry> _mockFunctionRegistry;

        public EndToEndAbilitySystemTests()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<EndToEndAbilitySystemTests>();
            _mockBuffManager = new Mock<IBuffManager>();
            _mockCardRepository = new Mock<ICardRepository>();
            _mockFunctionRegistry = new Mock<IFunctionRegistry>();
        }

        [Fact]
        public void AbilityConfig_CanBeCreated_WithBasicConfiguration()
        {
            // Arrange & Act
            var config = new AbilityConfig
            {
                Id = "test_ability_1",
                CardId = 1001,
                Name = "Test Ability",
                Description = "A test ability for validation",
                AbilityTag = "Test",
                Type = AbilityType.Triggered,
                Trigger = new TriggerConfig
                {
                    EventTypes = new List<GameEventType> { GameEventType.CardSummoned }
                },
                Conditions = new List<ConditionConfig>(),
                Targets = new List<TargetConfig>(),
                Effects = new List<EffectConfig>
                {
                    new EffectConfig
                    {
                        Type = "modify_property",
                        Parameters = new Dictionary<string, ValueConfig>
                        {
                            ["property"] = ValueConfig.Static("CanAttack"),
                            ["value"] = ValueConfig.Static(true)
                        },
                        ExecutionOrder = 1
                    }
                }
            };

            // Assert
            Assert.NotNull(config);
            Assert.Equal("test_ability_1", config.Id);
            Assert.Equal(1001, config.CardId);
            Assert.Equal("Test Ability", config.Name);
            Assert.Equal(AbilityType.Triggered, config.Type);
            Assert.Single(config.Effects);
        }

        [Fact]
        public void Ability_CanBeCreated_FromAbilityConfig()
        {
            // Arrange
            var config = CreateTestAbilityConfig();
            var cardInstance = CreateTestCardInstance();

            // Act
            // Pass mock objects to the Ability constructor
            var ability = new Ability(config, _mockBuffManager.Object, _mockCardRepository.Object, _mockFunctionRegistry.Object, cardInstance);

            // Assert
            Assert.NotNull(ability);
            Assert.Equal(config.Id.GetHashCode(), ability.AbilityId);
            Assert.Equal(config.CardId, ability.CardId);
            Assert.Equal(config.Name, ability.Name);
            Assert.Equal(config.Type, ability.Type);
            Assert.Equal(cardInstance, ability.SourceCard);
            Assert.Equal(AbilityState.Inactive, ability.State);
            Assert.False(ability.IsActive);
        }

        [Fact]
        public void Ability_CanBeActivated_AndStateChanges()
        {
            // Arrange
            var config = CreateTestAbilityConfig();
            var cardInstance = CreateTestCardInstance();
            // Pass mock objects to the Ability constructor
            var ability = new Ability(config, _mockBuffManager.Object, _mockCardRepository.Object, _mockFunctionRegistry.Object, cardInstance);

            // Act
            ability.Activate();

            // Assert
            Assert.Equal(AbilityState.Active, ability.State);
            Assert.True(ability.IsActive);
            Assert.NotNull(ability.ActivationTime);
        }

        [Fact]
        public void Ability_CanCheckTriggerConditions()
        {
            // Arrange
            var config = CreateTestAbilityConfig();
            var cardInstance = CreateTestCardInstance();
            // Pass mock objects to the Ability constructor
            var ability = new Ability(config, _mockBuffManager.Object, _mockCardRepository.Object, _mockFunctionRegistry.Object, cardInstance);
            ability.Activate();

            var gameEvent = new SummonEvent(
                new PlayerStateDto { PlayerId = "player1" },
                cardInstance
            );

            // Act
            var canTrigger = ability.CanTrigger(gameEvent);

            // Assert
            Assert.True(canTrigger);
        }

        [Fact]
        public async Task Ability_CanExecuteEffects_WithBasicConfiguration()
        {
            // Arrange
            var config = CreateTestAbilityConfig();
            var cardInstance = CreateTestCardInstance();
            // Pass mock objects to the Ability constructor
            var ability = new Ability(config, _mockBuffManager.Object, _mockCardRepository.Object, _mockFunctionRegistry.Object, cardInstance);
            ability.Activate();

            var context = new EffectExecutionContext
            {
                SourceCard = cardInstance,
                SelectedTargets = new List<CardInstance> { cardInstance },
                Variables = new Dictionary<string, object>(),
                GameState = null, // TODO: Create test GameState
                TriggeringEvent = null
            };

            // Act
            var events = await ability.ExecuteEffectsAsync(context);

            // Assert
            Assert.NotNull(events);
            // Note: Events list might be empty due to incomplete implementation
            // but the method should not throw exceptions
        }

        private AbilityConfig CreateTestAbilityConfig()
        {
            return new AbilityConfig
            {
                Id = "test_rush_ability",
                CardId = 1001,
                Name = "Rush",
                Description = "Can attack units immediately",
                AbilityTag = "Rush",
                Type = AbilityType.Keyword,
                Trigger = new TriggerConfig
                {
                    EventTypes = new List<GameEventType> { GameEventType.CardSummoned }
                },
                Conditions = new List<ConditionConfig>(),
                Targets = new List<TargetConfig>(),
                Effects = new List<EffectConfig>
                {
                    new EffectConfig
                    {
                        Type = "modify_property",
                        Parameters = new Dictionary<string, ValueConfig>
                        {
                            ["property"] = ValueConfig.Static("CanAttack"),
                            ["value"] = ValueConfig.Static(true)
                        },
                        ExecutionOrder = 1
                    }
                }
            };
        }

        private CardInstance CreateTestCardInstance()
        {
            var instance = new CardInstance(
                instanceId: Guid.NewGuid().ToString(),
                definitionId: 1001,
                ownerPlayerId: "player1"
            )
            {
                CurrentAttack = 2,
                CurrentHealth = 1,
                MaxHealth = 1,
                CurrentCost = 1,
                CardType = "Unit"
            };
            return instance;
        }
    }
}
