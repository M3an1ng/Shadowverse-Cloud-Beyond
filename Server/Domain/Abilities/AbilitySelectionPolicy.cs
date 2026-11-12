using System;
using ShadowverseBeyond.Server.Domain.Effects;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    // Inspect before invoking: skipping a choice must not leave earlier effects committed.
    public static class AbilitySelectionPolicy
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static bool RequiresPlayerSelection(AbilityConfig config)
            => Inspect(config, new HashSet<AbilityConfig>(), 0);

        private static bool Inspect(AbilityConfig config, HashSet<AbilityConfig> path, int depth)
        {
            if (!config.IsEnabled) return false;
            if (depth > 64 || !path.Add(config)) return true;
            try
            {
                if (config.Targets.Values.Any(t => t.IsEnabled && t.PlayerChoiceOptions?.IsRequired == true))
                    return true;
                foreach (var effect in config.Effects.Where(e => e.IsEnabled))
                {
                    if (effect.Type is EffectType.Choose or EffectType.Discover) return true;
                    if (effect.ChainedAbilities.Any(a => Inspect(a, path, depth + 1))) return true;
                    if (effect.Type is not (EffectType.Sequence or EffectType.Random)) continue;
                    if (!effect.Parameters.TryGetValue("AbilityConfigs", out var value)) continue;
                    // Dynamic branch lists cannot safely be inspected without executing a formula.
                    if (value is not StaticValueConfig fixedValue) return true;
                    var branches = fixedValue.Value is IEnumerable<AbilityConfig> configs
                        ? configs
                        : JsonSerializer.Deserialize<List<AbilityConfig>>(
                            JsonSerializer.Serialize(fixedValue.Value, JsonOptions), JsonOptions) ?? new();
                    if (branches.Any(a => Inspect(a, path, depth + 1))) return true;
                }
                return false;
            }
            finally { path.Remove(config); }
        }
    }
}
