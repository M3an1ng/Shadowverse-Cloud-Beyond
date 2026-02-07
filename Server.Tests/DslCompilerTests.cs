using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Services;
using Xunit;
using AutoMapper;
using Moq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos; // For PlayerStateDto, etc.
using ShadowverseBeyond.Server.Domain.Entities; // For CardInstance, etc.
using System.Collections;
using System.Linq;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST; // Added to fix compile error
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

using Microsoft.Extensions.Logging.Abstractions;

namespace ShadowverseBeyond.Server.Tests.DSL
{
    public class DslCompilerTests
    {
        private readonly Mock<ILogger<DslCompiler>> _mockLogger;
        private readonly Mock<IFunctionRegistry> _mockFunctionRegistry;
        private readonly DslCompiler _compiler;
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IDslExpressionCache> _mockDslCache;
        private readonly Mock<IGameDataProvider> _mockGameDataProvider;

        public DslCompilerTests()
        {
            _mockLogger = new Mock<ILogger<DslCompiler>>();
            _mockFunctionRegistry = new Mock<IFunctionRegistry>();
            _mockMapper = new Mock<IMapper>();
            _mockDslCache = new Mock<IDslExpressionCache>();
            _mockGameDataProvider = new Mock<IGameDataProvider>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            // Provide a stub IBuffService so GameContext can resolve it
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            _serviceProvider = services.BuildServiceProvider();

            _compiler = new DslCompiler(_mockLogger.Object, _mockFunctionRegistry.Object, _mockDslCache.Object, _mockGameDataProvider.Object);
        }

        [Fact]
        public void CompilePredicate_WithSimpleBooleanLiteral_ReturnsTrue()
        {
            // Arrange
            var dsl = "true";
            // GameContext now requires dependencies, which we mock.
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId);
            var context = new GameContext(_serviceProvider, _mockMapper.Object, _serviceProvider.GetRequiredService<IGameContextAccessor>(), gameState, player1.PlayerId);

