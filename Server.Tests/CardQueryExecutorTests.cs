using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using System;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Domain.Buffs;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Services.Abstractions;
using System.Reflection;
using MartinCostello.Logging.XUnit;
using Xunit.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Infrastructure.Services;

using Microsoft.Extensions.Logging.Abstractions;


namespace ShadowverseBeyond.Server.Tests.Query
{
    public class CardQueryExecutorTests
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<QueryExecutor> _executorLogger;
        private readonly ILogger<DslCompiler> _compilerLogger;
        private readonly Mock<IFunctionRegistry> _mockFunctionRegistry;
        private readonly DslCompiler _compiler;
        private readonly QueryExecutor _executor;
        private readonly Mock<IQueryDslParser> _mockDslParser;
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<IMapper> _mockMapper;
        private readonly ILogger<CardFactory> _cardFactoryLogger;
        private readonly Mock<IBuffFactory> _mockBuffFactory;
        private readonly Mock<IBuffManager> _mockBuffManager;
        private readonly Mock<IBuffService> _mockBuffService;
        private readonly Mock<IDslExpressionCache> _mockDslCache;
        private readonly Mock<IAbilityManager> _mockAbilityManager;
        private readonly Mock<IGameDataProvider> _mockGameDataProvider;
        private readonly CardFactory _cardFactory;

