using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class EffectTargetSelectorTests
    {
        [Fact]
        public void TrySelectTargets_EmptySelector_FlattensAllGroups()
        {
            var resolvedTargets = new Dictionary<string, List<TargetInfo>>
            {
                ["primary"] = new() { TargetInfo.ForPlayer(new PlayerState(Guid.NewGuid())) },
                ["secondary"] = new() { TargetInfo.ForPlayer(new PlayerState(Guid.NewGuid())) }
            };

            var ok = EffectTargetSelector.TrySelectTargets(Array.Empty<string>(), resolvedTargets, out var selected);

            Assert.True(ok);
            Assert.Equal(2, selected.Count);
        }

        [Fact]
        public void TrySelectTargets_ResolvedTargetsGroup_SelectsThatGroup()
        {
            var t1 = TargetInfo.ForPlayer(new PlayerState(Guid.NewGuid()));
            var t2 = TargetInfo.ForPlayer(new PlayerState(Guid.NewGuid()));

            var resolvedTargets = new Dictionary<string, List<TargetInfo>>
            {
                ["primary"] = new() { t1 },
                ["secondary"] = new() { t2 }
            };

            var ok = EffectTargetSelector.TrySelectTargets(new[] { "ResolvedTargets.primary" }, resolvedTargets, out var selected);

            Assert.True(ok);
            Assert.Single(selected);
            Assert.Equal(t1, selected[0]);
        }

        [Fact]
        public void TrySelectTargets_UnknownKey_ReturnsFalse()
        {
            var resolvedTargets = new Dictionary<string, List<TargetInfo>>
            {
                ["primary"] = new() { TargetInfo.ForPlayer(new PlayerState(Guid.NewGuid())) }
            };

            var ok = EffectTargetSelector.TrySelectTargets(new[] { "ResolvedTargets.does_not_exist" }, resolvedTargets, out var selected);

            Assert.False(ok);
            Assert.Empty(selected);
        }
    }
}
