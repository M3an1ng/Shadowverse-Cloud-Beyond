using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    public static class AbilitySuppressionEvaluator
    {
        public static bool Matches(IAbility ability, AbilitySuppressionBuffEffect effect)
        {
            if (ability == null || effect == null)
            {
                return false;
            }

            return effect.RemoveMode switch
            {
                RemoveAbilityMode.All => true,
                RemoveAbilityMode.Self => effect.SourceAbilityRuntimeId.HasValue
                                          && effect.SourceAbilityRuntimeId.Value != Guid.Empty
                                          && ability.RuntimeId == effect.SourceAbilityRuntimeId.Value,
                RemoveAbilityMode.Keyword => effect.AbilityKeyword != AbilityKeyword.None
                                             && ability.HasKeyword(effect.AbilityKeyword),
                RemoveAbilityMode.Id => effect.AbilityDefinitionIds.Count > 0
                                        && ability.DefinitionId > 0
                                        && effect.AbilityDefinitionIds.Contains(ability.DefinitionId),
                _ => false
            };
        }

        public static List<IAbility> CollectSuppressedAbilities(
            IEnumerable<IAbility> abilities,
            IEnumerable<AbilitySuppressionBuffEffect> effects)
        {
            var abilityList = abilities?.ToList() ?? new List<IAbility>();
            if (abilityList.Count == 0)
            {
                return new List<IAbility>();
            }

            var suppressionEffects = effects?.ToList() ?? new List<AbilitySuppressionBuffEffect>();
            if (suppressionEffects.Count == 0)
            {
                return new List<IAbility>();
            }

            return abilityList
                .Where(ability => suppressionEffects.Any(effect => Matches(ability, effect)))
                .ToList();
        }
    }
}
