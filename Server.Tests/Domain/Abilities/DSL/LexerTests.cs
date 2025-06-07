using System.Linq;
using Xunit;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;

namespace ShadowverseBeyond.Tests.Domain.Abilities.DSL
{
    /// <summary>
    /// 词法分析器单元测试
    /// </summary>
    public class LexerTests
    {
        [Fact]
        public void Lexer_ShouldTokenizeNumbers()
        {
            // Arrange
            var source = "123 3.14 -5";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(4, tokens.Count); // 3 numbers + EOF
            
            Assert.Equal(TokenType.Number, tokens[0].Type);
            Assert.Equal("123", tokens[0].Value);
            Assert.Equal(123, tokens[0].ParsedValue);
            
            Assert.Equal(TokenType.Number, tokens[1].Type);
            Assert.Equal("3.14", tokens[1].Value);
            Assert.Equal(3.14, tokens[1].ParsedValue);
            
            Assert.Equal(TokenType.Minus, tokens[2].Type);
            Assert.Equal("-", tokens[2].Value);
            
            Assert.Equal(TokenType.EOF, tokens[3].Type);
        }

        [Fact]
        public void Lexer_ShouldTokenizeStrings()
        {
            // Arrange
            var source = "\"hello world\" 'test string'";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(3, tokens.Count); // 2 strings + EOF
            
            Assert.Equal(TokenType.String, tokens[0].Type);
            Assert.Equal("hello world", tokens[0].Value);
            Assert.Equal("hello world", tokens[0].ParsedValue);
            
            Assert.Equal(TokenType.String, tokens[1].Type);
            Assert.Equal("test string", tokens[1].Value);
            Assert.Equal("test string", tokens[1].ParsedValue);
        }

        [Fact]
        public void Lexer_ShouldTokenizeStringWithEscapes()
        {
            // Arrange
            var source = "\"hello\\nworld\\t!\"";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(2, tokens.Count); // 1 string + EOF
            
            Assert.Equal(TokenType.String, tokens[0].Type);
            Assert.Equal("hello\nworld\t!", tokens[0].Value);
        }

        [Fact]
        public void Lexer_ShouldTokenizeBooleans()
        {
            // Arrange
            var source = "true false";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(3, tokens.Count); // 2 booleans + EOF
            
            Assert.Equal(TokenType.Boolean, tokens[0].Type);
            Assert.Equal("true", tokens[0].Value);
            Assert.Equal(true, tokens[0].ParsedValue);
            
            Assert.Equal(TokenType.Boolean, tokens[1].Type);
            Assert.Equal("false", tokens[1].Value);
            Assert.Equal(false, tokens[1].ParsedValue);
        }

        [Fact]
        public void Lexer_ShouldTokenizeKeywords()
        {
            // Arrange
            var source = "ability trigger condition target effect";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(6, tokens.Count); // 5 keywords + EOF
            
            Assert.Equal(TokenType.Ability, tokens[0].Type);
            Assert.Equal("ability", tokens[0].Value);
            
            Assert.Equal(TokenType.Trigger, tokens[1].Type);
            Assert.Equal("trigger", tokens[1].Value);
            
            Assert.Equal(TokenType.Condition, tokens[2].Type);
            Assert.Equal("condition", tokens[2].Value);
            
            Assert.Equal(TokenType.Target, tokens[3].Type);
            Assert.Equal("target", tokens[3].Value);
            
            Assert.Equal(TokenType.Effect, tokens[4].Type);
            Assert.Equal("effect", tokens[4].Value);
        }

        [Fact]
        public void Lexer_ShouldTokenizeIdentifiers()
        {
            // Arrange
            var source = "self enemy_units my_variable";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(4, tokens.Count); // 3 identifiers + EOF
            
            Assert.Equal(TokenType.Self, tokens[0].Type);
            Assert.Equal("self", tokens[0].Value);
            
            Assert.Equal(TokenType.Identifier, tokens[1].Type);
            Assert.Equal("enemy_units", tokens[1].Value);
            
            Assert.Equal(TokenType.Identifier, tokens[2].Type);
            Assert.Equal("my_variable", tokens[2].Value);
        }

        [Fact]
        public void Lexer_ShouldTokenizeOperators()
        {
            // Arrange
            var source = "+ - * / == != > < >= <= && || !";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(14, tokens.Count); // 13 operators + EOF
            
            Assert.Equal(TokenType.Plus, tokens[0].Type);
            Assert.Equal(TokenType.Minus, tokens[1].Type);
            Assert.Equal(TokenType.Multiply, tokens[2].Type);
            Assert.Equal(TokenType.Divide, tokens[3].Type);
            Assert.Equal(TokenType.Equal, tokens[4].Type);
            Assert.Equal(TokenType.NotEqual, tokens[5].Type);
            Assert.Equal(TokenType.GreaterThan, tokens[6].Type);
            Assert.Equal(TokenType.LessThan, tokens[7].Type);
            Assert.Equal(TokenType.GreaterEqual, tokens[8].Type);
            Assert.Equal(TokenType.LessEqual, tokens[9].Type);
            Assert.Equal(TokenType.And, tokens[10].Type);
            Assert.Equal(TokenType.Or, tokens[11].Type);
            Assert.Equal(TokenType.Not, tokens[12].Type);
        }

