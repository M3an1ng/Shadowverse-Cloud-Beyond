using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    public static class EffectTargetSelector
    {
        public static bool TrySelectTargets(
            IReadOnlyCollection<string>? selectorKeys,
            IReadOnlyDictionary<string, List<TargetInfo>> resolvedTargets,
            out List<TargetInfo> selectedTargets)
        {
            selectedTargets = new List<TargetInfo>();

            if (resolvedTargets == null)
            {
                return true;
            }

            if (selectorKeys == null || selectorKeys.Count == 0)
            {
                selectedTargets = resolvedTargets.Values.SelectMany(list => list).ToList();
                return true;
            }

            if (selectorKeys.Any(IsSelectAllToken))
            {
                selectedTargets = resolvedTargets.Values.SelectMany(list => list).ToList();
                return true;
            }

            var aggregated = new List<TargetInfo>();
            var matchedAny = false;

            foreach (var token in selectorKeys)
            {
                if (IsSelectAllToken(token))
                {
                    selectedTargets = resolvedTargets.Values.SelectMany(list => list).ToList();
                    return true;
                }

                var key = NormalizeKey(token);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!resolvedTargets.TryGetValue(key, out var targets))
                {
                    continue;
                }

                matchedAny = true;
                aggregated.AddRange(targets);
            }

            if (!matchedAny)
            {
                return false;
            }

            selectedTargets = DistinctTargets(aggregated);
            return true;
        }

        private static bool IsSelectAllToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return token.Equals("ResolvedTargets", StringComparison.OrdinalIgnoreCase)
                || token.Equals("ResolvedTargets.*", StringComparison.OrdinalIgnoreCase)
                || token.Equals("ResolvedTargets.All", StringComparison.OrdinalIgnoreCase)
                || token.Equals("*", StringComparison.OrdinalIgnoreCase)
                || token.Equals("All", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeKey(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            var key = token.Trim();
            const string prefix = "ResolvedTargets.";
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                key = key.Substring(prefix.Length);
            }

            return key.Trim();
        }

        private static List<TargetInfo> DistinctTargets(IEnumerable<TargetInfo> targets)
        {
            var unique = new HashSet<(Guid Id, TargetType Type)>();
            var result = new List<TargetInfo>();

            foreach (var target in targets)
            {
                var key = (target.InstanceId, target.Type);
                if (unique.Add(key))
                {
                    result.Add(target);
                }
            }

            return result;
        }
    }
}
