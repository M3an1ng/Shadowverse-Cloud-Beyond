using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Constants;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class AbilityKeywordParsingTests
    {
        [Fact]
        public void NormalizeKeywords_AddsEnhanceWhenActivateCostProvided()
        {
            var config = new AbilityConfig
            {
                AbilityKeywords = new List<AbilityKeywordConfig>
                {
                    new AbilityKeywordConfig { Keyword = AbilityKeyword.Fanfare }
                },
                ActivateCost = 5
            };

            config.NormalizeKeywords();

            Assert.True(config.HasKeyword(AbilityKeyword.Fanfare));
            Assert.Equal(5, config.GetKeywordValue(AbilityKeyword.Enhance));
        }

        [Fact]
        public void NormalizeKeywords_DoesNotOverrideEnhanceValue()
        {
            var config = new AbilityConfig
            {
                AbilityKeywords = new List<AbilityKeywordConfig>
                {
                    new AbilityKeywordConfig { Keyword = AbilityKeyword.Enhance, Value = 7 }
                },
                ActivateCost = 5
            };

            config.NormalizeKeywords();

            Assert.Equal(7, config.GetKeywordValue(AbilityKeyword.Enhance));
        }

        [Fact]
        public void BroadcastOnTriggerFalse_DoesNotHideBuffDisplayByDefault()
        {
            var config = new AbilityConfig
            {
                BroadcastOnTrigger = false
            };

            Assert.False(config.BroadcastOnTrigger);
            Assert.True(config.BuffDisplay);
        }
    }
}
