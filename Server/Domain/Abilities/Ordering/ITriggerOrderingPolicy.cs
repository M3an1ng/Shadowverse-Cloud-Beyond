using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Ordering
{
    public interface ITriggerOrderingPolicy
    {
        IReadOnlyList<OrderedTriggeredAbility> OrderTriggeredAbilities(
            IEnumerable<IAbility> triggeredAbilities,
            IGameEvent gameEvent,
            GameContext context,
            IReadOnlyDictionary<Guid, long> registrationOrders);
    }
}
