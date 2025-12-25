using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    public static class EffectTargetSelector
    {
        public static bool TrySelectTargets(
            string? selector,
            IReadOnlyDictionary<string, List<TargetInfo>> resolvedTargets,
            out List<TargetInfo> selectedTargets)
        {
            selectedTargets = new List<TargetInfo>();

            if (resolvedTargets == null)
                return true;

            if (string.IsNullOrWhiteSpace(selector))
            {
                selectedTargets = resolvedTargets.Values.SelectMany(list => list).ToList();
                return true;
            }

            selector = selector.Trim();

            if (selector.Equals("ResolvedTargets", StringComparison.OrdinalIgnoreCase) ||
                selector.Equals("ResolvedTargets.*", StringComparison.OrdinalIgnoreCase) ||
                selector.Equals("ResolvedTargets.All", StringComparison.OrdinalIgnoreCase) ||
                selector.Equals("*", StringComparison.OrdinalIgnoreCase))
            {
                selectedTargets = resolvedTargets.Values.SelectMany(list => list).ToList();
                return true;
            }

            var key = selector;
            const string prefix = "ResolvedTargets.";
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                key = key.Substring(prefix.Length);
            }

            key = key.Trim();
            if (string.IsNullOrEmpty(key))
            {
                selectedTargets = resolvedTargets.Values.SelectMany(list => list).ToList();
                return true;
            }

            if (!resolvedTargets.TryGetValue(key, out var targets))
                return false;

            selectedTargets = targets.ToList();
            return true;
        }
    }
}

