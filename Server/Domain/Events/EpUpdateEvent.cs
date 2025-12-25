using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events; // EventType, GameContext
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a player's Evolution Points (EP) change.
    /// </summary>
    [GameEventMetadata("EP更新", "当玩家的当前EP值发生变化时触发的事件。", EventType.EpUpdated)]
    public class EpUpdateEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("当前EP值", "更新后的当前EP值。")]
        public int CurrentEp { get; }

        [GameEventParameter("目标玩家", "EP值被更新的玩家。")]
        public PlayerState? TargetPlayer => Source?.Player;

        public EpUpdateEvent(PlayerState player, int currentEp)
            : base(EventType.EpUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            CurrentEp = currentEp;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = TargetPlayer;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "EpUpdateEvent.Commit: Source player is null." };
            }

            player.ApplyEvolutionPointsCurrentChange(CurrentEp);
            return new DomainOperationResult { Success = true };
        }

        public string GetAnnouncement(GameState gameState)
        {
            var playerId = TargetPlayer?.PlayerId;
            return playerId.HasValue
                ? $"ep player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)} current={CurrentEp}"
                : "ep";
        }
    }
}
