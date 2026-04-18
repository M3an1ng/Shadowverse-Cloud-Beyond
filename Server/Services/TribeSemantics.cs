using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public sealed class TribeSemantics : ITribeSemantics
    {
        private readonly ITribeRegistry _tribeRegistry;

        public TribeSemantics(ITribeRegistry tribeRegistry)
        {
            _tribeRegistry = tribeRegistry;
        }

        public IReadOnlyCollection<string> Expand(IEnumerable<string>? sourceTribes)
        {
            var normalized = _tribeRegistry.NormalizeMany(sourceTribes);
            if (normalized.Count == 0)
            {
                return Array.Empty<string>();
            }

            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tribe in normalized)
            {
                if (_tribeRegistry.TryGetDefinition(tribe, out var definition) &&
                    definition.Behavior == TribeBehavior.MatchAllKnownTribes)
                {
                    foreach (var knownTribe in _tribeRegistry.GetKnownNormalTribes())
                    {
                        expanded.Add(knownTribe);
                    }

                    continue;
                }

                expanded.Add(tribe);
            }

            return expanded.ToList();
        }

        public bool Contains(IEnumerable<string>? sourceTribes, string? expectedTribe)
        {
            var normalizedExpected = _tribeRegistry.Normalize(expectedTribe);
            if (string.IsNullOrWhiteSpace(normalizedExpected))
            {
                return false;
            }

            if (_tribeRegistry.TryGetDefinition(normalizedExpected, out var definition) &&
                definition.Behavior == TribeBehavior.MatchAllKnownTribes)
            {
                return _tribeRegistry.NormalizeMany(sourceTribes)
                    .Any(existing => string.Equals(existing, normalizedExpected, StringComparison.OrdinalIgnoreCase));
            }

            return Expand(sourceTribes)
                .Any(existing => string.Equals(existing, normalizedExpected, StringComparison.OrdinalIgnoreCase));
        }

        public bool Overlaps(IEnumerable<string>? left, IEnumerable<string>? right)
        {
            var expandedLeft = Expand(left);
            var expandedRight = Expand(right);
            if (expandedLeft.Count == 0 || expandedRight.Count == 0)
            {
                return false;
            }

            return expandedLeft.Intersect(expandedRight, StringComparer.OrdinalIgnoreCase).Any();
        }
    }
}
