using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class BuffConfigAdapterTests
    {
        [Fact]
        public void FromTemplate_preserves_legacy_template_values()
        {
            var legacyTemplate = new BuffTemplateConfig
            {
                DurationType = DurationType.Permanent,
                DurationParameter = 0,
                CanStack = true,
                StatModifiers = new List<StatModifierConfig>
                {
                    new StatModifierConfig
                    {
                        Stat = StatType.Attack,
                        Layer = ModifierLayer.Additive,
                        Value = new StaticValueConfig { Value = 2 }
                    }
                },
                KeywordModifiers = new List<KeywordModifierConfig>
                {
                    new KeywordModifierConfig
                    {
                        Keyword = CardKeyword.Storm,
                        Operation = KeywordOperation.Add
                    }
                }
            };

            var configs = BuffConfigAdapter.FromTemplate(legacyTemplate);

            Assert.Single(configs);
            var config = configs[0];
            Assert.Equal(legacyTemplate.DurationType, config.DurationType);
            Assert.Equal(legacyTemplate.DurationParameter, config.DurationParameter);
            Assert.Equal(legacyTemplate.CanStack, config.CanStack);
            Assert.Equal(legacyTemplate.StatModifiers.Count, config.StatModifiers.Count);
            Assert.Equal(legacyTemplate.KeywordModifiers.Count, config.KeywordModifiers.Count);
            Assert.Equal(legacyTemplate.StatModifiers[0].Stat, config.StatModifiers[0].Stat);
            Assert.Equal(legacyTemplate.StatModifiers[0].Layer, config.StatModifiers[0].Layer);
            Assert.Equal(legacyTemplate.KeywordModifiers[0].Keyword, config.KeywordModifiers[0].Keyword);
            Assert.Equal(legacyTemplate.KeywordModifiers[0].Operation, config.KeywordModifiers[0].Operation);
        }
    }
}
