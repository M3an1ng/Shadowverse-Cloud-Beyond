using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event to update only the max Super Evolution Points (SEP) value.
    /// </summary>
    [GameEventMetadata("SEP最大值变化", "当玩家的SEP最大值发生变化时触发的事件。", EventType.SepMaxUpdated)]
    public class SepMaxUpdateEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("新的SEP最大值", "更新后的SEP最大值。")]
        public int MaxSep { get; }

        [GameEventParameter("目标玩家", "SEP最大值被更新的玩家。")]
        public PlayerState? TargetPlayer => Source?.Player;

        public SepMaxUpdateEvent(PlayerState player, int maxSep)
            : base(EventType.SepMaxUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            MaxSep = maxSep;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = TargetPlayer;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "SepMaxUpdateEvent.Commit: Source player is null." };
            }

            player.ApplySuperEvolutionPointsMaxChange(MaxSep);
            return new DomainOperationResult { Success = true };
        }

        public string GetAnnouncement(GameState gameState)
        {
            var playerId = TargetPlayer?.PlayerId;
            return playerId.HasValue
                ? $"sep-max player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)} max={MaxSep}"
                : "sep-max";
        }
    }
}
