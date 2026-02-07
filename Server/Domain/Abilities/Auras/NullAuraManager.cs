using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Auras
{
    public sealed class NullAuraManager : IAuraManager
    {
        public static readonly NullAuraManager Instance = new();

        private NullAuraManager()
        {
        }

        public DomainOperationResult RegisterAura(AuraConfig auraConfig, IAbility sourceAbility, GameContext context, int auraIndex)
        {
            return new DomainOperationResult { Success = true };
        }

        public DomainOperationResult RefreshAura(
            AuraConfig auraConfig,
            IAbility sourceAbility,
            GameContext context,
            int auraIndex,
            IReadOnlyDictionary<string, List<TargetInfo>> resolvedTargets)
        {
            return new DomainOperationResult { Success = true };
        }

        public DomainOperationResult ClearAura(AuraConfig auraConfig, IAbility sourceAbility, GameContext context, int auraIndex)
        {
            return new DomainOperationResult { Success = true };
        }

        public DomainOperationResult RefreshAuras(IGameEvent gameEvent, GameContext context, TriggerTiming timing)
        {
            return new DomainOperationResult { Success = true };
        }

        public DomainOperationResult UnregisterBySourceCard(Guid sourceCardId, GameContext context)
        {
            return new DomainOperationResult { Success = true };
        }

        public DomainOperationResult UnregisterBySourceAbilities(IEnumerable<Guid> abilityRuntimeIds, GameContext context)
        {
            return new DomainOperationResult { Success = true };
        }
    }
}
