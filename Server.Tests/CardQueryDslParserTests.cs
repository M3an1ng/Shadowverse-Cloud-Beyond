using Xunit;
using ShadowverseBeyond.Server.Domain.Query;
using System.Linq;
using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Tests
{
    public class CardQueryDslParserTests
    {
        private readonly QueryDslParser _parser = new QueryDslParser();

        [Fact]
        public void Parse_SimpleQuery_ReturnsCorrectConfig()
        {
            // Arrange
            var dsl = "player.hand.query(self.cost > 3)";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal("player.hand", result.SourcePoolExpression);
            Assert.Single(result.Operations);

            var filterOp = Assert.IsType<FilterOperation>(result.Operations[0]);
            var filterConfig = Assert.Single(filterOp.Filters);
            var formulaConfig = Assert.IsType<FormulaValueConfig>(filterConfig.ValueSource);
            Assert.Equal("self.cost > 3", formulaConfig.Formula);
        }

        [Fact]
        public void Parse_FullQuery_ReturnsCorrectConfigInOrder()
        {
            // Arrange
            var dsl = "opponent.board.query(self.trait == \"Officer\").orderBy(self.attack desc).take(2)";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal("opponent.board", result.SourcePoolExpression);
            Assert.Equal(3, result.Operations.Count);

            // 1. FilterOperation
            var filterOp = Assert.IsType<FilterOperation>(result.Operations[0]);
            var filterFormulaConfig = Assert.IsType<FormulaValueConfig>(Assert.Single(filterOp.Filters).ValueSource);
            Assert.Equal("self.trait == \"Officer\"", filterFormulaConfig.Formula);

            // 2. SortOperation
            var sortOp = Assert.IsType<SortOperation>(result.Operations[1]);
            var sortConfig = Assert.Single(sortOp.Ordering);
            var sortFormulaConfig = Assert.IsType<FormulaValueConfig>(sortConfig.ValueSource);
            Assert.Equal("self.attack", sortFormulaConfig.Formula);
            Assert.Equal(OrderingDirection.Descending, sortConfig.Direction);

            // 3. SelectionOperation
            var selectionOp = Assert.IsType<SelectionOperation>(result.Operations[2]);
            Assert.Equal(SelectionStrategy.First, selectionOp.Strategy);
            Assert.NotNull(selectionOp.CountSource);
            var selectionFormulaConfig = Assert.IsType<FormulaValueConfig>(selectionOp.CountSource);
            Assert.Equal("2", selectionFormulaConfig.Formula);
        }

        [Fact]
        public void Parse_DifferentOrder_MaintainsCorrectOperationOrder()
        {
            // Arrange
            var dsl = "player.deck.take(10).query(self.type == \"Amulet\")";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal("player.deck", result.SourcePoolExpression);
            Assert.Equal(2, result.Operations.Count);

            // 1. SelectionOperation should be first
            var selectionOp = Assert.IsType<SelectionOperation>(result.Operations[0]);
            Assert.NotNull(selectionOp.CountSource);
            var selectionFormulaConfig = Assert.IsType<FormulaValueConfig>(selectionOp.CountSource);
            Assert.Equal("10", selectionFormulaConfig.Formula);

            // 2. FilterOperation should be second
            var filterOp = Assert.IsType<FilterOperation>(result.Operations[1]);
            var filterFormulaConfig = Assert.IsType<FormulaValueConfig>(Assert.Single(filterOp.Filters).ValueSource);
            Assert.Equal("self.type == \"Amulet\"", filterFormulaConfig.Formula);
        }

        [Fact]
        public void Parse_StaticDbSource_SetsCorrectTargetType()
        {
            // Arrange
            var dsl = "db.cards.query(self.cost >= 10)";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal("db.cards", result.SourcePoolExpression);
            var filterOp = Assert.IsType<FilterOperation>(Assert.Single(result.Operations));
            var filterFormulaConfig = Assert.IsType<FormulaValueConfig>(Assert.Single(filterOp.Filters).ValueSource);
            Assert.Equal("self.cost >= 10", filterFormulaConfig.Formula);
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
        public void Parse_MissingSource_ThrowsInvalidOperationException()
        {
            // Arrange
            var dsl = ".query(self.cost > 1)";

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _parser.Parse(dsl));
        }

        [Fact]
        public void Parse_MultipleOrderByClauses_ParsesCorrectly()
        {
            // Arrange
            var dsl = "all.board.orderBy(self.cost desc, self.attack asc)";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal("all.board", result.SourcePoolExpression);
            var sortOp = Assert.IsType<SortOperation>(Assert.Single(result.Operations));
            Assert.Equal(2, sortOp.Ordering.Count);

            var sortFormulaConfig1 = Assert.IsType<FormulaValueConfig>(sortOp.Ordering[0].ValueSource);
            Assert.Equal("self.cost", sortFormulaConfig1.Formula);
            Assert.Equal(OrderingDirection.Descending, sortOp.Ordering[0].Direction);

            var sortFormulaConfig2 = Assert.IsType<FormulaValueConfig>(sortOp.Ordering[1].ValueSource);
            Assert.Equal("self.attack", sortFormulaConfig2.Formula);
            Assert.Equal(OrderingDirection.Ascending, sortOp.Ordering[1].Direction);
        }

        [Fact]
        public void Parse_ChainedOrderByClauses_CreatesMultipleSortOperations()
        {
            // Arrange
            var dsl = "player.hand.orderBy(self.cost desc).orderBy(self.attack asc)";

            // Act
            var result = _parser.Parse(dsl);

            // Assert
            Assert.Equal("player.hand", result.SourcePoolExpression);
            Assert.Equal(2, result.Operations.Count);

            var sortOp1 = Assert.IsType<SortOperation>(result.Operations[0]);
            var sortConfig1 = Assert.Single(sortOp1.Ordering);
            var sortFormulaConfig1 = Assert.IsType<FormulaValueConfig>(sortConfig1.ValueSource);
            Assert.Equal("self.cost", sortFormulaConfig1.Formula);
            Assert.Equal(OrderingDirection.Descending, sortConfig1.Direction);

            var sortOp2 = Assert.IsType<SortOperation>(result.Operations[1]);
            var sortConfig2 = Assert.Single(sortOp2.Ordering);
            var sortFormulaConfig2 = Assert.IsType<FormulaValueConfig>(sortConfig2.ValueSource);
            Assert.Equal("self.attack", sortFormulaConfig2.Formula);
            Assert.Equal(OrderingDirection.Ascending, sortConfig2.Direction);
        }
    }
}