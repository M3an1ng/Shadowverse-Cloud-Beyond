using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class BuffNonStackableEffectIndexTests
    {
        [Fact]
        public void NonStackableBuffsFromDifferentEffectsDoNotReplace()
        {
            var engine = new BuffStackingEngine(NullLogger<BuffStackingEngine>.Instance);
            var sourceAbilityId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            var existing = CreateKeywordBuff(sourceAbilityId, targetId);
            existing.EffectKey = BuildNonStackableKey(sourceAbilityId, 0);

            var incoming = CreateKeywordBuff(sourceAbilityId, targetId);
            incoming.EffectKey = BuildNonStackableKey(sourceAbilityId, 1);

            var result = engine.EvaluateStacking(incoming, new List<Buff> { existing });

            Assert.Empty(result.BuffsToRemove);
            Assert.True(result.AllowAdd);
        }

        [Fact]
        public void NonStackableBuffsFromSameEffectReplace()
        {
            var engine = new BuffStackingEngine(NullLogger<BuffStackingEngine>.Instance);
            var sourceAbilityId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            var existing = CreateKeywordBuff(sourceAbilityId, targetId);
            existing.EffectKey = BuildNonStackableKey(sourceAbilityId, 2);

            var incoming = CreateKeywordBuff(sourceAbilityId, targetId);
            incoming.EffectKey = BuildNonStackableKey(sourceAbilityId, 2);

            var result = engine.EvaluateStacking(incoming, new List<Buff> { existing });

            Assert.True(result.AllowAdd);
            Assert.Empty(result.BuffsToRemove);
        }

        [Fact]
        public void BuffWithoutKeyDoesNotReplace()
        {
            var engine = new BuffStackingEngine(NullLogger<BuffStackingEngine>.Instance);
            var sourceAbilityId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            var existing = CreateKeywordBuff(sourceAbilityId, targetId);
            existing.EffectKey = BuildNonStackableKey(sourceAbilityId, 3);

            var incoming = CreateKeywordBuff(sourceAbilityId, targetId);

            var result = engine.EvaluateStacking(incoming, new List<Buff> { existing });

            Assert.Empty(result.BuffsToRemove);
        }

        private static Buff CreateKeywordBuff(Guid sourceAbilityId, Guid targetId)
        {
            return new Buff(
                sourceAbilityId: sourceAbilityId,
                targetCardInstanceId: targetId,
                currentTurn: 1,
                effects: new List<IBuffEffect>
                {
                    new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.Ward })
                });
        }

        private static string BuildNonStackableKey(Guid sourceAbilityId, int effectIndex)
        {
            return $"{sourceAbilityId:N}:{effectIndex}";
        }
    }
}