            // Act
            var predicate = _compiler.CompilePredicate<object>(dsl);
            var result = predicate(null!, context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CompilePredicate_WithSimpleArithmetic_ReturnsCorrectResult()
        {
            // Arrange
            var dsl = "1 + 2 == 3";
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId);
            var context = new GameContext(_serviceProvider, _mockMapper.Object, _serviceProvider.GetRequiredService<IGameContextAccessor>(), gameState, player1.PlayerId);

            // Act
            var predicate = _compiler.CompilePredicate<object>(dsl);
            var result = predicate(null!, context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CompilePredicateWithTrace_CapturesMemberAccessValues()
        {
            // Arrange
            var dsl = "player.Hand.Count > 3";
            var context = CreateTestGameContext();
            var tsc = new TargetSelectionContext(context, null);
            var predicate = _compiler.CompilePredicateWithTrace<object>(dsl, tsc);
            var traceCollector = new DslTraceCollector();

            // Act
            var result = predicate(null!, traceCollector);

            // Assert
            Assert.False(result);
            Assert.True(traceCollector.TryGetValue("player.Hand.Count", out var traced));
            Assert.Equal("0", traced.Display);
        }

        [Fact]
        public void CompilePredicate_WithSelfPropertyAccess_ReturnsCorrectResult()
        {
            // Arrange
            var dsl = "self.Attack > 5";
            var cardWithHighAttack = new TestCard { Attack = 10 };
            var cardWithLowAttack = new TestCard { Attack = 3 };

            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId);
            var context = new GameContext(_serviceProvider, _mockMapper.Object, _serviceProvider.GetRequiredService<IGameContextAccessor>(), gameState, player1.PlayerId);

            // Act
            var predicate = _compiler.CompilePredicate<TestCard>(dsl);
            var resultForHighAttack = predicate(cardWithHighAttack, context);
            var resultForLowAttack = predicate(cardWithLowAttack, context);

            // Assert
            Assert.True(resultForHighAttack);
            Assert.False(resultForLowAttack);
        }

        [Theory]
        [InlineData("self.Attack + 2", 10, 0, 12)]
        [InlineData("self.Defense - 1", 0, 5, 4)]
        [InlineData("self.Cost * 2", 0, 3, 6)]
        [InlineData("self.Defense / 2", 0, 8, 4)]
        [InlineData("(self.Attack + self.Defense) * 2", 5, 10, 30)]
        public void CompileValue_WithArithmeticOperators_ReturnsCorrectResult(string dsl, int attack, int defenseOrCost, int expected)
        {
            // Arrange
            var card = new TestCard { Attack = attack, Defense = defenseOrCost, Cost = defenseOrCost };
            var context = CreateTestGameContext();
            var func = _compiler.CompileValue<TestCard, int>(dsl);

            // Act
            var result = func(card, context);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("self.Attack > 5 and self.Defense < 3", 10, 2, true)]
        [InlineData("self.Attack > 5 and self.Defense < 3", 10, 5, false)]
        [InlineData("self.Attack > 5 and self.Defense < 3", 4, 2, false)]
        [InlineData("self.Attack > 5 or self.Defense < 3", 10, 5, true)]
        [InlineData("self.Attack > 5 or self.Defense < 3", 4, 2, true)]
        [InlineData("self.Attack > 5 or self.Defense < 3", 4, 5, false)]
        [InlineData("not (self.Attack > 5)", 4, 0, true)]
        [InlineData("not (self.Attack > 5)", 10, 0, false)]
        public void CompilePredicate_WithLogicalOperators_ReturnsCorrectResult(string dsl, int attack, int defense, bool expected)
        {
            // Arrange
            var card = new TestCard { Attack = attack, Defense = defense };
            var context = CreateTestGameContext();
            var predicate = _compiler.CompilePredicate<TestCard>(dsl);

            // Act
            var result = predicate(card, context);

            // Assert
            Assert.Equal(expected, result);
        }

        private GameContext CreateTestGameContext()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId);
            return new GameContext(_serviceProvider, _mockMapper.Object, _serviceProvider.GetRequiredService<IGameContextAccessor>(), gameState, player1.PlayerId);
        }
        // Helper class for testing 'self' context
        public class TestCard
        {
            public int Attack { get; set; }
            public int Defense { get; set; }
            public int Cost { get; set; }
        }
        [Fact]
        public void Compile_WithFunctionCall_ReturnsCorrectResult()
        {
            // Arrange
            var dsl = "Count(self.Cards) > 1";
            var mockFunction = new Mock<IFunctionDefinition>();

            // Setup the mock function's Execute method to behave like a 'Count' function
            mockFunction.Setup(f => f.Execute(It.IsAny<List<object?>>(), It.IsAny<GameContext>()))
                        .Returns((List<object?> args, GameContext ctx) =>
                        {
                            if (args.FirstOrDefault() is ICollection collection)
                            {
                                return collection.Count;
                            }
                            return 0;
                        });

            // Crucial: Setup the ReturnType property for the mock function
            mockFunction.Setup(f => f.ReturnType).Returns(typeof(int));

            _mockFunctionRegistry.Setup(r => r.GetFunction("Count")).Returns(mockFunction.Object);

            var cardList1 = new TestCardList { Cards = new List<TestCard> { new TestCard(), new TestCard() } }; // Count = 2
            var cardList2 = new TestCardList { Cards = new List<TestCard> { new TestCard() } }; // Count = 1
            var context = CreateTestGameContext();

            // Act
            var predicate = _compiler.CompilePredicate<TestCardList>(dsl);
            var result1 = predicate(cardList1, context);
            var result2 = predicate(cardList2, context);

            // Assert
            Assert.True(result1);
            Assert.False(result2);
        }

