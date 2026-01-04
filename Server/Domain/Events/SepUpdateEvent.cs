using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a player's Super Evolution Points (SEP) change.
    /// </summary>
    [GameEventMetadata("SEP变化", "当玩家的当前SEP值发生变化时触发的事件。", EventType.SepUpdated)]
    public class SepUpdateEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("当前SEP值", "更新后的当前SEP值。")]
        public int CurrentSep { get; }

        [GameEventParameter("目标玩家", "SEP值被更新的玩家。")]
        public PlayerState? TargetPlayer => Source?.Player;

        public SepUpdateEvent(PlayerState player, int currentSep)
            : base(EventType.SepUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            CurrentSep = currentSep;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = TargetPlayer;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "SepUpdateEvent.Commit: Source player is null." };
            }

            player.ApplySuperEvolutionPointsCurrentChange(CurrentSep);
            return new DomainOperationResult { Success = true };
        }

        public string GetAnnouncement(GameState gameState)
        {
            var playerId = TargetPlayer?.PlayerId;
            return playerId.HasValue
                ? $"sep player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)} current={CurrentSep}"
                : "sep";
        }
    }
}
