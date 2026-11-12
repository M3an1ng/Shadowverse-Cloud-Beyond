using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    public sealed class TribeSemantics : ITribeSemantics
    {
        private readonly ITribeRegistry _tribeRegistry;
        private readonly IGameContextAccessor? _gameContextAccessor;

        public TribeSemantics(ITribeRegistry tribeRegistry, IGameContextAccessor? gameContextAccessor = null)
        {
            _tribeRegistry = tribeRegistry;
            _gameContextAccessor = gameContextAccessor;
        }

        private GameMode ResolveEffectiveMode(GameMode? mode)
        {
            if (mode.HasValue)
            {
                return mode.Value;
            }

            return _gameContextAccessor?.GameContext?.CurrentGameState?.Mode ?? GameMode.Standard;
        }

        public IReadOnlyCollection<string> Expand(IEnumerable<string>? sourceTribes, GameMode? mode = null)
        {
            var normalized = _tribeRegistry.NormalizeMany(sourceTribes);
            if (normalized.Count == 0)
            {
                return Array.Empty<string>();
            }

            var effectiveMode = ResolveEffectiveMode(mode);
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tribe in normalized)
            {
                if (_tribeRegistry.TryGetDefinition(tribe, out var definition) &&
                    definition.Behavior == TribeBehavior.MatchAllKnownTribes)
                {
                    foreach (var knownTribe in _tribeRegistry.GetKnownNormalTribes(effectiveMode))
                    {
                        expanded.Add(knownTribe);
                    }

                    continue;
                }

                expanded.Add(tribe);
            }

            return expanded.ToList();
        }

        public bool Contains(IEnumerable<string>? sourceTribes, string? expectedTribe, GameMode? mode = null)
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

            return Expand(sourceTribes, mode)
                .Any(existing => string.Equals(existing, normalizedExpected, StringComparison.OrdinalIgnoreCase));
        }

        public bool Overlaps(IEnumerable<string>? left, IEnumerable<string>? right, GameMode? mode = null)
        {
            var expandedLeft = Expand(left, mode);
            var expandedRight = Expand(right, mode);
            if (expandedLeft.Count == 0 || expandedRight.Count == 0)
            {
                return false;
            }

            return expandedLeft.Intersect(expandedRight, StringComparer.OrdinalIgnoreCase).Any();
        }
    }
}