        // Helper class for function call test
        public class TestCardList
        {
            public List<TestCard> Cards { get; set; } = new List<TestCard>();
        }
        [Fact]
        public void Parser_ShouldParseMemberAccessCorrectly()
        {
            // Arrange
            var dsl = "self.CurrentAttack > 5";
            var lexer = new Lexer(dsl);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);

            // Act
            var ast = parser.ParseSingleExpression();

            // Assert
            // Root node should be a binary operation (>)
            var binaryOp = Assert.IsType<BinaryOperationNode>(ast);
            Assert.Equal(TokenType.GreaterThan, binaryOp.Operator);

            // Left side should be a member access (self.CurrentAttack)
            var memberAccess = Assert.IsType<MemberAccessNode>(binaryOp.Left);

            // The target of the member access should be the identifier 'self'
            var targetIdentifier = Assert.IsType<IdentifierNode>(memberAccess.Target);
            Assert.Single(targetIdentifier.Path);
            Assert.Equal("self", targetIdentifier.Path[0]);

            // The member being accessed should be 'CurrentAttack'
            var memberIdentifier = memberAccess.Member;
            Assert.Equal("CurrentAttack", memberIdentifier.Name);

            // Right side should be the literal value 5
            var literal = Assert.IsType<LiteralNode>(binaryOp.Right);
            Assert.Equal(5, literal.Value); // Parser parses numbers as int
        }

        [Fact]
        public void CompilePredicate_WithComplexContextAccess_ReturnsCorrectResult()
        {
            // Arrange
            var dsl = "self.Attack > player.Hand.Count";
            var cardWithHighAttack = new TestCard { Attack = 10 };
            var cardWithLowAttack = new TestCard { Attack = 3 };

            var player1 = new PlayerState(Guid.NewGuid());
            // Add 5 dummy cards to the player's hand to make Hand.Count = 5
            for (int i = 0; i < 5; i++)
            {
                player1.Hand.Add(new CardInstance(Guid.NewGuid(), i + 1, player1.PlayerId));
            }

            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId);
            var context = new GameContext(_serviceProvider, _mockMapper.Object, _serviceProvider.GetRequiredService<IGameContextAccessor>(), gameState, player1.PlayerId);

            // Act
            var predicate = _compiler.CompilePredicate<TestCard>(dsl);
            var resultForHighAttack = predicate(cardWithHighAttack, context);
            var resultForLowAttack = predicate(cardWithLowAttack, context);

            // Assert
            Assert.True(resultForHighAttack, "Expected true because Attack (10) > Hand.Count (5)");
            Assert.False(resultForLowAttack, "Expected false because Attack (3) <= Hand.Count (5)");
        }

        [Fact]
        public void CompilePredicate_WithTriggeringEventMemberAccess_ReturnsCorrectResult()
        {
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId);

            var sourceCard = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId);
            var targetCard = new CardInstance(Guid.NewGuid(), 2, player2.PlayerId);
            var triggeringEvent = new DamageEvent(targetCard, 3, sourceCard, DamageKind.Combat);

            var context = new GameContext(
                _serviceProvider,
                _mockMapper.Object,
                _serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                player1.PlayerId,
                triggeringEvent: triggeringEvent);

            var ownerPredicate = _compiler.CompilePredicate<object>(
                "context.triggeringEvent.DamageTarget.OwnerPlayerId == opponent.PlayerId");
            var kindPredicate = _compiler.CompilePredicate<object>(
                "context.triggeringEvent.Kind == \"Combat\"");

            Assert.True(ownerPredicate(null!, context));
            Assert.True(kindPredicate(null!, context));
        }

        [Fact]
        public void CompileGetter_WithQueryPipeline_ReturnsFilteredAndSortedResults()
        {
            var cards = new List<TestCard>
            {
                new TestCard { Attack = 1 },
                new TestCard { Attack = 5 },
                new TestCard { Attack = 3 }
            };

            var context = CreateTestGameContext();
            var getter = _compiler.CompileGetter<List<TestCard>>(
                "self.query(self.Attack >= 3).orderBy(self.Attack desc).take(2)");

            var result = getter(cards, context);
            var list = Assert.IsAssignableFrom<IEnumerable<TestCard>>(result).ToList();

            Assert.Equal(2, list.Count);
            Assert.Equal(5, list[0].Attack);
            Assert.Equal(3, list[1].Attack);
        }

        [Fact]
        public void CompileGetter_WithDistinctByKey_ReturnsFirstPerKey()
        {
            var cards = new List<TestCard>
            {
                new TestCard { Attack = 1 },
                new TestCard { Attack = 1 },
                new TestCard { Attack = 3 }
            };

            var context = CreateTestGameContext();
            var getter = _compiler.CompileGetter<List<TestCard>>("self.distinct(self.Attack)");

            var result = getter(cards, context);
            var list = Assert.IsAssignableFrom<IEnumerable<TestCard>>(result).ToList();

            Assert.Equal(2, list.Count);
            Assert.Equal(1, list[0].Attack);
            Assert.Equal(3, list[1].Attack);
        }

        [Fact]
        public void CompileGetter_WithDistinctEntityKey_Throws()
        {
            var cards = new List<TestCard>
            {
                new TestCard { Attack = 1 },
                new TestCard { Attack = 2 }
            };

            var context = CreateTestGameContext();
            var getter = _compiler.CompileGetter<List<TestCard>>("self.distinct(self)");

            Assert.Throws<InvalidOperationException>(() =>
            {
                var result = getter(cards, context);
                _ = Assert.IsAssignableFrom<IEnumerable<TestCard>>(result).ToList();
            });
        }

        [Fact]
        public void CompileGetter_WithTakeLast_ReturnsLastItem()
        {
            var cards = new List<TestCard>
            {
                new TestCard { Attack = 1 },
                new TestCard { Attack = 2 },
                new TestCard { Attack = 3 }
            };

            var context = CreateTestGameContext();
            var getter = _compiler.CompileGetter<List<TestCard>>("self.take(last 1)");

            var result = getter(cards, context);
            var list = Assert.IsAssignableFrom<IEnumerable<TestCard>>(result).ToList();

            Assert.Single(list);
            Assert.Equal(3, list[0].Attack);
        }

        [Fact]
        public void CompileGetter_WithSetOperators_ReturnsExpectedResults()
        {
            // Arrange
            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId);
            var context = new GameContext(_serviceProvider, _mockMapper.Object, _serviceProvider.GetRequiredService<IGameContextAccessor>(), gameState, player1.PlayerId);

            var sharedId = Guid.NewGuid();
            var cardA = new CardInstance(Guid.NewGuid(), 1, player1.PlayerId);
            var cardB = new CardInstance(Guid.NewGuid(), 2, player1.PlayerId);
            var cardOverlapLeft = new CardInstance(sharedId, 3, player1.PlayerId);
            var cardOverlapRight = new CardInstance(sharedId, 4, player1.PlayerId);

            player1.Hand.Add(cardA);
            player1.Hand.Add(cardOverlapLeft);
            player1.Field.Add(cardB);
            player1.Field.Add(cardOverlapRight);

            // Act
            var unionGetter = _compiler.CompileGetter<object>("player.Hand | player.Field");
            var unionResult = unionGetter(null!, context);

            var intersectGetter = _compiler.CompileGetter<object>("player.Hand & player.Field");
            var intersectResult = intersectGetter(null!, context);

            var diffGetter = _compiler.CompileGetter<object>("player.Hand ~ player.Field");
            var diffResult = diffGetter(null!, context);

            // Assert
            var unionList = Assert.IsAssignableFrom<IEnumerable<CardInstance>>(unionResult).ToList();
            Assert.Equal(3, unionList.Count);

            var intersectList = Assert.IsAssignableFrom<IEnumerable<CardInstance>>(intersectResult).ToList();
            Assert.Single(intersectList);
            Assert.Equal(sharedId, intersectList[0].InstanceId);

            var diffList = Assert.IsAssignableFrom<IEnumerable<CardInstance>>(diffResult).ToList();
            Assert.Single(diffList);
            Assert.Equal(cardA.InstanceId, diffList[0].InstanceId);
        }

        [Fact]
        public void CompileGetter_WithSetOperator_SingletonOperand_IsAutoBoxed()
        {
            var context = CreateTestGameContext();
            var player = context.GetRequestingPlayer();

            var leader = new CardInstance(Guid.NewGuid(), 1, player.PlayerId);
            player.Leader = leader;

            var handCard = new CardInstance(Guid.NewGuid(), 2, player.PlayerId);
            player.Hand.Add(handCard);

            var unionGetter = _compiler.CompileGetter<object>("player.Hand | player.Leader");
            var unionResult = unionGetter(null!, context);

            var unionEnumerable = Assert.IsAssignableFrom<IEnumerable>(unionResult);
            var unionList = unionEnumerable.Cast<CardInstance>().ToList();

            Assert.Equal(2, unionList.Count);
            Assert.Contains(unionList, card => card.InstanceId == leader.InstanceId);
            Assert.Contains(unionList, card => card.InstanceId == handCard.InstanceId);
        }

        [Fact]
        public void CompileGetterRuntime_ContextOnly_WithSelfReference_Throws()
        {
            var context = CreateTestGameContext();
            var tsc = new TargetSelectionContext(context, null);

            Assert.Throws<InvalidOperationException>(() =>
                _compiler.CompileGetterRuntime(
                    "self.Attack",
                    tsc,
                    selfType: null,
                    IDslCompiler.SelfResolutionMode.ContextOnly));
        }

        [Fact]
        public void CompileGetterRuntime_ItemOptional_WithRuntimeType_ResolvesSelf()
        {
            var context = CreateTestGameContext();
            var tsc = new TargetSelectionContext(context, null);
            var getter = _compiler.CompileGetterRuntime(
                "self.Attack + 1",
                tsc,
                typeof(TestCard),
                IDslCompiler.SelfResolutionMode.ItemOptional);

            var value = getter(new TestCard { Attack = 4 });

            Assert.Equal(5, Convert.ToInt32(value));
        }

        [Fact]
        public void CompilePredicateWithTraceRuntime_ContextOnly_NoSelf_Works()
        {
            var context = CreateTestGameContext();
            var tsc = new TargetSelectionContext(context, null);
            var traceCollector = new DslTraceCollector();
            var predicate = _compiler.CompilePredicateWithTraceRuntime(
                "player.Hand.Count == 0",
                tsc,
                selfType: null,
                IDslCompiler.SelfResolutionMode.ContextOnly);

            var result = predicate(null!, traceCollector);

            Assert.True(result);
            Assert.True(traceCollector.TryGetValue("player.Hand.Count", out _));
        }

        [Fact]
        public void CompileGetterRuntime_ContextOnly_AllowsSelfInsideQueryScope()
        {
            var context = CreateTestGameContext();
            var player = context.GetRequestingPlayer();
            player.Hand.Add(new CardInstance(Guid.NewGuid(), 1, player.PlayerId));

            var tsc = new TargetSelectionContext(context, null);
            var getter = _compiler.CompileGetterRuntime(
                "player.Hand.query(self.OwnerPlayerId == player.PlayerId).take(first 1)",
                tsc,
                selfType: null,
                IDslCompiler.SelfResolutionMode.ContextOnly);

            var result = getter(null!);
            var list = Assert.IsAssignableFrom<IEnumerable>(result).Cast<object>().ToList();

            Assert.Single(list);
        }
    }
}
