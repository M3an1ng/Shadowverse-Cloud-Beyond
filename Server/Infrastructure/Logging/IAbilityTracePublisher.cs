using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;

namespace ShadowverseBeyond.Server.Infrastructure.Logging
{
    public interface IAbilityTracePublisher
    {
        void PublishCondition(GameContext context, IAbility ability, bool success, IReadOnlyList<ConditionTraceEntryDto> entries, string message);
        void PublishTarget(GameContext context, IAbility ability, bool success, IReadOnlyList<ConditionTraceEntryDto> entries, int targetCount, string message);
    }
}
