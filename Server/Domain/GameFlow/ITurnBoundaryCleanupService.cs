using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.GameFlow
{
    public interface ITurnBoundaryCleanupService
    {
        IReadOnlyList<IGameEvent> CollectCleanupEvents(
            GameContext context,
            TurnBoundaryCleanupTiming timing,
            PlayerState subjectPlayer);
    }
}
