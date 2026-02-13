using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Abilities.JsonConverters;
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Constants;
using System.Text.Json.Serialization;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Represents the configuration for a card ability, potentially parsed from DSL.
    /// </summary>
    public class AbilityConfig
    {
        private Dictionary<string, ValueConfig> _properties = new(StringComparer.OrdinalIgnoreCase);
        [JsonIgnore] // This will be populated from the database entity's Id, not the JSON config.
        public long Id { get; set; }
        public int CardId { get; set; } // 添加 CardId 属性
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [JsonConverter(typeof(AbilityKeywordConfigListConverter))]
        public List<AbilityKeywordConfig> AbilityKeywords { get; set; } = new();

        public int? ActivateCost { get; set; }
        public AbilityType Type { get; set; }
        public TriggerConfig Trigger { get; set; } = new();
        /// <summary>
        /// 卡牌打出前的预判断条件。
        /// 只有满足这些条件（以及基础条件如费用、场地），卡牌才能被打出。
        /// </summary>
        public List<ConditionConfig> PreConditions { get; set; } = new();
        public List<ConditionConfig> Conditions { get; set; } = new();
        /// <summary>
        /// Defines all targets required by this ability.
        /// The key is a unique identifier for the target (e.g., "primaryTarget", "aoeVictims"),
        /// which can be referenced by Effect Handlers and in the DSL.
        /// </summary>
        [JsonConverter(typeof(SingleTargetConfigToDictionaryConverter))]
        public Dictionary<string, TargetConfig> Targets { get; set; } = new();

        public Dictionary<string, ValueConfig> Properties
        {
            get => _properties;
            set
            {
                if (value == null)
                {
                    _properties = new Dictionary<string, ValueConfig>(StringComparer.OrdinalIgnoreCase);
                    return;
                }

                _properties = new Dictionary<string, ValueConfig>(value, StringComparer.OrdinalIgnoreCase);
            }
        }

        public List<EffectConfig> Effects { get; set; } = new();
        public List<AuraConfig> Auras { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
        public string Version { get; set; } = "1.0";
        public List<CounterRegistrationConfig> CounterRegistrations { get; set; } = new();
        public List<PlayerCounterRegistrationConfig> PlayerCounterRegistrations { get; set; } = new();

        [JsonIgnore]
        public AbilityKeyword PrimaryKeyword => AbilityKeywords.FirstOrDefault()?.Keyword ?? AbilityKeyword.None;

        public bool HasKeyword(AbilityKeyword keyword)
        {
            return AbilityKeywords.Any(k => k.Keyword == keyword);
        }

        public int? GetKeywordValue(AbilityKeyword keyword)
        {
            return AbilityKeywords.FirstOrDefault(k => k.Keyword == keyword)?.Value;
        }

        public void NormalizeKeywords()
        {
            if (!ActivateCost.HasValue)
            {
                return;
            }

            var enhanceEntry = AbilityKeywords.FirstOrDefault(k => k.Keyword == AbilityKeyword.Enhance);
            if (enhanceEntry == null)
            {
                AbilityKeywords.Add(new AbilityKeywordConfig
                {
                    Keyword = AbilityKeyword.Enhance,
                    Value = ActivateCost
                });
            }
            else if (!enhanceEntry.Value.HasValue)
            {
                enhanceEntry.Value = ActivateCost;
            }
        }
    } // End of AbilityConfig class

    public class CounterRegistrationConfig
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int InitialValue { get; set; }
    }

    public class PlayerCounterRegistrationConfig
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int InitialValue { get; set; }
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;
    }
} // End of namespace
