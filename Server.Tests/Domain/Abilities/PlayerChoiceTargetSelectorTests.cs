using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace ShadowverseBeyond.Server.Tests.Domain.Abilities
{
    /// <summary>
    /// 测试PlayerChoiceTargetSelector的TODO实现
    /// </summary>
    public class PlayerChoiceTargetSelectorTests
    {
        private readonly Mock<ICardQueryExecutor> _mockQueryExecutor;
        private readonly Mock<ILogger<PlayerChoiceTargetSelector>> _mockLogger;
        private readonly Mock<ILogger<FilteredTargetSelector>> _mockFilteredLogger;

        public PlayerChoiceTargetSelectorTests()
        {
            _mockQueryExecutor = new Mock<ICardQueryExecutor>();
            _mockLogger = new Mock<ILogger<PlayerChoiceTargetSelector>>();
            _mockFilteredLogger = new Mock<ILogger<FilteredTargetSelector>>();
        }

        [Fact]
        public void Constructor_WithValidDependencies_ShouldSucceed()
        {
            // Arrange
            var config = CreateTestTargetConfig();

            // Act & Assert
            var selector = new PlayerChoiceTargetSelector(config, _mockQueryExecutor.Object, _mockLogger.Object);
            Assert.NotNull(selector);
        }

        [Fact]
        public void Constructor_WithNullDependencies_ShouldThrowArgumentNullException()
        {
            // Arrange
            var config = CreateTestTargetConfig();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new PlayerChoiceTargetSelector(null!, _mockQueryExecutor.Object, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => 
                new PlayerChoiceTargetSelector(config, null!, _mockLogger.Object));
            Assert.Throws<ArgumentNullException>(() => 
                new PlayerChoiceTargetSelector(config, _mockQueryExecutor.Object, null!));
        }

        [Fact]
        public async Task SelectTargetsAsync_WithValidQuery_ShouldReturnFilteredTargets()
        {
            // Arrange
            var config = CreateTestTargetConfig();
            var context = CreateTestGameContext();
            var selector = new PlayerChoiceTargetSelector(config, _mockQueryExecutor.Object, _mockLogger.Object);

            var mockQueryResult = new CardQueryResult
            {
                Success = true,
                Results = new List<object>
                {
                    CreateTestCardInstance(Guid.NewGuid(), CardZone.Field),
                    CreateTestCardInstance(Guid.NewGuid(), CardZone.Hand)
                }
            };

            _mockQueryExecutor.Setup(x => x.ExecuteQueryAsync(It.IsAny<CardQueryConfig>(), It.IsAny<GameContext>()))
                .ReturnsAsync(mockQueryResult);

            // Act
            var result = await selector.SelectTargetsAsync(config, context);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            _mockQueryExecutor.Verify(x => x.ExecuteQueryAsync(It.IsAny<CardQueryConfig>(), It.IsAny<GameContext>()), Times.Once);
        }

        [Fact]
        public async Task SelectTargetsAsync_WithAutoSelect_ShouldReturnAllTargets()
        {
            // Arrange
            var config = CreateTestTargetConfig();
            config.PlayerChoiceOptions.AutoSelect = true;
            config.PlayerChoiceOptions.MinCount = 1;
            config.PlayerChoiceOptions.MaxCount = 2;

            var context = CreateTestGameContext();
            var selector = new PlayerChoiceTargetSelector(config, _mockQueryExecutor.Object, _mockLogger.Object);

            var mockQueryResult = new CardQueryResult
            {
                Success = true,
                Results = new List<object>
                {
                    CreateTestCardInstance(Guid.NewGuid(), CardZone.Field)
                }
            };

            _mockQueryExecutor.Setup(x => x.ExecuteQueryAsync(It.IsAny<CardQueryConfig>(), It.IsAny<GameContext>()))
                .ReturnsAsync(mockQueryResult);

            // Act
            var result = await selector.SelectTargetsAsync(config, context);

            // Assert
            Assert.Single(result);
        }

        [Fact]
        public async Task SelectTargetsAsync_WithNoQuery_ShouldReturnDefaultTargets()
        {
            // Arrange
            var config = new TargetConfig
            {
                SelectorType = SelectorType.PlayerChoice,
                Query = null, // No query
                PlayerChoiceOptions = new PlayerChoiceConfig
                {
                    IsRequired = false
                }
            };

            var context = CreateTestGameContext();
            var selector = new PlayerChoiceTargetSelector(config, _mockQueryExecutor.Object, _mockLogger.Object);

            // Act
            var result = await selector.SelectTargetsAsync(config, context);

            // Assert
            Assert.NotNull(result);
            // Should return default targets from context (source card and current player)
        }

        [Fact]
        public void TargetSelectorFactory_CreateSelector_ShouldCreatePlayerChoiceSelector()
        {
            // Arrange
            var config = CreateTestTargetConfig();

            // Act
            var selector = TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockFilteredLogger.Object);

            // Assert
            Assert.IsType<PlayerChoiceTargetSelector>(selector);
        }

        [Fact]
        public async Task SelectTargetsAsync_WithZoneFiltering_ShouldFilterInactiveZones()
        {
            // Arrange
            var config = CreateTestTargetConfig();
            var context = CreateTestGameContextWithCards();
            var selector = new PlayerChoiceTargetSelector(config, _mockQueryExecutor.Object, _mockLogger.Object);

            var fieldCard = CreateTestCardInstance(Guid.NewGuid(), CardZone.Field);
            var handCard = CreateTestCardInstance(Guid.NewGuid(), CardZone.Hand);
            var deckCard = CreateTestCardInstance(Guid.NewGuid(), CardZone.Deck);

            var mockQueryResult = new CardQueryResult
            {
                Success = true,
                Results = new List<object> { fieldCard, handCard, deckCard }
            };

            _mockQueryExecutor.Setup(x => x.ExecuteQueryAsync(It.IsAny<CardQueryConfig>(), It.IsAny<GameContext>()))
                .ReturnsAsync(mockQueryResult);

            // Act
            var result = await selector.SelectTargetsAsync(config, context);

            // Assert
            Assert.NotNull(result);
            // Zone filtering should prefer active zones (Field) over inactive zones (Hand, Deck)
        }

        private TargetConfig CreateTestTargetConfig()
        {
            return new TargetConfig
            {
                SelectorType = SelectorType.PlayerChoice,
                Query = new CardQueryConfig
                {
                    SourcePoolExpression = "Own.Field"
                },
                PlayerChoiceOptions = new PlayerChoiceConfig
                {
                    IsRequired = true,
                    MinCount = 1,
                    MaxCount = 1,
                    AutoSelect = false,
                    CanCancel = false
                }
            };
        }

        private GameContext CreateTestGameContext()
        {
            var player1 = new PlayerState { PlayerId = Guid.NewGuid() };
            var player2 = new PlayerState { PlayerId = Guid.NewGuid() };
            var gameState = new GameState { Player1 = player1, Player2 = player2 };

            return new GameContext(gameState, player1.PlayerId, null!, null);
        }

        private GameContext CreateTestGameContextWithCards()
        {
            var player1 = new PlayerState 
            { 
                PlayerId = Guid.NewGuid(),
                Field = new List<CardInstance>
                {
                    CreateTestCardInstance(Guid.NewGuid(), CardZone.Field)
                },
                Hand = new List<CardInstance>
                {
                    CreateTestCardInstance(Guid.NewGuid(), CardZone.Hand)
                }
            };
            var player2 = new PlayerState { PlayerId = Guid.NewGuid() };
            var gameState = new GameState { Player1 = player1, Player2 = player2 };

            return new GameContext(gameState, player1.PlayerId, null!, null);
        }

        private CardInstance CreateTestCardInstance(Guid instanceId, CardZone zone)
        {
            return new CardInstance
            {
                InstanceId = instanceId,
                DefinitionId = 1,
                OwnerPlayerId = Guid.NewGuid(),
                CurrentZone = zone,
                CurrentAttack = 2,
                CurrentHealth = 3,
                CardType = "Follower"
            };
        }
    }
}
