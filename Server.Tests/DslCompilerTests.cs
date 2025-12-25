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
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST; // Added to fix compile error
using ShadowverseBeyond.Server.Domain.Buffs;

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
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            // Provide a stub IBuffService so GameContext can resolve it
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
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
    }
}
