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
    [GameEventMetadata("SEP Update", "Triggered when a player's current SEP value changes.", EventType.SepUpdated)]
    public class SepUpdateEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("Current SEP", "Updated current SEP value.")]
        public int CurrentSep { get; }

        [GameEventParameter("Target Player", "Player whose SEP changed.")]
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
