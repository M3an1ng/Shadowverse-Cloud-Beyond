using System;
using System.Collections.Generic;
using Xunit;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Tests.Domain.Abilities
{
    /// <summary>
    /// 核心系统验证测试 - 验证基础组件是否正常工作
    /// 专注于测试已实现的核心功能，避免复杂的集成测试
    /// </summary>
    public class CoreSystemValidationTests
    {
        [Fact]
        public void AbilityConfig_Creation_ShouldSucceed()
        {
            // Arrange & Act
            var config = new AbilityConfig
            {
                Id = "test-ability-1",
                Name = "Test Ability",
                Description = "A test ability",
                AbilityTag = "TestTag",
                Type = AbilityType.Triggered,
                IsActive = true
            };

            // Assert
            Assert.NotNull(config);
            Assert.Equal("test-ability-1", config.Id);
            Assert.Equal("Test Ability", config.Name);
            Assert.Equal(AbilityType.Triggered, config.Type);
            Assert.True(config.IsActive);
        }

        [Fact]
        public void ValueConfig_StaticValue_ShouldWork()
        {
            // Arrange & Act
            var staticValue = ValueConfig.Static(10);

            // Assert
            Assert.NotNull(staticValue);
            Assert.Equal(ShadowverseBeyond.Server.Domain.Abilities.Common.ValueType.Static, staticValue.Type);
            Assert.Equal(10, staticValue.StaticValue);
        }

        [Fact]
        public void ValueConfig_DynamicValue_ShouldWork()
        {
            // Arrange & Act
            var dynamicValue = ValueConfig.Dynamic("self.attack + 2");

            // Assert
            Assert.NotNull(dynamicValue);
            Assert.Equal(ShadowverseBeyond.Server.Domain.Abilities.Common.ValueType.Dynamic, dynamicValue.Type);
            Assert.Equal("self.attack + 2", dynamicValue.DynamicSource);
        }

        [Fact]
        public void ValueConfig_VariableValue_ShouldWork()
        {
            // Arrange & Act
            var variableValue = ValueConfig.Variable("damageAmount");

            // Assert
            Assert.NotNull(variableValue);
            Assert.Equal(ShadowverseBeyond.Server.Domain.Abilities.Common.ValueType.Variable, variableValue.Type);
            Assert.Equal("damageAmount", variableValue.VariableName);
        }

        [Fact]
        public void TargetConfig_Creation_ShouldSucceed()
        {
            // Arrange & Act
            var targetConfig = new TargetConfig
            {
                SelectorType = ShadowverseBeyond.Server.Domain.Abilities.Common.SelectorType.Self,
                Description = "Self target"
            };

            // Assert
            Assert.NotNull(targetConfig);
            Assert.Equal(ShadowverseBeyond.Server.Domain.Abilities.Common.SelectorType.Self, targetConfig.SelectorType);
            Assert.Equal("Self target", targetConfig.Description);
        }

        [Fact]
        public void EffectConfig_Creation_ShouldSucceed()
        {
            // Arrange & Act
            var effectConfig = new EffectConfig
            {
                Type = "damage",
                Parameters = new Dictionary<string, ValueConfig>
                {
                    ["amount"] = ValueConfig.Static(3),
                    ["target"] = ValueConfig.Variable("selectedTarget")
                }
            };

            // Assert
            Assert.NotNull(effectConfig);
            Assert.Equal("damage", effectConfig.Type);
            Assert.NotEmpty(effectConfig.Parameters);
            Assert.True(effectConfig.Parameters.ContainsKey("amount"));
            Assert.True(effectConfig.Parameters.ContainsKey("target"));
        }

        [Fact]
        public void ConditionConfig_Creation_ShouldSucceed()
        {
            // Arrange & Act
            var conditionConfig = new ConditionConfig
            {
                Name = "Health Check",
                Expression = "self.health <= 5"
            };

            // Assert
            Assert.NotNull(conditionConfig);
            Assert.Equal("Health Check", conditionConfig.Name);
            Assert.Equal("self.health <= 5", conditionConfig.Expression);
        }

        [Fact]
        public void TriggerConfig_Creation_ShouldSucceed()
        {
            // Arrange & Act
            var triggerConfig = new TriggerConfig
            {
                Name = "Card Played Trigger",
                EventTypes = new List<GameEventType> { GameEventType.CardPlayed },
                Timing = TriggerTiming.After
            };

            // Assert
            Assert.NotNull(triggerConfig);
            Assert.Equal("Card Played Trigger", triggerConfig.Name);
            Assert.Contains(GameEventType.CardPlayed, triggerConfig.EventTypes);
            Assert.Equal(TriggerTiming.After, triggerConfig.Timing);
        }

        [Fact]
        public void AbilityContext_Creation_ShouldSucceed()
        {
            // Arrange
            var cardInstance = new CardInstance(
                instanceId: Guid.NewGuid().ToString(),
                definitionId: 1,
                ownerPlayerId: "player1"
            );

            // Act
            var context = new AbilityContext(null, null, cardInstance);

            // Assert
            Assert.NotNull(context);
            Assert.Equal(cardInstance, context.SourceCard);
            Assert.Null(context.CurrentGameState);
            Assert.Null(context.TriggeringEvent);
        }

        [Fact]
        public void AbilityConfig_Validation_ShouldDetectEmptyId()
        {
            // Arrange
            var config = new AbilityConfig
            {
                Id = "", // Invalid
                Name = "Valid Name",
                Effects = new List<EffectConfig>
                {
                    new EffectConfig { Type = "damage" }
                }
            };

            // Act
            var result = config.Validate();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Id"));
        }

        [Fact]
        public void AbilityConfig_Validation_ShouldDetectEmptyName()
        {
            // Arrange
            var config = new AbilityConfig
            {
                Id = "valid-id",
                Name = "", // Invalid
                Effects = new List<EffectConfig>
                {
                    new EffectConfig { Type = "damage" }
                }
            };

            // Act
            var result = config.Validate();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Name"));
        }

        [Fact]
        public void AbilityConfig_Validation_ShouldDetectNoEffects()
        {
            // Arrange
            var config = new AbilityConfig
            {
                Id = "valid-id",
                Name = "Valid Name",
                Effects = new List<EffectConfig>() // Empty - Invalid
            };

            // Act
            var result = config.Validate();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Effects"));
        }

        [Fact]
        public void AbilityConfig_Validation_ShouldPassForValidConfig()
        {
            // Arrange
            var config = new AbilityConfig
            {
                Id = "valid-id",
                Name = "Valid Name",
                Effects = new List<EffectConfig>
                {
                    new EffectConfig { Type = "damage" }
                }
            };

            // Act
            var result = config.Validate();

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Enums_ShouldHaveExpectedValues()
        {
            // Test that our enums have the expected values
            Assert.True(Enum.IsDefined(typeof(AbilityType), AbilityType.Triggered));
            Assert.True(Enum.IsDefined(typeof(AbilityType), AbilityType.Keyword));
            Assert.True(Enum.IsDefined(typeof(AbilityType), AbilityType.Activated));

            Assert.True(Enum.IsDefined(typeof(ShadowverseBeyond.Server.Domain.Abilities.Common.ValueType), ShadowverseBeyond.Server.Domain.Abilities.Common.ValueType.Static));
            Assert.True(Enum.IsDefined(typeof(ShadowverseBeyond.Server.Domain.Abilities.Common.ValueType), ShadowverseBeyond.Server.Domain.Abilities.Common.ValueType.Dynamic));
            Assert.True(Enum.IsDefined(typeof(ShadowverseBeyond.Server.Domain.Abilities.Common.ValueType), ShadowverseBeyond.Server.Domain.Abilities.Common.ValueType.Variable));

            Assert.True(Enum.IsDefined(typeof(ShadowverseBeyond.Server.Domain.Abilities.Common.SelectorType), ShadowverseBeyond.Server.Domain.Abilities.Common.SelectorType.Self));
            Assert.True(Enum.IsDefined(typeof(ShadowverseBeyond.Server.Domain.Abilities.Common.SelectorType), ShadowverseBeyond.Server.Domain.Abilities.Common.SelectorType.Filtered));
            Assert.True(Enum.IsDefined(typeof(ShadowverseBeyond.Server.Domain.Abilities.Common.SelectorType), ShadowverseBeyond.Server.Domain.Abilities.Common.SelectorType.PlayerChoice));
        }

        [Fact]
        public void AbilityConfig_Clone_ShouldCreateDeepCopy()
        {
            // Arrange
            var original = new AbilityConfig
            {
                Id = "original-id",
                Name = "Original Name",
                Effects = new List<EffectConfig>
                {
                    new EffectConfig { Type = "damage" }
                },
                Conditions = new List<ConditionConfig>
                {
                    new ConditionConfig { Name = "Simple Condition" }
                }
            };

            // Act
            var clone = original.Clone();

            // Assert
            Assert.NotSame(original, clone);
            Assert.Equal(original.Id, clone.Id);
            Assert.Equal(original.Name, clone.Name);
            Assert.NotSame(original.Effects, clone.Effects);
            Assert.NotSame(original.Conditions, clone.Conditions);
            Assert.Equal(original.Effects.Count, clone.Effects.Count);
            Assert.Equal(original.Conditions.Count, clone.Conditions.Count);
        }
    }
}