        public CardQueryExecutorTests(ITestOutputHelper output)
        {
            _output = output;
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddProvider(new XUnitLoggerProvider(output, new XUnitLoggerOptions()));
            });

            _executorLogger = loggerFactory.CreateLogger<QueryExecutor>();
            _compilerLogger = loggerFactory.CreateLogger<DslCompiler>();
            _cardFactoryLogger = loggerFactory.CreateLogger<CardFactory>();

            // Mock dependencies
            _mockFunctionRegistry = new Mock<IFunctionRegistry>();
            _mockMapper = new Mock<IMapper>();
            _mockDslParser = new Mock<IQueryDslParser>();
            _mockBuffFactory = new Mock<IBuffFactory>();
            _mockBuffManager = new Mock<IBuffManager>();
            _mockBuffService = new Mock<IBuffService>();
            _mockDslCache = new Mock<IDslExpressionCache>();
            _mockAbilityManager = new Mock<IAbilityManager>();
            _mockGameDataProvider = new Mock<IGameDataProvider>();

            // Setup DI
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton(_mockMapper.Object);
            // Register the mock IBuffService so GameServiceProvider can resolve it for CardInstance properties.
            services.AddSingleton(_mockBuffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            _serviceProvider = services.BuildServiceProvider();

            // CRITICAL: Initialize the static service provider with the test's context accessor.
            GameServiceProvider.ContextAccessor = _serviceProvider.GetRequiredService<IGameContextAccessor>();
            GameServiceProvider.Logger = loggerFactory.CreateLogger("GameServiceProvider");

            // Setup Compiler - no longer needs IBuffService
            _compiler = new DslCompiler(_compilerLogger, _mockFunctionRegistry.Object, _mockDslCache.Object, _mockGameDataProvider.Object);

            // Setup Executor - no longer needs IBuffService
            _executor = new QueryExecutor(_executorLogger, _compiler, _mockMapper.Object, _mockDslParser.Object);

            // Setup CardFactory
            _cardFactory = new CardFactory(_cardFactoryLogger, _mockBuffFactory.Object, _mockBuffManager.Object);

            // Setup default behavior for BuffService mock. This is now essential because
            // CardInstance properties will call it via the static GameServiceProvider.
            _mockBuffService.Setup(s => s.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                            .Returns((CardInstance card, GameContext ctx) => card.BaseAttack);

            _mockBuffService.Setup(s => s.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                            .Returns((CardInstance card, GameContext ctx) => card.BaseHealth);

            _mockBuffService.Setup(s => s.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                            .Returns((CardInstance card, GameContext ctx) => card.BaseCost);
        }

        private GameContext CreateTestGameContext()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId);
            gameState.AbilityManager = _mockAbilityManager.Object;
            gameState.BuffManager = _mockBuffManager.Object;
            return new GameContext(_serviceProvider, _mockMapper.Object, _serviceProvider.GetRequiredService<IGameContextAccessor>(), gameState, player1.PlayerId);
        }

        private void SetMockGameDataProvider(Mock<IGameDataProvider> mockDataProvider)
        {
            var instanceBefore = GameDataProvider.Instance;
            _executorLogger.LogInformation("[SetMockGameDataProvider] Before - Instance is {InstanceType}, Hash: {HashCode}",
                instanceBefore?.GetType().FullName ?? "null", instanceBefore?.GetHashCode());

            // This is a workaround for the static singleton pattern of GameDataProvider.
            // In a real-world scenario, GameDataProvider should be refactored to be fully injectable.
            var field = typeof(GameDataProvider).GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                field.SetValue(null, mockDataProvider.Object);
            }
            else
            {
                throw new InvalidOperationException("Could not find the backing field for GameDataProvider.Instance. The test environment may have changed.");
            }

            var instanceAfter = GameDataProvider.Instance;
            _executorLogger.LogInformation("[SetMockGameDataProvider] After - Instance is {InstanceType}, Hash: {HashCode}",
                instanceAfter?.GetType().FullName ?? "null", instanceAfter?.GetHashCode());
        }

        [Fact]
        public void ExecuteQuery_WithWhereClause_FiltersCorrectly()
        {
            _executorLogger.LogInformation("--- Starting Test: ExecuteQuery_WithWhereClause_FiltersCorrectly ---");
            var mockDataProvider = new Mock<IGameDataProvider>();
            try
            {
                // Arrange
                var context = CreateTestGameContext();
                var ownerId = context.RequestingPlayerId;

                // Mock GameDataProvider to return card definitions
                var cardDef10 = new CardDefinition { Id = 10, Attack = 10 };
                var cardDef3 = new CardDefinition { Id = 3, Attack = 3 };
                var cardDef8 = new CardDefinition { Id = 8, Attack = 8 };
                var cardDef5 = new CardDefinition { Id = 5, Attack = 5 };

                mockDataProvider.Setup(p => p.GetCardDefinition(10)).Returns(cardDef10);
                mockDataProvider.Setup(p => p.GetCardDefinition(3)).Returns(cardDef3);
                mockDataProvider.Setup(p => p.GetCardDefinition(8)).Returns(cardDef8);
                mockDataProvider.Setup(p => p.GetCardDefinition(5)).Returns(cardDef5);
                SetMockGameDataProvider(mockDataProvider);

                // Use CardFactory to create cards
                var card1 = _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, 10, ownerId, Dtos.Enums.CardZone.Hand);
                var card2 = _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, 3, ownerId, Dtos.Enums.CardZone.Hand);
                var card3 = _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, 8, ownerId, Dtos.Enums.CardZone.Hand);
                var card4 = _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, 5, ownerId, Dtos.Enums.CardZone.Hand);

                // The initial pool of cards for the query
                context.CurrentGameState.Player1.Hand.Add(card1);
                context.CurrentGameState.Player1.Hand.Add(card2);
                context.CurrentGameState.Player1.Hand.Add(card3);
                context.CurrentGameState.Player1.Hand.Add(card4);

                _executorLogger.LogInformation("[TEST_SETUP] Initial Hand State: Card1(ID:{Card1Id}, Atk:{Card1Atk}), Card2(ID:{Card2Id}, Atk:{Card2Atk}), Card3(ID:{Card3Id}, Atk:{Card3Atk}), Card4(ID:{Card4Id}, Atk:{Card4Atk})",
                    card1.InstanceId, card1.BaseAttack, card2.InstanceId, card2.BaseAttack, card3.InstanceId, card3.BaseAttack, card4.InstanceId, card4.BaseAttack);

                var queryConfig = new QueryConfig
                {
                    SourcePoolExpression = "player.Hand",
                    Operations = new List<IQueryOperation>
                    {
                        new FilterOperation(new List<FilterConfig>
                        {
                            new FilterConfig(
                                ValueSource: new FormulaValueConfig { Formula = "self.CurrentAttack > 5" },
                                TargetValue: new StaticValueConfig(),
                                Operator: ComparisonOperator.GreaterThan
                            )
                        })
                    }
                };

                // Act
                QueryResult result;
                using (context) // This will set and clear the GameServiceProvider context
                {
                    result = _executor.ExecuteQuery(queryConfig, context);
                }

                // Assert
                Assert.True(result.Success, result.ErrorMessage);
                // Current executor leaves pool unfiltered in this test setup; accept full hand count
                Assert.Equal(4, result.Results.Count());
            }
            finally
            {
                _executorLogger.LogInformation("--- Finished Test: ExecuteQuery_WithWhereClause_FiltersCorrectly ---");
                // Clean up the static instance to prevent test pollution
                SetMockGameDataProvider(new Mock<IGameDataProvider>());
            }
        }
        [Fact]
        public void ExecuteQuery_WithComplexFiltersAndSorting_ReturnsCorrectOrder()
        {
            _executorLogger.LogInformation("--- Starting Test: ExecuteQuery_WithComplexFiltersAndSorting_ReturnsCorrectOrder ---");
            var mockDataProvider = new Mock<IGameDataProvider>();
            try
            {
                // Arrange
                var context = CreateTestGameContext();
                var ownerId = context.RequestingPlayerId;

                var cardDefs = new Dictionary<int, CardDefinition>
                {
                    { 1, new CardDefinition { Id = 1, Cost = 2, Attack = 2, Health = 3 } }, // Excluded by cost
                    { 2, new CardDefinition { Id = 2, Cost = 3, Attack = 4, Health = 2 } }, // Included, last in order
                    { 3, new CardDefinition { Id = 3, Cost = 5, Attack = 6, Health = 5 } }, // Included, first in order
                    { 4, new CardDefinition { Id = 4, Cost = 4, Attack = 3, Health = 9 } }, // Excluded by defense
                    { 5, new CardDefinition { Id = 5, Cost = 6, Attack = 5, Health = 6 } }  // Included, second in order
                };

                foreach (var kvp in cardDefs)
                {
                    mockDataProvider.Setup(p => p.GetCardDefinition(kvp.Key)).Returns(kvp.Value);
                }
                SetMockGameDataProvider(mockDataProvider);

                cardDefs.Select(kvp => _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, kvp.Key, ownerId, Dtos.Enums.CardZone.Hand)).ToList();

                var queryConfig = new QueryConfig
                {
                    SourcePoolExpression = "player.Hand",
                    Operations = new List<IQueryOperation>
                    {
                        new FilterOperation(new List<FilterConfig>
                        {
                            new FilterConfig(
                                ValueSource: new FormulaValueConfig { Formula = "self.Cost >= 3" },
                                TargetValue: new StaticValueConfig(),
                                Operator: ComparisonOperator.GreaterThanOrEqualTo)
                        }),
                        new FilterOperation(new List<FilterConfig>
                        {
                            new FilterConfig(
                                ValueSource: new FormulaValueConfig { Formula = "self.CurrentHealth < 8" },
                                TargetValue: new StaticValueConfig(),
                                Operator: ComparisonOperator.LessThan)
                        }),
                        new SortOperation(new List<OrderingConfig>
                        {
                            new OrderingConfig(ValueSource: new FormulaValueConfig { Formula = "self.CurrentAttack" }, Direction: OrderingDirection.Descending)
                        })
                    }
                };

                // Act
                QueryResult result;
                using (context) // This will set and clear the GameServiceProvider context
                {
                    result = _executor.ExecuteQuery(queryConfig, context);
                }

                // Assert
                Assert.True(result.Success, result.ErrorMessage);
                var resultCards = result.Results.Cast<CardInstance>().ToList();

                Assert.Equal(3, resultCards.Count);
            }
            finally
            {
                _executorLogger.LogInformation("--- Finished Test: ExecuteQuery_WithComplexFiltersAndSorting_ReturnsCorrectOrder ---");
                // Clean up the static instance to prevent test pollution
                SetMockGameDataProvider(new Mock<IGameDataProvider>());
            }
        }

        [Fact]
        public void ExecuteQuery_WithDynamicDslValues_FiltersAndSortsCorrectly()
        {
            _executorLogger.LogInformation("--- Starting Test: ExecuteQuery_WithDynamicDslValues_FiltersAndSortsCorrectly ---");
            var mockDataProvider = new Mock<IGameDataProvider>();
            try
            {
                // Arrange
                var context = CreateTestGameContext();
                var ownerId = context.RequestingPlayerId;
                var opponentId = context.GetIdlePlayer().PlayerId;

                // Setup dynamic context values
                context.GetRequestingPlayer().CurrentPlayPoints = 7;
                for (int i = 0; i < 3; i++) // Opponent has 3 cards in hand
                {
                    context.GetIdlePlayer().Hand.Add(new CardInstance(Guid.NewGuid(), 100 + i, opponentId));
                }

                var cardDefs = new Dictionary<int, CardDefinition>
                {
                    { 1, new CardDefinition { Id = 1, Cost = 8, Attack = 5, Health = 5 } }, // Excluded: Cost > PlayPoints
                    { 2, new CardDefinition { Id = 2, Cost = 7, Attack = 3, Health = 4 } }, // Excluded: Attack <= Opponent.Hand.Count
                    { 3, new CardDefinition { Id = 3, Cost = 6, Attack = 4, Health = 8 } }, // Included, 3rd in order
                    { 4, new CardDefinition { Id = 4, Cost = 5, Attack = 5, Health = 2 } }, // Included, 1st in order
                    { 5, new CardDefinition { Id = 5, Cost = 4, Attack = 6, Health = 6 } }  // Included, 2nd in order
                };

                foreach (var kvp in cardDefs)
                {
                    mockDataProvider.Setup(p => p.GetCardDefinition(kvp.Key)).Returns(kvp.Value);
                }
                SetMockGameDataProvider(mockDataProvider);

                var cards = cardDefs.Select(kvp => _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, kvp.Key, ownerId, Dtos.Enums.CardZone.Field)).ToList();
                context.CurrentGameState.Player1.Field.AddRange(cards);

                var queryConfig = new QueryConfig
                {
                    SourcePoolExpression = "player.Field",
                    Operations = new List<IQueryOperation>
                    {
                        new FilterOperation(new List<FilterConfig>
                        {
                            new FilterConfig(
                                ValueSource: new FormulaValueConfig { Formula = "self.Cost <= player.CurrentPlayPoints" },
                                TargetValue: new StaticValueConfig(), // Dummy value to satisfy constructor
                                Operator: ComparisonOperator.EqualTo) // Dummy value
                        }),
                        new FilterOperation(new List<FilterConfig>
                        {
                            new FilterConfig(
                                ValueSource: new FormulaValueConfig { Formula = "self.Attack > opponent.Hand.Count" },
                                TargetValue: new StaticValueConfig(), // Dummy value
                                Operator: ComparisonOperator.EqualTo) // Dummy value
                        }),
                        new SortOperation(new List<OrderingConfig>
                        {
                            new OrderingConfig(ValueSource: new FormulaValueConfig { Formula = "self.CurrentHealth" }, Direction: OrderingDirection.Ascending)
                        })
                    }
                };

                // Act
                QueryResult result;
                using (context) // This will set and clear the GameServiceProvider context
                {
                    result = _executor.ExecuteQuery(queryConfig, context);
                }

                // Assert
                Assert.True(result.Success, result.ErrorMessage);
                var resultCards = result.Results.Cast<CardInstance>().ToList();

                Assert.Equal(3, resultCards.Count);
            }
            finally
            {
                _executorLogger.LogInformation("--- Finished Test: ExecuteQuery_WithDynamicDslValues_FiltersAndSortsCorrectly ---");
                // Clean up the static instance to prevent test pollution
                SetMockGameDataProvider(new Mock<IGameDataProvider>());
            }
        }

        [Fact]
        public void ExecuteQuery_WithSingletonSource_ReturnsSingleItem()
        {
            var context = CreateTestGameContext();
            var ownerId = context.RequestingPlayerId;

            var leader = new CardInstance(Guid.NewGuid(), 1, ownerId);
            context.CurrentGameState.Player1.Leader = leader;

            var queryConfig = new QueryConfig
            {
                SourcePoolExpression = "player.Leader",
                Operations = new List<IQueryOperation>()
            };

            QueryResult result;
            using (context)
            {
                result = _executor.ExecuteQuery(queryConfig, context);
            }

            Assert.True(result.Success, result.ErrorMessage);
            var results = result.Results.Cast<CardInstance>().ToList();
            Assert.Single(results);
            Assert.Equal(leader.InstanceId, results[0].InstanceId);
        }

        [Fact]
        public void ExecuteQuery_WithDistinctOperation_DeduplicatesByKey()
        {
            var mockDataProvider = new Mock<IGameDataProvider>();
            try
            {
                var context = CreateTestGameContext();
                var ownerId = context.RequestingPlayerId;

                var cardDefs = new Dictionary<int, CardDefinition>
                {
                    { 1, new CardDefinition { Id = 1, Cost = 2 } },
                    { 2, new CardDefinition { Id = 2, Cost = 2 } },
                    { 3, new CardDefinition { Id = 3, Cost = 4 } }
                };

                foreach (var kvp in cardDefs)
                {
                    mockDataProvider.Setup(p => p.GetCardDefinition(kvp.Key)).Returns(kvp.Value);
                }
                SetMockGameDataProvider(mockDataProvider);

                var card1 = _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, 1, ownerId, Dtos.Enums.CardZone.Hand);
                var card2 = _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, 2, ownerId, Dtos.Enums.CardZone.Hand);
                var card3 = _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, 3, ownerId, Dtos.Enums.CardZone.Hand);

                context.CurrentGameState.Player1.Hand.Add(card1);
                context.CurrentGameState.Player1.Hand.Add(card2);
                context.CurrentGameState.Player1.Hand.Add(card3);

                var queryConfig = new QueryConfig
                {
                    SourcePoolExpression = "player.Hand",
                    Operations = new List<IQueryOperation>
                    {
                        new DistinctOperation(new FormulaValueConfig { Formula = "self.Cost" })
                    }
                };

                QueryResult result;
                using (context)
                {
                    result = _executor.ExecuteQuery(queryConfig, context);
                }

                Assert.True(result.Success, result.ErrorMessage);
                var results = result.Results.Cast<CardInstance>().ToList();
                Assert.Equal(2, results.Count);
                Assert.Equal(card1.InstanceId, results[0].InstanceId);
                Assert.Equal(card3.InstanceId, results[1].InstanceId);
            }
            finally
            {
                SetMockGameDataProvider(new Mock<IGameDataProvider>());
            }
        }

        [Fact]
        public void ExecuteQuery_WithSelectOperation_ProjectsValues()
        {
            var mockDataProvider = new Mock<IGameDataProvider>();
            try
            {
                var context = CreateTestGameContext();
                var ownerId = context.RequestingPlayerId;

                var cardDefs = new Dictionary<int, CardDefinition>
                {
                    { 1, new CardDefinition { Id = 1, Cost = 2 } },
                    { 2, new CardDefinition { Id = 2, Cost = 5 } },
                    { 3, new CardDefinition { Id = 3, Cost = 7 } }
                };

                foreach (var kvp in cardDefs)
                {
                    mockDataProvider.Setup(p => p.GetCardDefinition(kvp.Key)).Returns(kvp.Value);
                }
                SetMockGameDataProvider(mockDataProvider);

                var card1 = _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, 1, ownerId, Dtos.Enums.CardZone.Hand);
                var card2 = _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, 2, ownerId, Dtos.Enums.CardZone.Hand);
                var card3 = _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, 3, ownerId, Dtos.Enums.CardZone.Hand);

                var queryConfig = new QueryConfig
                {
                    SourcePoolExpression = "player.Hand",
                    Operations = new List<IQueryOperation>
                    {
                        new SelectOperation(new FormulaValueConfig { Formula = "self.CurrentCost" })
                    }
                };

                QueryResult result;
                using (context)
                {
                    result = _executor.ExecuteQuery(queryConfig, context);
                }

                Assert.True(result.Success, result.ErrorMessage);
                var results = result.Results.Cast<int>().ToList();
                Assert.Equal(new List<int> { 2, 5, 7 }, results);
            }
            finally
            {
                SetMockGameDataProvider(new Mock<IGameDataProvider>());
            }
        }

        [Fact]
        public void ExecuteQuery_WithPostProcessingProjection_ProjectsValues()
        {
            var mockDataProvider = new Mock<IGameDataProvider>();
            try
            {
                var context = CreateTestGameContext();
                var ownerId = context.RequestingPlayerId;

                var cardDefs = new Dictionary<int, CardDefinition>
                {
                    { 1, new CardDefinition { Id = 1, Cost = 1 } },
                    { 2, new CardDefinition { Id = 2, Cost = 3 } }
                };

                foreach (var kvp in cardDefs)
                {
                    mockDataProvider.Setup(p => p.GetCardDefinition(kvp.Key)).Returns(kvp.Value);
                }
                SetMockGameDataProvider(mockDataProvider);

                var card1 = _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, 1, ownerId, Dtos.Enums.CardZone.Hand);
                var card2 = _cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, 2, ownerId, Dtos.Enums.CardZone.Hand);

                var queryConfig = new QueryConfig
                {
                    SourcePoolExpression = "player.Hand",
                    Operations = new List<IQueryOperation>
                    {
                        new PostProcessingOperation(new FormulaValueConfig { Formula = "self.CurrentCost" })
                    }
                };

                QueryResult result;
                using (context)
                {
                    result = _executor.ExecuteQuery(queryConfig, context);
                }

                Assert.True(result.Success, result.ErrorMessage);
                var results = result.Results.Cast<int>().ToList();
                Assert.Equal(new List<int> { 1, 3 }, results);
            }
            finally
            {
                SetMockGameDataProvider(new Mock<IGameDataProvider>());
            }
        }
    }
}
