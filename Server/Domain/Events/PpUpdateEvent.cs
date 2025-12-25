using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events; // EventType, GameContext
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a player's Play Points (PP) change.
    /// </summary>
    [GameEventMetadata("PP更新", "当玩家的当前PP值发生变化时触发的事件。", EventType.PpUpdated)]
    public class PpUpdateEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("当前PP值", "更新后的当前PP值。")]
        public int CurrentPp { get; }

        [GameEventParameter("目标玩家", "PP值被更新的玩家。")]
        public PlayerState? TargetPlayer => Source?.Player;

        public PpUpdateEvent(PlayerState player, int currentPp)
            : base(EventType.PpUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            CurrentPp = currentPp;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = TargetPlayer;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "PpUpdateEvent.Commit: Source player is null." };
            }

            player.ApplyPlayPointsCurrentChange(CurrentPp);
            return new DomainOperationResult { Success = true };
        }

        public string GetAnnouncement(GameState gameState)
        {
            var playerId = TargetPlayer?.PlayerId;
            return playerId.HasValue
                ? $"pp player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)} current={CurrentPp}"
                : "pp";
        }
    }
}
