using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using System;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// System lifecycle event: start of a player's turn.
    /// Implements ICommittableEvent; Commit executes turn start operations.
    /// </summary>
    [GameEventMetadata("回合开始", "当一个玩家的回合开始时触发。", EventType.TurnStart)]
    public class TurnStartEvent : DelayedEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("回合开始的玩家", "当前回合开始的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? StartingPlayer => Source?.Player;

        public TurnStartEvent(PlayerState startingPlayer, DelayConfig? delayConfig = null, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.TurnStart, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(startingPlayer);
            ConfigureDelay(delayConfig ?? DelayConfig.QueueDrained());
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = StartingPlayer;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "TurnStartEvent.Commit: Source player is null." };
            }

            var now = DateTime.UtcNow;
            context.CurrentGameState.TurnTimeoutSeconds = GameConstants.Timing.DefaultTurnTimeoutSeconds;
            context.CurrentGameState.TurnStartedAtUtc = now;
            context.CurrentGameState.TurnEndsAtUtc = now.AddSeconds(context.CurrentGameState.TurnTimeoutSeconds);

            // Delegate to PlayerState.StartTurn which generates factual sub-events (PP/EP updates, draw, resets).
            return player.StartTurn(context, DelayConfig.Parent(EventId));
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = StartingPlayer;
            if (player == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Player(player)}回合开始。";
        }
    }
}
