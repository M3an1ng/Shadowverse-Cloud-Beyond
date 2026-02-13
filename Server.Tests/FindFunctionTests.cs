using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Entities;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class FindFunctionTests
    {
        [Fact]
        public void ResolveReturnType_ListSource_ReturnsCollectionElementType()
        {
            var function = new FindFunction();
            var resolver = Assert.IsAssignableFrom<IFunctionReturnTypeResolver>(function);

            var resolved = resolver.ResolveReturnType(new[] { typeof(List<PlayerState>), typeof(int) });

            Assert.Equal(typeof(PlayerState), resolved);
        }

        [Fact]
        public void Execute_ListSource_ReturnsOriginalElement()
        {
            var function = new FindFunction();
            var expected = new PlayerState(Guid.NewGuid());
            var source = new List<PlayerState> { expected };

            var result = function.Execute(new List<object?> { source, 1 }, null!);

            Assert.Same(expected, result);
        }

        [Fact]
        public void Execute_IndexOutOfRange_ReturnsDefaultValueWithoutTargetInfoConversion()
        {
            var function = new FindFunction();
            var fallback = new PlayerState(Guid.NewGuid());
            var source = new List<PlayerState>();

            var result = function.Execute(new List<object?> { source, 1, fallback }, null!);

            Assert.Same(fallback, result);
        }
    }
}
