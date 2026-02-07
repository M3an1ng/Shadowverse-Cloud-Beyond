using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Auras
{
    public interface IAuraManager
    {
        DomainOperationResult RegisterAura(AuraConfig auraConfig, IAbility sourceAbility, GameContext context, int auraIndex);

        DomainOperationResult RefreshAura(
            AuraConfig auraConfig,
            IAbility sourceAbility,
            GameContext context,
            int auraIndex,
            IReadOnlyDictionary<string, List<TargetInfo>> resolvedTargets);

        DomainOperationResult ClearAura(
            AuraConfig auraConfig,
            IAbility sourceAbility,
            GameContext context,
            int auraIndex);

        DomainOperationResult RefreshAuras(IGameEvent gameEvent, GameContext context, TriggerTiming timing);

        DomainOperationResult UnregisterBySourceCard(Guid sourceCardId, GameContext context);

        DomainOperationResult UnregisterBySourceAbilities(IEnumerable<Guid> abilityRuntimeIds, GameContext context);
    }
}
