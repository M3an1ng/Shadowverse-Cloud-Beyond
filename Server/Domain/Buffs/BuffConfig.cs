using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    public class BuffConfig
    {
        public string Key { get; set; } = string.Empty;
        public DurationType DurationType { get; set; } = DurationType.Permanent;
        public ValueConfig? DurationParameter { get; set; } = new StaticValueConfig { Value = 0 };
        public bool CanStack { get; set; } = false;

        public List<StatModifierConfig> StatModifiers { get; set; } = new();
        public List<KeywordModifierConfig> KeywordModifiers { get; set; } = new();
        public List<TribeModifierConfig> TribeModifiers { get; set; } = new();
        public List<AbilityKeywordModifierConfig> AbilityKeywordModifiers { get; set; } = new();
        public List<AbilityConfig> GrantedAbilities { get; set; } = new();
    }

    public class BuffTemplateConfig
    {
        public DurationType DurationType { get; set; } = DurationType.Permanent;
        public ValueConfig? DurationParameter { get; set; } = new StaticValueConfig { Value = 0 };
        public bool CanStack { get; set; } = false;

        public List<StatModifierConfig> StatModifiers { get; set; } = new();
        public List<KeywordModifierConfig> KeywordModifiers { get; set; } = new();
        public List<TribeModifierConfig> TribeModifiers { get; set; } = new();
        public List<AbilityKeywordModifierConfig> AbilityKeywordModifiers { get; set; } = new();
        public List<AbilityConfig> GrantedAbilities { get; set; } = new();
    }

    public class StatModifierConfig
    {
        public StatType Stat { get; set; } = StatType.Attack;
        public ModifierLayer Layer { get; set; } = ModifierLayer.Additive;
        public ValueConfig? Value { get; set; }
    }

    public class KeywordModifierConfig
    {
        public CardKeyword Keyword { get; set; } = CardKeyword.None;
        public KeywordOperation Operation { get; set; } = KeywordOperation.Add;
    }

    public class AbilityKeywordModifierConfig
    {
        public AbilityKeyword Keyword { get; set; } = AbilityKeyword.None;
        public ModifierLayer Layer { get; set; } = ModifierLayer.Additive;
        public ValueConfig? Value { get; set; }
    }

    public class TribeModifierConfig
    {
        public string Tribe { get; set; } = string.Empty;
    }

    public static class BuffConfigAdapter
    {
        public static List<BuffConfig> FromTemplate(BuffTemplateConfig? template)
        {
            if (template == null)
            {
                return new List<BuffConfig>();
            }

            return new List<BuffConfig>
            {
                new BuffConfig
                {
                    DurationType = template.DurationType,
                    DurationParameter = template.DurationParameter,
                    CanStack = template.CanStack,
                    StatModifiers = template.StatModifiers ?? new List<StatModifierConfig>(),
                    KeywordModifiers = template.KeywordModifiers ?? new List<KeywordModifierConfig>(),
                    TribeModifiers = template.TribeModifiers ?? new List<TribeModifierConfig>(),
                    AbilityKeywordModifiers = template.AbilityKeywordModifiers ?? new List<AbilityKeywordModifierConfig>(),
                    GrantedAbilities = template.GrantedAbilities ?? new List<AbilityConfig>()
                }
            };
        }
    }
}
