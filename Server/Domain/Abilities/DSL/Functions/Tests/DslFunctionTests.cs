//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Interfaces;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions.Tests
{
    /// <summary>
    /// DSL函数系统的单元测试
    /// </summary>
    public class DslFunctionTests
    {
        private readonly Mock<ILogger<FunctionRegistry>> _mockLogger;
        private readonly Mock<ICardRepository> _mockCardRepository;
        private readonly FunctionRegistry _functionRegistry;
        private readonly EvaluationContext _evaluationContext;

        public DslFunctionTests()
        {
            _mockLogger = new Mock<ILogger<FunctionRegistry>>();
            _mockCardRepository = new Mock<ICardRepository>();
            _functionRegistry = new FunctionRegistry(_mockLogger.Object);
            _evaluationContext = new EvaluationContext(
                _mockCardRepository.Object,
                _functionRegistry
            );
        }

        #region Count Function Tests

        [Fact]
        public async Task CountFunction_WithEmptyCollection_ReturnsZero()
        {
            // Arrange
            var countFunction = new CountFunction();
            var args = new List<object?> { new List<object>() };

            // Act
            var result = await countFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task CountFunction_WithNonEmptyCollection_ReturnsCorrectCount()
        {
            // Arrange
            var countFunction = new CountFunction();
            var collection = new List<object> { "item1", "item2", "item3" };
            var args = new List<object?> { collection };

            // Act
            var result = await countFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task CountFunction_WithNullCollection_ReturnsZero()
        {
            // Arrange
            var countFunction = new CountFunction();
            var args = new List<object?> { null };

            // Act
            var result = await countFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(0, result);
        }

        #endregion

        #region Max Function Tests

        [Fact]
        public async Task MaxFunction_WithNumericValues_ReturnsMaximum()
        {
            // Arrange
            var maxFunction = new MaxFunction();
            var args = new List<object?> { 1, 5, 3, 9, 2 };

            // Act
            var result = await maxFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(9.0, result);
        }

        [Fact]
        public async Task MaxFunction_WithEmptyCollection_ReturnsNull()
        {
            // Arrange
            var maxFunction = new MaxFunction();
            var args = new List<object?> { new List<object>() };

            // Act
            var result = await maxFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task MaxFunction_WithMixedTypes_ConvertsAndReturnsMax()
        {
            // Arrange
            var maxFunction = new MaxFunction();
            var args = new List<object?> { 1, "5", 3.7, 2 };

            // Act
            var result = await maxFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(5.0, result);
        }

        #endregion

        #region Min Function Tests

        [Fact]
        public async Task MinFunction_WithNumericValues_ReturnsMinimum()
        {
            // Arrange
            var minFunction = new MinFunction();
            var args = new List<object?> { 1, 5, 3, 9, 2 };

            // Act
            var result = await minFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(1.0, result);
        }

        [Fact]
        public async Task MinFunction_WithEmptyCollection_ReturnsNull()
        {
            // Arrange
            var minFunction = new MinFunction();
            var args = new List<object?> { new List<object>() };

            // Act
            var result = await minFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Sum Function Tests

        [Fact]
        public async Task SumFunction_WithNumericValues_ReturnsSum()
        {
            // Arrange
            var sumFunction = new SumFunction();
            var args = new List<object?> { 1, 2, 3, 4, 5 };

            // Act
            var result = await sumFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(15.0, result);
        }

        [Fact]
        public async Task SumFunction_WithEmptyCollection_ReturnsZero()
        {
            // Arrange
            var sumFunction = new SumFunction();
            var args = new List<object?> { new List<object>() };

            // Act
            var result = await sumFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(0.0, result);
        }

        #endregion

        #region If Function Tests

        [Fact]
        public async Task IfFunction_WithTrueCondition_ReturnsThenValue()
        {
            // Arrange
            var ifFunction = new IfFunction();
            var args = new List<object?> { true, "then_value", "else_value" };

            // Act
            var result = await ifFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal("then_value", result);
        }

        [Fact]
        public async Task IfFunction_WithFalseCondition_ReturnsElseValue()
        {
            // Arrange
            var ifFunction = new IfFunction();
            var args = new List<object?> { false, "then_value", "else_value" };

            // Act
            var result = await ifFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal("else_value", result);
        }

        [Fact]
        public async Task IfFunction_WithoutElseValue_ReturnsNull()
        {
            // Arrange
            var ifFunction = new IfFunction();
            var args = new List<object?> { false, "then_value" };

            // Act
            var result = await ifFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Contains Function Tests

        [Fact]
        public async Task ContainsFunction_WithStringContainsSubstring_ReturnsTrue()
        {
            // Arrange
            var containsFunction = new ContainsFunction();
            var args = new List<object?> { "hello world", "world" };

            // Act
            var result = await containsFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(true, result);
        }

        [Fact]
        public async Task ContainsFunction_WithCollectionContainsValue_ReturnsTrue()
        {
            // Arrange
            var containsFunction = new ContainsFunction();
            var collection = new List<object> { 1, 2, 3, 4, 5 };
            var args = new List<object?> { collection, 3 };

            // Act
            var result = await containsFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(true, result);
        }

        [Fact]
        public async Task ContainsFunction_WithCollectionNotContainsValue_ReturnsFalse()
        {
            // Arrange
            var containsFunction = new ContainsFunction();
            var collection = new List<object> { 1, 2, 3, 4, 5 };
            var args = new List<object?> { collection, 6 };

            // Act
            var result = await containsFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(false, result);
        }

        #endregion

        #region IsEmpty Function Tests

        [Fact]
        public async Task IsEmptyFunction_WithEmptyCollection_ReturnsTrue()
        {
            // Arrange
            var isEmptyFunction = new IsEmptyFunction();
            var args = new List<object?> { new List<object>() };

            // Act
            var result = await isEmptyFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(true, result);
        }

        [Fact]
        public async Task IsEmptyFunction_WithNonEmptyCollection_ReturnsFalse()
        {
            // Arrange
            var isEmptyFunction = new IsEmptyFunction();
            var collection = new List<object> { "item" };
            var args = new List<object?> { collection };

            // Act
            var result = await isEmptyFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(false, result);
        }

        [Fact]
        public async Task IsEmptyFunction_WithEmptyString_ReturnsTrue()
        {
            // Arrange
            var isEmptyFunction = new IsEmptyFunction();
            var args = new List<object?> { "" };

            // Act
            var result = await isEmptyFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(true, result);
        }

        [Fact]
        public async Task IsEmptyFunction_WithNonEmptyString_ReturnsFalse()
        {
            // Arrange
            var isEmptyFunction = new IsEmptyFunction();
            var args = new List<object?> { "hello" };

            // Act
            var result = await isEmptyFunction.ExecuteAsync(args, _evaluationContext);

            // Assert
            Assert.Equal(false, result);
        }

        #endregion

        #region Function Registry Tests

        [Fact]
        public void FunctionRegistry_RegisterFunction_CanRetrieveFunction()
        {
            // Arrange
            var customFunction = new CountFunction();

            // Act
            _functionRegistry.RegisterFunction(customFunction);
            var retrievedFunction = _functionRegistry.GetFunction("count");

            // Assert
            Assert.NotNull(retrievedFunction);
            Assert.Equal("count", retrievedFunction.Name);
        }

        [Fact]
        public void FunctionRegistry_GetNonExistentFunction_ReturnsNull()
        {
            // Act
            var result = _functionRegistry.GetFunction("nonexistent");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FunctionRegistry_IsRegistered_ReturnsCorrectStatus()
        {
            // Arrange
            var customFunction = new CountFunction();
            _functionRegistry.RegisterFunction(customFunction);

            // Act & Assert
            Assert.True(_functionRegistry.IsRegistered("count"));
            Assert.False(_functionRegistry.IsRegistered("nonexistent"));
        }

        #endregion
    }
}
