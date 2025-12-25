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

        [GameEventParameter("目标玩家", "PP最大值被更新的玩家。")]
        public PlayerState? TargetPlayer => Source?.Player;

        public PpMaxUpdateEvent(PlayerState player, int maxPp)
            : base(EventType.PpMaxUpdated)
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
            var playerId = TargetPlayer?.PlayerId;
            return playerId.HasValue
                ? $"pp-max player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)} max={MaxPp}"
                : "pp-max";
        }
    }
}
