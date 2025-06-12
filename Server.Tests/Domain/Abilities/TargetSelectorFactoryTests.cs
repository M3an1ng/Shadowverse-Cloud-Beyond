using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using System;
using System.Text.Json;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Tests.Domain.Abilities
{
    /// <summary>
    /// 测试TargetSelectorFactory的TODO实现和新功能
    /// </summary>
    public class TargetSelectorFactoryTests
    {
        private readonly Mock<ICardQueryExecutor> _mockQueryExecutor;
        private readonly Mock<ILogger<FilteredTargetSelector>> _mockLogger;

        public TargetSelectorFactoryTests()
        {
            _mockQueryExecutor = new Mock<ICardQueryExecutor>();
            _mockLogger = new Mock<ILogger<FilteredTargetSelector>>();
            
            // 清空缓存以确保测试独立性
            TargetSelectorFactory.ClearCache();
        }

        [Fact]
        public void CreateSelectorFromJson_WithValidJson_ShouldCreateSelector()
        {
            // Arrange
            var config = new TargetConfig
            {
                SelectorType = SelectorType.Self
            };
            var json = JsonSerializer.Serialize(config);

            // Act
            var selector = TargetSelectorFactory.CreateSelectorFromJson(json, _mockQueryExecutor.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(selector);
            Assert.IsType<SelfTargetSelector>(selector);
        }

        [Fact]
        public void CreateSelectorFromJson_WithInvalidJson_ShouldThrowArgumentException()
        {
            // Arrange
            var invalidJson = "{ invalid json }";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                TargetSelectorFactory.CreateSelectorFromJson(invalidJson, _mockQueryExecutor.Object, _mockLogger.Object));
        }

        [Fact]
        public void CreateSelectorFromJson_WithNullJson_ShouldThrowArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                TargetSelectorFactory.CreateSelectorFromJson(null!, _mockQueryExecutor.Object, _mockLogger.Object));
        }

        [Fact]
        public void CreateSelectorFromJson_WithEmptyJson_ShouldThrowArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                TargetSelectorFactory.CreateSelectorFromJson("", _mockQueryExecutor.Object, _mockLogger.Object));
        }

        [Fact]
        public void CreateSelector_WithSelfType_ShouldCreateSelfTargetSelector()
        {
            // Arrange
            var config = new TargetConfig
            {
                SelectorType = SelectorType.Self
            };

            // Act
            var selector = TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(selector);
            Assert.IsType<SelfTargetSelector>(selector);
        }

        [Fact]
        public void CreateSelector_WithFilteredType_ShouldCreateFilteredTargetSelector()
        {
            // Arrange
            var config = new TargetConfig
            {
                SelectorType = SelectorType.Filtered,
                Query = new CardQueryConfig
                {
                    SourcePoolExpression = "Own.Field"
                }
            };

            // Act
            var selector = TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(selector);
            Assert.IsType<FilteredTargetSelector>(selector);
        }

        [Fact]
        public void CreateSelector_WithPlayerChoiceType_ShouldCreatePlayerChoiceTargetSelector()
        {
            // Arrange
            var config = new TargetConfig
            {
                SelectorType = SelectorType.PlayerChoice,
                PlayerChoiceOptions = new PlayerChoiceConfig
                {
                    IsRequired = true,
                    MinCount = 1,
                    MaxCount = 1
                }
            };

            // Act
            var selector = TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(selector);
            Assert.IsType<PlayerChoiceTargetSelector>(selector);
        }

        [Fact]
        public void CreateSelfSelector_WithoutDependencies_ShouldCreateSelfTargetSelector()
        {
            // Act
            var selector = TargetSelectorFactory.CreateSelfSelector();

            // Assert
            Assert.NotNull(selector);
            Assert.IsType<SelfTargetSelector>(selector);
        }

        [Fact]
        public void CreateSelfSelector_WithDependencies_ShouldCreateSelfTargetSelector()
        {
            // Act
            var selector = TargetSelectorFactory.CreateSelfSelector(_mockQueryExecutor.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(selector);
            Assert.IsType<SelfTargetSelector>(selector);
        }

        [Fact]
        public void CreateEnemyFieldSelector_ShouldCreateFilteredTargetSelector()
        {
            // Act
            var selector = TargetSelectorFactory.CreateEnemyFieldSelector(_mockQueryExecutor.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(selector);
            Assert.IsType<FilteredTargetSelector>(selector);
        }

        [Fact]
        public void CreateAllyFieldSelector_ShouldCreateFilteredTargetSelector()
        {
            // Act
            var selector = TargetSelectorFactory.CreateAllyFieldSelector(_mockQueryExecutor.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(selector);
            Assert.IsType<FilteredTargetSelector>(selector);
        }

        [Fact]
        public void CreateAllEnemyFieldSelector_ShouldCreateFilteredTargetSelector()
        {
            // Act
            var selector = TargetSelectorFactory.CreateAllEnemyFieldSelector(_mockQueryExecutor.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(selector);
            Assert.IsType<FilteredTargetSelector>(selector);
        }

        [Fact]
        public void CreateAllAllyFieldSelector_ShouldCreateFilteredTargetSelector()
        {
            // Act
            var selector = TargetSelectorFactory.CreateAllAllyFieldSelector(_mockQueryExecutor.Object, _mockLogger.Object);

            // Assert
            Assert.NotNull(selector);
            Assert.IsType<FilteredTargetSelector>(selector);
        }

        [Fact]
        public void CreateSelector_WithCaching_ShouldUseCacheForSameConfig()
        {
            // Arrange
            var config = new TargetConfig
            {
                SelectorType = SelectorType.Self
            };

            // Act
            var selector1 = TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockLogger.Object, useCache: true);
            var selector2 = TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockLogger.Object, useCache: true);

            // Assert
            Assert.NotNull(selector1);
            Assert.NotNull(selector2);
            Assert.Same(selector1, selector2); // Should be the same cached instance
            Assert.True(TargetSelectorFactory.CacheStatistics.CacheHits > 0);
        }

        [Fact]
        public void CreateSelector_WithoutCaching_ShouldCreateNewInstances()
        {
            // Arrange
            var config = new TargetConfig
            {
                SelectorType = SelectorType.Self
            };

            // Act
            var selector1 = TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockLogger.Object, useCache: false);
            var selector2 = TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockLogger.Object, useCache: false);

            // Assert
            Assert.NotNull(selector1);
            Assert.NotNull(selector2);
            Assert.NotSame(selector1, selector2); // Should be different instances
        }

        [Fact]
        public void ClearCache_ShouldResetCacheAndStatistics()
        {
            // Arrange
            var config = new TargetConfig
            {
                SelectorType = SelectorType.Self
            };
            
            // Create some cached selectors
            TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockLogger.Object, useCache: true);
            TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockLogger.Object, useCache: true);

            // Act
            TargetSelectorFactory.ClearCache();

            // Assert
            Assert.Equal(0, TargetSelectorFactory.CacheStatistics.CacheSize);
            Assert.Equal(0, TargetSelectorFactory.CacheStatistics.CacheHits);
            Assert.Equal(0, TargetSelectorFactory.CacheStatistics.CacheMisses);
        }

        [Fact]
        public void GetCacheReport_ShouldReturnValidReport()
        {
            // Arrange
            var config = new TargetConfig
            {
                SelectorType = SelectorType.Self
            };
            
            // Create some cache activity
            TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockLogger.Object, useCache: true);
            TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockLogger.Object, useCache: true);

            // Act
            var report = TargetSelectorFactory.GetCacheReport();

            // Assert
            Assert.NotNull(report);
            Assert.Contains("Cache Size:", report);
            Assert.Contains("Cache Hits:", report);
            Assert.Contains("Cache Misses:", report);
            Assert.Contains("Hit Rate:", report);
        }

        [Theory]
        [InlineData(SelectorType.Self)]
        [InlineData(SelectorType.Filtered)]
        public void CreateSelector_WithValidTypes_ShouldNotThrow(SelectorType selectorType)
        {
            // Arrange
            var config = new TargetConfig
            {
                SelectorType = selectorType,
                Query = selectorType == SelectorType.Filtered ? new CardQueryConfig
                {
                    SourcePoolExpression = "Own.Field"
                } : null
            };

            // Act & Assert
            var selector = TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, _mockLogger.Object);
            Assert.NotNull(selector);
        }

        [Fact]
        public void CreateSelector_WithNullConfig_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                TargetSelectorFactory.CreateSelector(null!, _mockQueryExecutor.Object, _mockLogger.Object));
        }

        [Fact]
        public void CreateSelector_WithNullQueryExecutor_ShouldThrowArgumentNullException()
        {
            // Arrange
            var config = new TargetConfig { SelectorType = SelectorType.Self };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                TargetSelectorFactory.CreateSelector(config, null!, _mockLogger.Object));
        }

        [Fact]
        public void CreateSelector_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange
            var config = new TargetConfig { SelectorType = SelectorType.Self };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                TargetSelectorFactory.CreateSelector(config, _mockQueryExecutor.Object, null!));
        }
    }
}
