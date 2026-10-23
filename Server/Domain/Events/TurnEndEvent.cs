using System;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// System lifecycle event: end of a player's turn.
    /// Implements ICommittableEvent; boundary cleanup is scheduled separately
    /// after turn-end triggered abilities and effects finish.
    /// </summary>
    [GameEventMetadata("回合结束", "当一个玩家的回合结束时触发。", EventType.TurnEnd)]
    public class TurnEndEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("回合结束的玩家", "当前回合结束的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? EndingPlayer => Source?.Player;

        public TurnEndEvent(PlayerState endingPlayer, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.TurnEnd, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(endingPlayer);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (EndingPlayer == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "TurnEndEvent.Commit: Source player is null." };
            }

            return EndingPlayer.EndTurn();
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = EndingPlayer;
            if (player == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Player(player)}回合结束。";
        }
    }
}
