using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Dtos;

namespace Server.Tests.Domain.Abilities.Conditions
{
    public class CompositeConditionEvaluatorTests
    {
        private readonly Mock<IConditionEvaluator> _mockConditionEvaluator;
        private readonly Mock<ILogger<CompositeConditionEvaluator>> _mockLogger;
        private readonly CompositeConditionEvaluator _compositeEvaluator;

        public CompositeConditionEvaluatorTests()
        {
            _mockConditionEvaluator = new Mock<IConditionEvaluator>();
            _mockLogger = new Mock<ILogger<CompositeConditionEvaluator>>();

            _compositeEvaluator = new CompositeConditionEvaluator(
                _mockConditionEvaluator.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task EvaluateConditionsAsync_EmptyList_ReturnsTrue()
        {
            // Arrange
            var conditions = new List<ConditionConfig>();
            var context = CreateTestAbilityContext();

            // Act
            var result = await _compositeEvaluator.EvaluateConditionsAsync(conditions, context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task EvaluateConditionsAsync_AndLogic_AllTrue_ReturnsTrue()
        {
            // Arrange
            var conditions = new List<ConditionConfig>
            {
                new ConditionConfig { Id = "condition1", IsEnabled = true },
                new ConditionConfig { Id = "condition2", IsEnabled = true }
            };
            var context = CreateTestAbilityContext();

            _mockConditionEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionConfig>(), context))
                .ReturnsAsync(true);

            // Act
            var result = await _compositeEvaluator.EvaluateConditionsAsync(conditions, context, LogicOperator.And);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task EvaluateConditionsAsync_AndLogic_OneFalse_ReturnsFalse()
        {
            // Arrange
            var conditions = new List<ConditionConfig>
            {
                new ConditionConfig { Id = "condition1", IsEnabled = true },
                new ConditionConfig { Id = "condition2", IsEnabled = true }
            };
            var context = CreateTestAbilityContext();

            _mockConditionEvaluator.SetupSequence(e => e.EvaluateAsync(It.IsAny<ConditionConfig>(), context))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            // Act
            var result = await _compositeEvaluator.EvaluateConditionsAsync(conditions, context, LogicOperator.And);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EvaluateConditionsAsync_OrLogic_OneTrue_ReturnsTrue()
        {
            // Arrange
            var conditions = new List<ConditionConfig>
            {
                new ConditionConfig { Id = "condition1", IsEnabled = true },
                new ConditionConfig { Id = "condition2", IsEnabled = true }
            };
            var context = CreateTestAbilityContext();

            _mockConditionEvaluator.SetupSequence(e => e.EvaluateAsync(It.IsAny<ConditionConfig>(), context))
                .ReturnsAsync(false)
                .ReturnsAsync(true);

            // Act
            var result = await _compositeEvaluator.EvaluateConditionsAsync(conditions, context, LogicOperator.Or);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task EvaluateConditionsAsync_OrLogic_AllFalse_ReturnsFalse()
        {
            // Arrange
            var conditions = new List<ConditionConfig>
            {
                new ConditionConfig { Id = "condition1", IsEnabled = true },
                new ConditionConfig { Id = "condition2", IsEnabled = true }
            };
            var context = CreateTestAbilityContext();

            _mockConditionEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionConfig>(), context))
                .ReturnsAsync(false);

            // Act
            var result = await _compositeEvaluator.EvaluateConditionsAsync(conditions, context, LogicOperator.Or);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EvaluateConditionsAsync_NotLogic_True_ReturnsFalse()
        {
            // Arrange
            var conditions = new List<ConditionConfig>
            {
                new ConditionConfig { Id = "condition1", IsEnabled = true }
            };
            var context = CreateTestAbilityContext();

            _mockConditionEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionConfig>(), context))
                .ReturnsAsync(true);

            // Act
            var result = await _compositeEvaluator.EvaluateConditionsAsync(conditions, context, LogicOperator.Not);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EvaluateConditionsAsync_XorLogic_OnlyOneTrue_ReturnsTrue()
        {
            // Arrange
            var conditions = new List<ConditionConfig>
            {
                new ConditionConfig { Id = "condition1", IsEnabled = true },
                new ConditionConfig { Id = "condition2", IsEnabled = true }
            };
            var context = CreateTestAbilityContext();

            _mockConditionEvaluator.SetupSequence(e => e.EvaluateAsync(It.IsAny<ConditionConfig>(), context))
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            // Act
            var result = await _compositeEvaluator.EvaluateConditionsAsync(conditions, context, LogicOperator.Xor);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task EvaluateConditionsAsync_XorLogic_BothTrue_ReturnsFalse()
        {
            // Arrange
            var conditions = new List<ConditionConfig>
            {
                new ConditionConfig { Id = "condition1", IsEnabled = true },
                new ConditionConfig { Id = "condition2", IsEnabled = true }
            };
            var context = CreateTestAbilityContext();

            _mockConditionEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionConfig>(), context))
                .ReturnsAsync(true);

            // Act
            var result = await _compositeEvaluator.EvaluateConditionsAsync(conditions, context, LogicOperator.Xor);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EvaluateConditionsAsync_DisabledConditions_IgnoresDisabled()
        {
            // Arrange
            var conditions = new List<ConditionConfig>
            {
                new ConditionConfig { Id = "condition1", IsEnabled = true },
                new ConditionConfig { Id = "condition2", IsEnabled = false }, // Disabled
                new ConditionConfig { Id = "condition3", IsEnabled = true }
            };
            var context = CreateTestAbilityContext();

            _mockConditionEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionConfig>(), context))
                .ReturnsAsync(true);

            // Act
            var result = await _compositeEvaluator.EvaluateConditionsAsync(conditions, context, LogicOperator.And);

            // Assert
            Assert.True(result);
            // Verify that only enabled conditions were evaluated
            _mockConditionEvaluator.Verify(e => e.EvaluateAsync(It.IsAny<ConditionConfig>(), context), Times.Exactly(2));
        }

        [Fact]
        public async Task EvaluateConditionsAsync_Exception_ReturnsFalse()
        {
            // Arrange
            var conditions = new List<ConditionConfig>
            {
                new ConditionConfig { Id = "condition1", IsEnabled = true }
            };
            var context = CreateTestAbilityContext();

            _mockConditionEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionConfig>(), context))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _compositeEvaluator.EvaluateConditionsAsync(conditions, context);

            // Assert
            Assert.False(result);
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
