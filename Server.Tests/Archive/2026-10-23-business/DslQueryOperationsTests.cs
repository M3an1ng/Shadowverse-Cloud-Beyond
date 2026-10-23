using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.DSL
{
    public class DslQueryOperationsTests
    {
        [Fact]
        public void TakeFirst_CountExceedingLength_ReturnsWholeListInOriginalOrder()
        {
            var result = DslQueryOperations.TakeFirst(new[] { 1, 2, 3, 4 }, 10).ToList();

            Assert.Equal(new[] { 1, 2, 3, 4 }, result);
        }

        [Fact]
        public void TakeLast_CountExceedingLength_ReversesWholeList()
        {
            var result = DslQueryOperations.TakeLast(new[] { 1, 2, 3, 4 }, 10).ToList();

            Assert.Equal(new[] { 4, 3, 2, 1 }, result);
        }

        [Fact]
        public void TakeRandom_CountExceedingLength_ShufflesWholeList()
        {
            DslQueryOperations.RandomOverride = new Random(0);

            try
            {
                var source = new[] { 1, 2, 3, 4, 5 };
                var result = DslQueryOperations.TakeRandom(source, 10).ToList();

                Assert.Equal(source.Length, result.Count);
                Assert.Equal(source.OrderBy(value => value), result.OrderBy(value => value));
                Assert.False(source.SequenceEqual(result));
            }
            finally
            {
                DslQueryOperations.RandomOverride = null;
            }
        }
    }
}
