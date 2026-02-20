using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class TargetConfigCompatibilityTests
    {
        [Fact]
        public void GetEffectiveTargetHandlingStrategy_Default_ReturnsDoNotIntercept()
        {
            var config = new TargetConfig();

            var strategy = config.GetEffectiveTargetHandlingStrategy();

            Assert.Equal(TargetHandlingStrategy.DoNotIntercept, strategy);
        }

        [Fact]
        public void GetEffectiveTargetHandlingStrategy_RequireValidTargetTrueWithoutExplicitStrategy_ReturnsBlockPlayerOperation()
        {
            var config = new TargetConfig
            {
                RequireValidTarget = true
            };

            var strategy = config.GetEffectiveTargetHandlingStrategy();

            Assert.Equal(TargetHandlingStrategy.BlockPlayerOperation, strategy);
        }

        [Fact]
        public void ShouldBlockPlayerOperationOnInsufficientTargets_RequireValidTargetFalse_ReturnsFalse()
        {
            var config = new TargetConfig
            {
                RequireValidTarget = false,
                TargetHandling = TargetHandlingStrategy.DoNotIntercept
            };

            var result = config.ShouldBlockPlayerOperationOnInsufficientTargets();

            Assert.False(result);
        }

        [Fact]
        public void ShouldBlockPlayerOperationOnInsufficientTargets_RequireValidTargetTrueAndBlockExecutionOnly_ReturnsFalse()
        {
            var config = new TargetConfig
            {
                RequireValidTarget = true,
                TargetHandling = TargetHandlingStrategy.BlockAbilityExecutionOnly
            };

            var result = config.ShouldBlockPlayerOperationOnInsufficientTargets();

            Assert.False(result);
        }

        [Fact]
        public void ShouldBlockPlayerOperationOnInsufficientTargets_RequireValidTargetTrueAndBlockPlayerOperation_ReturnsTrue()
        {
            var config = new TargetConfig
            {
                RequireValidTarget = true,
                TargetHandling = TargetHandlingStrategy.BlockPlayerOperation
            };

            var result = config.ShouldBlockPlayerOperationOnInsufficientTargets();

            Assert.True(result);
        }

        [Fact]
        public void GetEffectiveTargetHandlingStrategy_InvalidEnum_FallsBackToDoNotIntercept()
        {
            var config = new TargetConfig
            {
                TargetHandling = (TargetHandlingStrategy)999
            };

            var strategy = config.GetEffectiveTargetHandlingStrategy();

            Assert.Equal(TargetHandlingStrategy.DoNotIntercept, strategy);
        }
    }
}
