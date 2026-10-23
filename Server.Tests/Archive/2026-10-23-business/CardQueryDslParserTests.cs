using System;
using Xunit;
using ShadowverseBeyond.Server.Domain.Query;

namespace ShadowverseBeyond.Server.Tests
{
    public class CardQueryDslParserTests
    {
        private readonly QueryDslParser _parser = new QueryDslParser();

        [Fact]
        public void Parse_SimpleQuery_PreservesDslAndHasNoOperations()
        {
            // Arrange
            var dsl = "player.hand.query(self.cost > 3)";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal(dsl, result.SourcePoolExpression);
            Assert.Empty(result.Operations);
        }

        [Fact]
        public void Parse_FullQuery_PreservesDslAndHasNoOperations()
        {
            // Arrange
            var dsl = "opponent.board.query(self.trait == \"Officer\").orderBy(self.attack desc).take(2)";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal(dsl, result.SourcePoolExpression);
            Assert.Empty(result.Operations);
        }

        [Fact]
        public void Parse_DifferentOrder_PreservesDslAndHasNoOperations()
        {
            // Arrange
            var dsl = "player.deck.take(10).query(self.type == \"Amulet\")";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal(dsl, result.SourcePoolExpression);
            Assert.Empty(result.Operations);
        }

        [Fact]
        public void Parse_TakeRandom_PreservesDslAndHasNoOperations()
        {
            // Arrange
            var dsl = "player.hand.take(random 3)";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal(dsl, result.SourcePoolExpression);
            Assert.Empty(result.Operations);
        }

        [Fact]
        public void Parse_TakeLast_PreservesDslAndHasNoOperations()
        {
            // Arrange
            var dsl = "player.hand.take(last 2)";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal(dsl, result.SourcePoolExpression);
            Assert.Empty(result.Operations);
        }

        [Fact]
        public void Parse_StaticDbSource_PreservesDslAndHasNoOperations()
        {
            // Arrange
            var dsl = "db.cards.query(self.cost >= 10)";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal(dsl, result.SourcePoolExpression);
            Assert.Empty(result.Operations);
        }

        [Fact]
        public void Parse_SourceOnly_ReturnsConfigWithNoOperations()
        {
            // Arrange
            var dsl = "player.graveyard";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal("player.graveyard", result.SourcePoolExpression);
            Assert.Empty(result.Operations);
        }

        [Fact]
        public void Parse_SourceWithSetOperators_PreservesSourceExpression()
        {
            // Arrange
            var dsl = "player.hand | opponent.hand";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal("player.hand | opponent.hand", result.SourcePoolExpression);
            Assert.Empty(result.Operations);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void Parse_InvalidDsl_ThrowsArgumentException(string dsl)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _parser.Parse(dsl));
        }

        [Fact]
        public void Parse_MissingSource_PreservesDslAndHasNoOperations()
        {
            // Arrange
            var dsl = ".query(self.cost > 1)";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal(dsl, result.SourcePoolExpression);
            Assert.Empty(result.Operations);
        }
    }
}
