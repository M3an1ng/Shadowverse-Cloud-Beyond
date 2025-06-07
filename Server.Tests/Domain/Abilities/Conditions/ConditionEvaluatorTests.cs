using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Processing;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Dtos;

namespace Server.Tests.Domain.Abilities.Conditions
{
    public class ConditionEvaluatorTests
    {
        private readonly Mock<DslEvaluator> _mockDslEvaluator;
        private readonly Mock<DslExpressionParser> _mockDslParser;
        private readonly Mock<ILogger<ConditionEvaluator>> _mockLogger;
        private readonly ConditionEvaluator _conditionEvaluator;

        public ConditionEvaluatorTests()
        {
            _mockDslEvaluator = new Mock<DslEvaluator>(Mock.Of<ILogger<DslEvaluator>>());
            _mockDslParser = new Mock<DslExpressionParser>(Mock.Of<ILogger<DslExpressionParser>>());
            _mockLogger = new Mock<ILogger<ConditionEvaluator>>();

            _conditionEvaluator = new ConditionEvaluator(
                _mockDslEvaluator.Object,
                _mockDslParser.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task EvaluateAsync_DisabledCondition_ReturnsFalse()
        {
            // Arrange
            var config = new ConditionConfig
            {
                Id = "test_condition",
                IsEnabled = false,
                Expression = "true"
            };
            var context = CreateTestAbilityContext();

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(config, context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EvaluateAsync_EmptyExpression_ReturnsTrue()
        {
            // Arrange
            var config = new ConditionConfig
            {
                Id = "test_condition",
                IsEnabled = true,
                Expression = ""
            };
            var context = CreateTestAbilityContext();

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(config, context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task EvaluateAsync_ValidBooleanExpression_ReturnsCorrectResult()
        {
            // Arrange
            var config = new ConditionConfig
            {
                Id = "test_condition",
                IsEnabled = true,
                Expression = "self.health > 5"
            };
            var context = CreateTestAbilityContext();

            var mockExpression = new Mock<ExpressionNode>();
            _mockDslParser.Setup(p => p.ParseExpression(config.Expression))
                .Returns(mockExpression.Object);

            var evaluationResult = EvaluationResult.Success(true);
            _mockDslEvaluator.Setup(e => e.EvaluateAsync(mockExpression.Object, It.IsAny<EvaluationContext>()))
                .ReturnsAsync(evaluationResult);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(config, context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task EvaluateAsync_NonBooleanExpression_ConvertsToBoolean()
        {
            // Arrange
            var config = new ConditionConfig
            {
                Id = "test_condition",
                IsEnabled = true,
                Expression = "self.attack"
            };
            var context = CreateTestAbilityContext();

            var mockExpression = new Mock<ExpressionNode>();
            _mockDslParser.Setup(p => p.ParseExpression(config.Expression))
                .Returns(mockExpression.Object);

            var evaluationResult = EvaluationResult.Success(5); // Non-zero integer should convert to true
            _mockDslEvaluator.Setup(e => e.EvaluateAsync(mockExpression.Object, It.IsAny<EvaluationContext>()))
                .ReturnsAsync(evaluationResult);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(config, context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task EvaluateAsync_ParseFailure_ReturnsFalse()
        {
            // Arrange
            var config = new ConditionConfig
            {
                Id = "test_condition",
                IsEnabled = true,
                Expression = "invalid expression"
            };
            var context = CreateTestAbilityContext();

            _mockDslParser.Setup(p => p.ParseExpression(config.Expression))
                .Returns((ExpressionNode?)null);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(config, context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EvaluateAsync_EvaluationFailure_ReturnsFalse()
        {
            // Arrange
            var config = new ConditionConfig
            {
                Id = "test_condition",
                IsEnabled = true,
                Expression = "self.health > 5"
            };
            var context = CreateTestAbilityContext();

            var mockExpression = new Mock<ExpressionNode>();
            _mockDslParser.Setup(p => p.ParseExpression(config.Expression))
                .Returns(mockExpression.Object);

            var evaluationResult = EvaluationResult.Failure("Evaluation error");
            _mockDslEvaluator.Setup(e => e.EvaluateAsync(mockExpression.Object, It.IsAny<EvaluationContext>()))
                .ReturnsAsync(evaluationResult);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(config, context);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(-1, true)]
        [InlineData(0.0, false)]
        [InlineData(0.1, true)]
        [InlineData("", false)]
        [InlineData("false", false)]
        [InlineData("true", true)]
        [InlineData("hello", true)]
        [InlineData(null, false)]
        public async Task EvaluateAsync_TypeConversion_ConvertsCorrectly(object? value, bool expected)
        {
            // Arrange
            var config = new ConditionConfig
            {
                Id = "test_condition",
                IsEnabled = true,
                Expression = "test_value"
            };
            var context = CreateTestAbilityContext();

            var mockExpression = new Mock<ExpressionNode>();
            _mockDslParser.Setup(p => p.ParseExpression(config.Expression))
                .Returns(mockExpression.Object);

            var evaluationResult = EvaluationResult.Success(value);
            _mockDslEvaluator.Setup(e => e.EvaluateAsync(mockExpression.Object, It.IsAny<EvaluationContext>()))
                .ReturnsAsync(evaluationResult);

            // Act
            var result = await _conditionEvaluator.EvaluateAsync(config, context);

            // Assert
            Assert.Equal(expected, result);
        }

        private AbilityContext CreateTestAbilityContext()
        {
            var gameState = new GameStateDto
            {
                GameId = "test_game",
                CurrentPlayerId = "player1",
                Players = new List<PlayerStateDto>
                {
                    new PlayerStateDto { PlayerId = "player1" },
                    new PlayerStateDto { PlayerId = "player2" }
                }
            };

            return new AbilityContext
            {
                RequestingPlayerId = 1,
                CurrentGameState = gameState,
                SourceCard = null
            };
        }
    }
}
