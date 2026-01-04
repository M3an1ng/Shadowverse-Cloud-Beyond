using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// System lifecycle event: end of a player's turn.
    /// Implements ICommittableEvent; Commit executes turn-end cleanup.
    /// </summary>
    [GameEventMetadata("回合结束", "当一个玩家的回合结束时触发。", EventType.TurnEnd)]
    public class TurnEndEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("回合结束的玩家", "当前回合结束的玩家。")]
        public PlayerState? EndingPlayer => Source?.Player;

        public TurnEndEvent(PlayerState endingPlayer)
            : base(EventType.TurnEnd)
        {
            Source = TargetInfo.ForPlayer(endingPlayer);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (EndingPlayer == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "TurnEndEvent.Commit: Source player is null." };
            }

            // Delegate to PlayerState.EndTurn which generates turn-end factual events and resets counters.
            return EndingPlayer.EndTurn();
        }

        public string GetAnnouncement(GameState gameState)
        {
            var playerId = EndingPlayer?.PlayerId;
            return playerId.HasValue
                ? $"turn-end player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)}"
                : "turn-end";
        }
    }
}
