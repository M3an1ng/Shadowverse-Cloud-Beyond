using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    public static class EffectTargetImmunityPolicy
    {
        public static bool IsImmuneTarget(CardInstance target, GameContext context) => IsImmuneTarget(target, context.CurrentEffect, context);
        public static bool IsImmuneTarget(CardInstance target, Effect? effect, GameContext context)
        {
            if (effect == null) return false; // Combat/system actions have no card ability effect.
            var sourceId = effect.SourceCardId ?? context.SourceAbility?.SourceCardId ?? context.SourceCard?.InstanceId;
            return IsImmuneToOtherCard(target, sourceId, context);
        }
        public static bool IsImmuneToOtherCard(CardInstance target, Guid? sourceId, GameContext context)
        {
            if (target == null || !target.IsOnField || !sourceId.HasValue || sourceId == target.InstanceId) return false;
            return context.BuffService.GetEffectiveKeywords(target, context).Contains(CardKeyword.EffectImmune);
        }
    }
}
