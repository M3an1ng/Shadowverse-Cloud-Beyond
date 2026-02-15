using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class FrequencyFunctionTests
    {
        [Fact]
        public void Execute_NumberCollection_ReturnsMatchedCount()
        {
            var function = new FrequencyFunction();

            var result = function.Execute(new List<object?> { new List<int> { 1, 2, 2, 3, 2 }, 2 }, null!);

            Assert.Equal(3, result);
        }

        [Fact]
        public void Execute_StringCollection_IsCaseInsensitive()
        {
            var function = new FrequencyFunction();

            var result = function.Execute(
                new List<object?> { new List<string> { "PlayCard", "playcard", "Draw" }, "PLAYCARD" },
                null!);

            Assert.Equal(2, result);
        }

        [Fact]
        public void Execute_NullSource_ReturnsZero()
        {
            var function = new FrequencyFunction();

            var result = function.Execute(new List<object?> { null, 1 }, null!);

            Assert.Equal(0, result);
        }

        [Fact]
        public void ValidateArguments_NonCollectionSource_ReturnsFalse()
        {
            var function = new FrequencyFunction();

            var isValid = function.ValidateArguments(new List<object?> { 123, 1 });

            Assert.False(isValid);
        }
    }
}
