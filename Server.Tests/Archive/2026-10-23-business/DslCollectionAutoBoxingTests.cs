using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.DSL
{
    public class DslCollectionAutoBoxingTests
    {
        private sealed class StatHolder
        {
            public int Value { get; set; }
        }

        [Fact]
        public void SumFunction_WithSingletonObjectProperty_AutoBoxes()
        {
            var function = new SumFunction();
            var args = new List<object?> { new StatHolder { Value = 7 }, "Value" };

            var result = function.Execute(args, null!);

            Assert.Equal(7.0, result);
        }

        [Fact]
        public void IsEmptyFunction_WithSingletonValue_ReturnsFalse()
        {
            var function = new IsEmptyFunction();
            var args = new List<object?> { new StatHolder { Value = 1 } };

            var result = function.Execute(args, null!);

            Assert.False((bool)result!);
        }
    }
}
