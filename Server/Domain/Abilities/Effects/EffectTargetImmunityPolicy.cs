using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    public static class EffectTargetImmunityPolicy
    {
        private static readonly HashSet<EffectType> IgnoredEffectTypes = new()
        {
            EffectType.Damage,
            EffectType.ModifyStatus
        };

        public static bool IsImmuneTarget(CardInstance target, GameContext context)
        {
            return IsImmuneTarget(target, context.CurrentEffect, context);
        }

        public static bool IsImmuneTarget(CardInstance target, Effect? effect, GameContext context)
        {
            if (target == null || effect == null)
            {
                return false;
            }

            if (context.IsAuraRefresh)
            {
                return false;
            }

            if (IgnoredEffectTypes.Contains(effect.Type))
            {
                return false;
            }

            if (IsEffectFromTargetItself(target, effect, context))
            {
                return false;
            }

            if (!target.IsFollower || !target.IsOnField)
            {
                return false;
            }

            var keywords = context.BuffService.GetEffectiveKeywords(target, context);
            return keywords.Contains(CardKeyword.EffectImmune);
        }

        private static bool IsEffectFromTargetItself(CardInstance target, Effect effect, GameContext context)
        {
            if (context.SourceCard?.InstanceId == target.InstanceId)
            {
                return true;
            }

            return effect.SourceCardId.HasValue && effect.SourceCardId.Value == target.InstanceId;
        }
    }
}
