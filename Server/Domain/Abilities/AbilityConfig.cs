using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Abilities.JsonConverters;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Represents the configuration for a card ability, potentially parsed from DSL.
    /// </summary>
    public class AbilityConfig
    {
        [JsonIgnore] // This will be populated from the database entity's Id, not the JSON config.
        public long Id { get; set; }
        public int CardId { get; set; } // 添加 CardId 属性
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public EffectKeyword AbilityTag { get; set; } = EffectKeyword.None;
        public int? EnhanceCost { get; set; }
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
        public List<EffectConfig> Effects { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public string Version { get; set; } = "1.0";
    } // End of AbilityConfig class
} // End of namespace
