using System.Collections.Generic;
using System.Linq;
using Xunit;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;

namespace ShadowverseBeyond.Tests.Domain.Abilities.DSL
{
    /// <summary>
    /// 语法分析器单元测试
    /// </summary>
    public class ParserTests
    {
        private List<Token> CreateTokens(params (TokenType type, string value)[] tokenData)
        {
            var tokens = new List<Token>();
            for (int i = 0; i < tokenData.Length; i++)
            {
                var (type, value) = tokenData[i];
                tokens.Add(new Token(type, value, new TokenPosition(1, i + 1, i, value.Length)));
            }
            tokens.Add(new Token(TokenType.EOF, "", new TokenPosition(1, tokenData.Length + 1, tokenData.Length, 0)));
            return tokens;
        }

        [Fact]
        public void Parser_ShouldParseSimpleAbility()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "TestAbility"),
                (TokenType.LeftBrace, "{"),
                (TokenType.RightBrace, "}")
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.Equal("TestAbility", ability.Name);
            Assert.Equal("triggered", ability.Type); // default type
            Assert.Empty(parser.Errors);
        }

        [Fact]
        public void Parser_ShouldParseAbilityWithType()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "TestAbility"),
                (TokenType.Colon, ":"),
                (TokenType.Identifier, "continuous"),
                (TokenType.LeftBrace, "{"),
                (TokenType.RightBrace, "}")
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.Equal("TestAbility", ability.Name);
            Assert.Equal("continuous", ability.Type);
            Assert.Empty(parser.Errors);
        }

        [Fact]
        public void Parser_ShouldParseTrigger()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "TestAbility"),
                (TokenType.LeftBrace, "{"),
                (TokenType.Trigger, "trigger"),
                (TokenType.On, "on"),
                (TokenType.Identifier, "play_card"),
                (TokenType.RightBrace, "}")
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.NotNull(ability.Trigger);
            Assert.Single(ability.Trigger.EventTypes);
            Assert.Equal("play_card", ability.Trigger.EventTypes[0]);
            Assert.Empty(parser.Errors);
        }

        [Fact]
        public void Parser_ShouldParseTriggerWithCondition()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "TestAbility"),
                (TokenType.LeftBrace, "{"),
                (TokenType.Trigger, "trigger"),
                (TokenType.On, "on"),
                (TokenType.Identifier, "play_card"),
                (TokenType.When, "when"),
                (TokenType.Self, "self"),
                (TokenType.Equal, "=="),
                (TokenType.String, "hand"),
                (TokenType.RightBrace, "}")
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.NotNull(ability.Trigger);
            Assert.NotNull(ability.Trigger.Condition);
            Assert.IsType<BinaryOperationNode>(ability.Trigger.Condition);

            var condition = (BinaryOperationNode)ability.Trigger.Condition;
            Assert.Equal(TokenType.Equal, condition.Operator);
            Assert.Empty(parser.Errors);
        }

        [Fact]
        public void Parser_ShouldParseCondition()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "TestAbility"),
                (TokenType.LeftBrace, "{"),
                (TokenType.Condition, "condition"),
                (TokenType.Identifier, "target"),
                (TokenType.GreaterThan, ">"),
                (TokenType.Number, "3"),
                (TokenType.RightBrace, "}")
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.Single(ability.Conditions);
            Assert.IsType<BinaryOperationNode>(ability.Conditions[0].Expression);

            var expression = (BinaryOperationNode)ability.Conditions[0].Expression;
            Assert.Equal(TokenType.GreaterThan, expression.Operator);
            Assert.Empty(parser.Errors);
        }

        [Fact]
        public void Parser_ShouldParseTarget()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "TestAbility"),
                (TokenType.LeftBrace, "{"),
                (TokenType.Target, "target"),
                (TokenType.Enemy, "enemy"),
                (TokenType.RightBrace, "}")
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.Single(ability.Targets);
            Assert.Equal("enemy", ability.Targets[0].Type);
            Assert.Empty(parser.Errors);
        }

        [Fact]
        public void Parser_ShouldParseTargetWithFilter()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "TestAbility"),
                (TokenType.LeftBrace, "{"),
                (TokenType.Target, "target"),
                (TokenType.Enemy, "enemy"),
                (TokenType.Where, "where"),
                (TokenType.Identifier, "attack"),
                (TokenType.GreaterThan, ">"),
                (TokenType.Number, "3"),
                (TokenType.RightBrace, "}")
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.Single(ability.Targets);
            Assert.Single(ability.Targets[0].Filters);

            var filter = ability.Targets[0].Filters[0];
            Assert.Equal("attack", filter.Property);
            Assert.Equal(">", filter.Operator);
            Assert.Empty(parser.Errors);
        }

        [Fact]
        public void Parser_ShouldParseEffect()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "TestAbility"),
                (TokenType.LeftBrace, "{"),
                (TokenType.Effect, "effect"),
                (TokenType.Identifier, "damage"),
                (TokenType.RightBrace, "}")
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.Single(ability.Effects);
            Assert.Equal("damage", ability.Effects[0].Type);
            Assert.Empty(parser.Errors);
        }

        [Fact]
        public void Parser_ShouldParseEffectWithParameters()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "TestAbility"),
                (TokenType.LeftBrace, "{"),
                (TokenType.Effect, "effect"),
                (TokenType.Identifier, "damage"),
                (TokenType.LeftParen, "("),
                (TokenType.Identifier, "target"),
                (TokenType.Comma, ","),
                (TokenType.Number, "3"),
                (TokenType.RightParen, ")"),
                (TokenType.RightBrace, "}")
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.Single(ability.Effects);
            Assert.Equal(2, ability.Effects[0].Parameters.Count);
            Assert.Empty(parser.Errors);
        }

        [Fact]
        public void Parser_ShouldParseProperty()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "TestAbility"),
                (TokenType.LeftBrace, "{"),
                (TokenType.Identifier, "priority"),
                (TokenType.Assign, "="),
                (TokenType.Number, "100"),
                (TokenType.RightBrace, "}")
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.Single(ability.Properties);
            Assert.Equal("priority", ability.Properties[0].Name);
            Assert.IsType<LiteralNode>(ability.Properties[0].Value);
            Assert.Empty(parser.Errors);
        }

        [Fact]
        public void Parser_ShouldParseBinaryExpression()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Number, "5"),
                (TokenType.Plus, "+"),
                (TokenType.Number, "3"),
                (TokenType.Multiply, "*"),
                (TokenType.Number, "2")
            );
            var parser = new Parser(tokens);

            // Act
            var expression = parser.ParseExpression();

            // Assert
            Assert.IsType<BinaryOperationNode>(expression);
            var binaryOp = (BinaryOperationNode)expression;
            Assert.Equal(TokenType.Plus, binaryOp.Operator);

            // Should respect operator precedence: 5 + (3 * 2)
            Assert.IsType<LiteralNode>(binaryOp.Left);
            Assert.IsType<BinaryOperationNode>(binaryOp.Right);

            var rightOp = (BinaryOperationNode)binaryOp.Right;
            Assert.Equal(TokenType.Multiply, rightOp.Operator);
        }

        [Fact]
        public void Parser_ShouldParseLogicalExpression()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Identifier, "x"),
                (TokenType.GreaterThan, ">"),
                (TokenType.Number, "0"),
                (TokenType.And, "and"),
                (TokenType.Identifier, "y"),
                (TokenType.LessThan, "<"),
                (TokenType.Number, "10")
            );
            var parser = new Parser(tokens);

            // Act
            var expression = parser.ParseExpression();

            // Assert
            Assert.IsType<BinaryOperationNode>(expression);
            var logicalOp = (BinaryOperationNode)expression;
            Assert.Equal(TokenType.And, logicalOp.Operator);

            Assert.IsType<BinaryOperationNode>(logicalOp.Left);
            Assert.IsType<BinaryOperationNode>(logicalOp.Right);
        }

        [Fact]
        public void Parser_ShouldParseFunctionCall()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Identifier, "damage"),
                (TokenType.LeftParen, "("),
                (TokenType.Identifier, "target"),
                (TokenType.Comma, ","),
                (TokenType.Number, "3"),
                (TokenType.RightParen, ")")
            );
            var parser = new Parser(tokens);

            // Act
            var expression = parser.ParseExpression();

            // Assert
            Assert.IsType<FunctionCallNode>(expression);
            var funcCall = (FunctionCallNode)expression;
            Assert.Equal("damage", funcCall.FunctionName);
            Assert.Equal(2, funcCall.Arguments.Count);
        }

        [Fact]
        public void Parser_ShouldParsePropertyAccess()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Self, "self"),
                (TokenType.Dot, "."),
                (TokenType.Identifier, "attack")
            );
            var parser = new Parser(tokens);

            // Act
            var expression = parser.ParseExpression();

            // Assert
            Assert.IsType<IdentifierNode>(expression);
            var identifier = (IdentifierNode)expression;
            Assert.Equal(2, identifier.Path.Count);
            Assert.Equal("self", identifier.Path[0]);
            Assert.Equal("attack", identifier.Path[1]);
        }

        [Fact]
        public void Parser_ShouldParseComplexAbility()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "Fanfare"),
                (TokenType.Colon, ":"),
                (TokenType.Identifier, "triggered"),
                (TokenType.LeftBrace, "{"),
                (TokenType.Trigger, "trigger"),
                (TokenType.On, "on"),
                (TokenType.Identifier, "play_card"),
                (TokenType.When, "when"),
                (TokenType.Self, "self"),
                (TokenType.Equal, "=="),
                (TokenType.String, "hand"),
                (TokenType.Target, "target"),
                (TokenType.Enemy, "enemy"),
                (TokenType.Where, "where"),
                (TokenType.Identifier, "attack"),
                (TokenType.GreaterThan, ">"),
                (TokenType.Number, "3"),
                (TokenType.Effect, "effect"),
                (TokenType.Identifier, "damage"),
                (TokenType.LeftParen, "("),
                (TokenType.Identifier, "target"),
                (TokenType.Comma, ","),
                (TokenType.Number, "2"),
                (TokenType.RightParen, ")"),
                (TokenType.Identifier, "priority"),
                (TokenType.Assign, "="),
                (TokenType.Number, "100"),
                (TokenType.RightBrace, "}")
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.Equal("Fanfare", ability.Name);
            Assert.Equal("triggered", ability.Type);
            Assert.NotNull(ability.Trigger);
            Assert.Single(ability.Targets);
            Assert.Single(ability.Effects);
            Assert.Single(ability.Properties);
            Assert.Empty(parser.Errors);
        }

        [Fact]
        public void Parser_ShouldHandleSyntaxError()
        {
            // Arrange - Missing closing brace
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "TestAbility"),
                (TokenType.LeftBrace, "{"),
                (TokenType.Trigger, "trigger")
            // Missing closing brace and other tokens
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.NotEmpty(parser.Errors);
            Assert.NotNull(ability); // Should still return an ability node
        }

        [Fact]
        public void Parser_ShouldHandleUnexpectedToken()
        {
            // Arrange
            var tokens = CreateTokens(
                (TokenType.Ability, "ability"),
                (TokenType.Identifier, "TestAbility"),
                (TokenType.LeftBrace, "{"),
                (TokenType.Number, "123"), // Unexpected number in ability body
                (TokenType.RightBrace, "}")
            );
            var parser = new Parser(tokens);

            // Act
            var ability = parser.ParseAbility();

            // Assert
            Assert.NotEmpty(parser.Errors);
            Assert.Contains("Unexpected token", parser.Errors[0].Message);
        }
    }
}
