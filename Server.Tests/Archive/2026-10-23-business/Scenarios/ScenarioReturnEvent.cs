using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Tests.Scenarios
{
    internal sealed class ScenarioReturnEvent : ReturnEvent
    {
        public ScenarioReturnEvent(PlayerState player, CardInstance card, EventReasonSource reason)
            : base(card, reason)
        {
            Source = TargetInfo.ForPlayer(player);
        }
    }
}
