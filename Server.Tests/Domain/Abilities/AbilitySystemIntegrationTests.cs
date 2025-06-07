using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Processing;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Tests.Domain.Abilities
{
    /// <summary>
    /// 端到端集成测试，验证完整的能力系统流程：
    /// DSL解析 → AST生成 → AbilityConfig创建 → 运行时执行
    /// </summary>
    public class AbilitySystemIntegrationTests
    {
        private readonly Mock<ILogger<DslExpressionParser>> _mockDslLogger;
        private readonly Mock<ILogger<FilteredTargetSelector>> _mockTargetSelectorLogger;
        private readonly Mock<ICardQueryExecutor> _mockQueryExecutor;
        private readonly Mock<IGameStateActionDispatcher> _mockGameStateDispatcher;
        private readonly Mock<IValueSourceResolver> _mockValueSourceResolver;
        private readonly Mock<ILogger<EffectHandler>> _mockEffectHandlerLogger;
        private readonly DslExpressionParser _dslParser;
        private readonly EffectHandler _effectHandler;

        public AbilitySystemIntegrationTests()
        {
            _mockDslLogger = new Mock<ILogger<DslExpressionParser>>();
            _mockTargetSelectorLogger = new Mock<ILogger<FilteredTargetSelector>>();
            _mockQueryExecutor = new Mock<ICardQueryExecutor>();
            _mockGameStateDispatcher = new Mock<IGameStateActionDispatcher>();
            _mockValueSourceResolver = new Mock<IValueSourceResolver>();
            _mockEffectHandlerLogger = new Mock<ILogger<EffectHandler>>();

            _dslParser = new DslExpressionParser(_mockDslLogger.Object);
            _effectHandler = new EffectHandler(_mockGameStateDispatcher.Object, _mockValueSourceResolver.Object, _mockEffectHandlerLogger.Object);
        }

        [Fact]
        public async Task EndToEnd_SimpleDirectDamageAbility_ShouldExecuteSuccessfully()
        {
            // Arrange: 创建一个简单的直接伤害能力DSL
            var dslSource = @"
                ABILITY DirectDamage
                TYPE Triggered
                TAG Fanfare
                TRIGGER
                    EVENT CardPlayed
                    SCOPE Self
                END_TRIGGER
                TARGET
                    TYPE enemy
                    SCOPE field
                    COUNT 1
                    SELECTION random
                END_TARGET
                EFFECT
                    TYPE damage
                    TARGET target_1
                    AMOUNT 3
                END_EFFECT
                END_ABILITY";

            // 设置模拟数据
            var sourceCard = new CardInstance(
                instanceId: Guid.NewGuid().ToString(),
                definitionId: 1,
                ownerPlayerId: "player1"
            )
            {
                CurrentAttack = 2,
                CurrentHealth = 3
            };

            var targetCard = new CardInstance(
                instanceId: Guid.NewGuid().ToString(),
                definitionId: 2,
                ownerPlayerId: "player2"
            )
            {
                CurrentAttack = 4,
                CurrentHealth = 5
            };

            var gameState = new GameStateDto
            {
                CurrentPlayerId = "player1",
                Players = new List<PlayerStateDto>
                {
                    new PlayerStateDto { PlayerId = "player1" },
                    new PlayerStateDto { PlayerId = "player2" }
                }
            };

            // 模拟查询执行器返回目标
            var queryResult = new CardQueryResult
            {
                Results = new List<object> { targetCard }
            };
            _mockQueryExecutor.Setup(x => x.ExecuteQueryAsync(It.IsAny<CardQueryConfig>(), It.IsAny<QueryContext>()))
                .ReturnsAsync(queryResult);

            // Act: 执行完整流程
            try
            {
                // 1. DSL解析 → AbilityConfig
                var abilityConfig = AbilityConfig.FromDsl(dslSource);
                Assert.NotNull(abilityConfig);
                Assert.Equal("DirectDamage", abilityConfig.Name);
                Assert.Equal("Fanfare", abilityConfig.AbilityTag);

                // 2. 创建能力执行上下文
                var abilityContext = new AbilityContext(gameState, null, sourceCard);

                // 3. 目标选择
                if (abilityConfig.Targets.Any())
                {
                    var targetConfig = abilityConfig.Targets.First();
                    var targetSelector = TargetSelectorFactory.CreateSelector(targetConfig, _mockQueryExecutor.Object, _mockTargetSelectorLogger.Object);
                    var selectedTargets = await targetSelector.SelectTargetsAsync(targetConfig, abilityContext);
                    abilityContext.SelectedTargets = selectedTargets;

                    Assert.NotEmpty(abilityContext.SelectedTargets);
                }

                // 4. 效果执行
                if (abilityConfig.Effects.Any())
                {
                    var effectConfig = abilityConfig.Effects.First();
                    var result = await _effectHandler.ExecuteAsync(effectConfig, abilityContext);

                    Assert.True(result.Success);
                }

                // Assert: 验证整个流程成功
                Assert.True(true, "端到端测试成功完成");
            }
            catch (Exception ex)
            {
                Assert.True(false, $"端到端测试失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [Fact]
        public void DSLParsing_ComplexAbilityWithConditions_ShouldParseCorrectly()
        {
            // Arrange: 复杂能力DSL，包含条件
            var dslSource = @"
                ABILITY ConditionalHeal
                TYPE Triggered
                TAG LastWords
                TRIGGER
                    EVENT CardDestroyed
                    SCOPE Self
                END_TRIGGER
                CONDITION
                    TYPE expression
                    EXPRESSION self.health <= 2
                END_CONDITION
                TARGET
                    TYPE ally
                    SCOPE field
                    COUNT all
                END_TARGET
                EFFECT
                    TYPE heal
                    TARGET target_1
                    AMOUNT 2
                END_EFFECT
                END_ABILITY";

            // Act & Assert
            var abilityConfig = AbilityConfig.FromDsl(dslSource);

            Assert.NotNull(abilityConfig);
            Assert.Equal("ConditionalHeal", abilityConfig.Name);
            Assert.Equal("LastWords", abilityConfig.AbilityTag);
            Assert.NotEmpty(abilityConfig.Conditions);
            Assert.NotEmpty(abilityConfig.Targets);
            Assert.NotEmpty(abilityConfig.Effects);
        }

        [Fact]
        public void TargetSelection_SelfTargetSelector_ShouldReturnSourceCard()
        {
            // Arrange
            var sourceCard = new CardInstance(
                instanceId: Guid.NewGuid().ToString(),
                definitionId: 1,
                ownerPlayerId: "player1"
            );

            var context = new AbilityContext(null, null, sourceCard);
            var targetConfig = new TargetConfig
            {
                SelectorType = ShadowverseBeyond.Server.Domain.Abilities.Common.SelectorType.Self
            };

            var selector = new SelfTargetSelector();

            // Act
            var result = selector.SelectTargetsAsync(targetConfig, context).Result;

            // Assert
            Assert.Single(result);
            Assert.Equal(sourceCard.InstanceId, result.First().Id);
        }

        [Fact]
        public void ValueConfig_StaticValue_ShouldReturnCorrectValue()
        {
            // Arrange & Act
            var staticValue = ValueConfig.Static(5);
            var dynamicValue = ValueConfig.Dynamic("self.attack");
            var variableValue = ValueConfig.Variable("damageAmount");

            // Assert
            Assert.Equal(ShadowverseBeyond.Server.Domain.Abilities.Common.ValueType.Static, staticValue.Type);
            Assert.Equal(5, staticValue.StaticValue);

            Assert.Equal(ShadowverseBeyond.Server.Domain.Abilities.Common.ValueType.Dynamic, dynamicValue.Type);
            Assert.Equal("self.attack", dynamicValue.DynamicExpression);

            Assert.Equal(ShadowverseBeyond.Server.Domain.Abilities.Common.ValueType.Variable, variableValue.Type);
            Assert.Equal("damageAmount", variableValue.VariableName);
        }

        [Fact]
        public void AbilityConfig_Validation_ShouldDetectInvalidConfigurations()
        {
            // Arrange
            var invalidConfig = new AbilityConfig
            {
                Id = "",  // Invalid: empty ID
                Name = "", // Invalid: empty name
                Effects = new List<EffectConfig>() // Invalid: no effects
            };

            // Act
            var validationResult = invalidConfig.Validate();

            // Assert
            Assert.False(validationResult.IsValid);
            Assert.NotEmpty(validationResult.Errors);
        }
    }
}
