using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// System lifecycle event: start of a player's turn.
    /// Implements ICommittableEvent; Commit executes turn start operations.
    /// </summary>
    [GameEventMetadata("回合开始时", "当一个玩家的回合开始时触发。", EventType.TurnStart)]
    public class TurnStartEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("回合开始的玩家", "当前回合开始的玩家。")]
        public PlayerState? StartingPlayer => Source?.Player;

        public TurnStartEvent(PlayerState startingPlayer)
            : base(EventType.TurnStart)
        {
            Source = TargetInfo.ForPlayer(startingPlayer);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = StartingPlayer;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "TurnStartEvent.Commit: Source player is null." };
            }

            // Delegate to PlayerState.StartTurn which generates factual sub-events (PP/EP updates, draw, resets).
            return player.StartTurn(context.CurrentGameState);
        }

        public string GetAnnouncement(GameState gameState)
        {
            var playerId = StartingPlayer?.PlayerId;
            return playerId.HasValue
                ? $"turn-start player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)}"
                : "turn-start";
        }
    }
}
