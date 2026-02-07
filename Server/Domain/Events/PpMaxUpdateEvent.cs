using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event to update only the max Play Points (PP) value.
    /// </summary>
    [GameEventMetadata("PP最大值更新", "当玩家的PP最大值发生变化时触发的事件。", EventType.PpMaxUpdated)]
    public class PpMaxUpdateEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("新的PP最大值", "更新后的PP最大值。")]
        public int MaxPp { get; }

        [GameEventParameter("目标玩家", "PP最大值被更新的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? TargetPlayer => Source?.Player;

        public PpMaxUpdateEvent(PlayerState player, int maxPp, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.PpMaxUpdated, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            MaxPp = maxPp;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = TargetPlayer;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "PpMaxUpdateEvent.Commit: Source player is null." };
            }

            player.ApplyPlayPointsMaxChange(MaxPp);
            return new DomainOperationResult { Success = true };
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = TargetPlayer;
            if (player == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Player(player)}的PP上限变为 {AnnouncementTokens.Number(MaxPp)}。";
        }
    }
}
