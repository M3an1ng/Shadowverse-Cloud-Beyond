using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event to update only the max Evolution Points (EP) value.
    /// </summary>
    [GameEventMetadata("EP最大值变化", "当玩家的EP最大值发生变化时触发的事件。", EventType.EpMaxUpdated)]
    public class EpMaxUpdateEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("新的EP最大值", "更新后的EP最大值。")]
        public int MaxEp { get; }

        [GameEventParameter("目标玩家", "EP最大值被更新的玩家。")]
        public PlayerState? TargetPlayer => Source?.Player;

        public EpMaxUpdateEvent(PlayerState player, int maxEp)
            : base(EventType.EpMaxUpdated)
        {
            Source = TargetInfo.ForPlayer(player);
            MaxEp = maxEp;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = TargetPlayer;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "EpMaxUpdateEvent.Commit: Source player is null." };
            }

            player.ApplyEvolutionPointsMaxChange(MaxEp);
            return new DomainOperationResult { Success = true };
        }

        public string GetAnnouncement(GameState gameState)
        {
            var playerId = TargetPlayer?.PlayerId;
            return playerId.HasValue
                ? $"ep-max player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)} max={MaxEp}"
                : "ep-max";
        }
    }
}
