using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.GameFlow
{
    public class TurnBoundaryCleanupService : ITurnBoundaryCleanupService
    {
        private readonly IBuffService _buffService;

        public TurnBoundaryCleanupService(IBuffService buffService)
        {
            _buffService = buffService ?? throw new ArgumentNullException(nameof(buffService));
        }

        public IReadOnlyList<IGameEvent> CollectCleanupEvents(
            GameContext context,
            TurnBoundaryCleanupTiming timing,
            PlayerState subjectPlayer)
        {
            if (subjectPlayer == null)
            {
                return Array.Empty<IGameEvent>();
            }

            return timing switch
            {
                TurnBoundaryCleanupTiming.TurnEnd => CollectEndOfTurnCleanupEvents(context, subjectPlayer),
                TurnBoundaryCleanupTiming.TurnStart => CollectStartOfTurnCleanupEvents(context, subjectPlayer),
                _ => Array.Empty<IGameEvent>()
            };
        }

        public IReadOnlyList<IGameEvent> CollectEndOfTurnCleanupEvents(
            GameContext context,
            PlayerState endingPlayer)
        {
            var events = new List<IGameEvent>();
            events.AddRange(endingPlayer.CleanupEndOfTurnState().GeneratedEvents);
            if (context != null)
            {
                events.AddRange(_buffService.CollectEndOfTurnExpiryEvents(context));
            }

            return events;
        }

        public IReadOnlyList<IGameEvent> CollectStartOfTurnCleanupEvents(
            GameContext context,
            PlayerState startingPlayer)
        {
            if (context == null)
            {
                return Array.Empty<IGameEvent>();
            }

            return _buffService.CollectStartOfTurnExpiryEvents(context, startingPlayer)
                .Cast<IGameEvent>()
                .ToList();
        }
    }
}
