using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    internal static class DslTribeOperations
    {
        public static bool Contains(GameContext? context, IEnumerable<string>? source, string? expected)
        {
            if (source == null || string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }

            var semantics = context?.ServiceProvider.GetService<ITribeSemantics>();
            if (semantics != null)
            {
                return semantics.Contains(source, expected);
            }

            return source.Any(item => string.Equals(item?.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
