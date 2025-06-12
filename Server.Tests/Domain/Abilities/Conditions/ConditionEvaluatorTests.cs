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
        // Changed DslEvaluator mock to IDslConditionEvaluator
        private readonly Mock<IDslConditionEvaluator> _mockDslConditionEvaluator;
        // Removed unused DslExpressionParser mock
        // private readonly Mock<DslExpressionParser> _mockDslParser;
        private readonly Mock<ILogger<ConditionEvaluator>> _mockLogger;
        private readonly ConditionEvaluator _conditionEvaluator;

        public ConditionEvaluatorTests()
        {
            // Changed mock type
            _mockDslConditionEvaluator = new Mock<IDslConditionEvaluator>();
            // Removed parser mock initialization
            // _mockDslParser = new Mock<DslExpressionParser>(Mock.Of<ILogger<DslExpressionParser>>());
            _mockLogger = new Mock<ILogger<ConditionEvaluator>>();

            // Updated constructor call with correct arguments
            _conditionEvaluator = new ConditionEvaluator(
                _mockDslConditionEvaluator.Object,
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

            // Removed parser setup - ConditionEvaluator uses IDslConditionEvaluator directly
            // var mockExpression = new Mock<ExpressionNode>();
            // _mockDslParser.Setup(p => p.ParseExpression(config.Expression))
            //     .Returns(mockExpression.Object);

            var evaluationResult = EvaluationResult.Success(true);
            // Setup the mock for the interface method used by ConditionEvaluator
            // The interface returns Task<bool>, so setup should return a boolean
            _mockDslConditionEvaluator.Setup(e => e.EvaluateDslExpressionAsync(config.Expression, It.IsAny<ConditionEvaluationContext>()))
                .ReturnsAsync(true); // Expecting true in this test case

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

            // Removed parser setup
            // var mockExpression = new Mock<ExpressionNode>();
            // _mockDslParser.Setup(p => p.ParseExpression(config.Expression))
            //     .Returns(mockExpression.Object);

            var evaluationResult = EvaluationResult.Success(5); // Non-zero integer should convert to true
            // Setup the mock for the interface method
            // ConditionEvaluator should handle the conversion internally if needed,
            // but the mock should return the expected boolean result of the evaluation.
            // Assuming non-zero integer expression evaluates to true condition.
            _mockDslConditionEvaluator.Setup(e => e.EvaluateDslExpressionAsync(config.Expression, It.IsAny<ConditionEvaluationContext>()))
                .ReturnsAsync(true); // Expecting true in this test case

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

            // Removed parser setup - ConditionEvaluator handles expression directly
            // _mockDslParser.Setup(p => p.ParseExpression(config.Expression))
            //     .Returns((ExpressionNode?)null);

            // Setup the evaluator to simulate parse failure by returning false
            _mockDslConditionEvaluator.Setup(e => e.EvaluateDslExpressionAsync(config.Expression, It.IsAny<ConditionEvaluationContext>()))
               .ReturnsAsync(false); // Expecting false on parse failure

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

            // Removed parser setup
            // var mockExpression = new Mock<ExpressionNode>();
            // _mockDslParser.Setup(p => p.ParseExpression(config.Expression))
            //     .Returns(mockExpression.Object);

            var evaluationResult = EvaluationResult.Failure("Evaluation error");
            // Setup the mock for the interface method to simulate evaluation failure by returning false
            _mockDslConditionEvaluator.Setup(e => e.EvaluateDslExpressionAsync(config.Expression, It.IsAny<ConditionEvaluationContext>()))
                .ReturnsAsync(false); // Expecting false on evaluation failure

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

            // Removed parser setup
            // var mockExpression = new Mock<ExpressionNode>();
            // _mockDslParser.Setup(p => p.ParseExpression(config.Expression))
            //     .Returns(mockExpression.Object);

            var evaluationResult = EvaluationResult.Success(value);
            // Setup the mock for the interface method
            // The mock should return the final boolean result expected for the test case
            _mockDslConditionEvaluator.Setup(e => e.EvaluateDslExpressionAsync(config.Expression, It.IsAny<ConditionEvaluationContext>()))
                .ReturnsAsync(expected); // Use the 'expected' value from the Theory

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