        [Fact]
        public void Lexer_ShouldTokenizeDelimiters()
        {
            // Arrange
            var source = "( ) [ ] { } , ; : .";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(11, tokens.Count); // 10 delimiters + EOF
            
            Assert.Equal(TokenType.LeftParen, tokens[0].Type);
            Assert.Equal(TokenType.RightParen, tokens[1].Type);
            Assert.Equal(TokenType.LeftBracket, tokens[2].Type);
            Assert.Equal(TokenType.RightBracket, tokens[3].Type);
            Assert.Equal(TokenType.LeftBrace, tokens[4].Type);
            Assert.Equal(TokenType.RightBrace, tokens[5].Type);
            Assert.Equal(TokenType.Comma, tokens[6].Type);
            Assert.Equal(TokenType.Semicolon, tokens[7].Type);
            Assert.Equal(TokenType.Colon, tokens[8].Type);
            Assert.Equal(TokenType.Dot, tokens[9].Type);
        }

        [Fact]
        public void Lexer_ShouldTokenizeLineComments()
        {
            // Arrange
            var source = "// This is a comment\nability";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(4, tokens.Count); // comment + newline + ability + EOF
            
            Assert.Equal(TokenType.Comment, tokens[0].Type);
            Assert.Equal(" This is a comment", tokens[0].Value);
            
            Assert.Equal(TokenType.Newline, tokens[1].Type);
            
            Assert.Equal(TokenType.Ability, tokens[2].Type);
        }

        [Fact]
        public void Lexer_ShouldTokenizeBlockComments()
        {
            // Arrange
            var source = "/* This is a\nmulti-line comment */ ability";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(3, tokens.Count); // comment + ability + EOF
            
            Assert.Equal(TokenType.Comment, tokens[0].Type);
            Assert.Contains("This is a", tokens[0].Value);
            Assert.Contains("multi-line comment", tokens[0].Value);
            
            Assert.Equal(TokenType.Ability, tokens[1].Type);
        }

        [Fact]
        public void Lexer_ShouldHandleComplexExpression()
        {
            // Arrange
            var source = "self.attack > enemy.health && target.cost <= 3";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(12, tokens.Count); // 11 tokens + EOF
            
            var tokenTypes = tokens.Take(11).Select(t => t.Type).ToArray();
            var expectedTypes = new[]
            {
                TokenType.Self,
                TokenType.Dot,
                TokenType.Identifier,
                TokenType.GreaterThan,
                TokenType.Identifier,
                TokenType.Dot,
                TokenType.Identifier,
                TokenType.And,
                TokenType.Identifier,
                TokenType.Dot,
                TokenType.Identifier
            };
            
            Assert.Equal(expectedTypes, tokenTypes);
        }

        [Fact]
        public void Lexer_ShouldTrackPositions()
        {
            // Arrange
            var source = "ability\ntrigger";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(4, tokens.Count); // ability + newline + trigger + EOF
            
            // First token position
            Assert.Equal(1, tokens[0].Position.Line);
            Assert.Equal(1, tokens[0].Position.Column);
            
            // Newline position
            Assert.Equal(1, tokens[1].Position.Line);
            Assert.Equal(8, tokens[1].Position.Column);
            
            // Second token position (after newline)
            Assert.Equal(2, tokens[2].Position.Line);
            Assert.Equal(1, tokens[2].Position.Column);
        }

        [Fact]
        public void Lexer_ShouldHandleUnterminatedString()
        {
            // Arrange
            var source = "\"unterminated string";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Single(lexer.Errors);
            Assert.Contains("Unterminated string literal", lexer.Errors[0].Message);
            
            // Should still produce a token (marked as invalid)
            Assert.Equal(TokenType.Invalid, tokens[0].Type);
        }

        [Fact]
        public void Lexer_ShouldHandleInvalidCharacters()
        {
            // Arrange
            var source = "ability @ trigger";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Single(lexer.Errors);
            Assert.Contains("Unexpected character '@'", lexer.Errors[0].Message);
            
            // Should continue parsing after error
            Assert.Equal(TokenType.Ability, tokens[0].Type);
            Assert.Equal(TokenType.Invalid, tokens[1].Type);
            Assert.Equal(TokenType.Trigger, tokens[2].Type);
        }

        [Fact]
        public void Lexer_ShouldSkipWhitespace()
        {
            // Arrange
            var source = "  ability   trigger  ";
            var lexer = new Lexer(source);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.Equal(3, tokens.Count); // ability + trigger + EOF (whitespace skipped)
            
            Assert.Equal(TokenType.Ability, tokens[0].Type);
            Assert.Equal(TokenType.Trigger, tokens[1].Type);
            Assert.Equal(TokenType.EOF, tokens[2].Type);
        }
    }
}
