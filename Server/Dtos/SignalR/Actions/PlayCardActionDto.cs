using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Actions
{
    /// <summary>
    /// DTO for the play card action.
    /// </summary>
    public class PlayCardActionDto
    {
        /// <summary>
        /// The unique identifier (UID) of the card instance being played from the hand.
        /// Using string for flexibility, but could be Guid if UIDs are Guids.
        /// </summary>
        public Guid CardUid { get; set; } = Guid.Empty;

        /// <summary>
        /// The unique identifier (UID) of the target, if the card requires one (e.g., targeted spell or Fanfare effect).
        /// Can be a unit UID or a player/leader identifier (e.g., "Player1Leader", "Player2Leader").
        /// Null or empty if no target is required or selected.
        /// </summary>
        public Guid? TargetUid { get; set; }

        // Potential future additions:
        // public int? SelectedOption { get; set; } // For cards with choices
        // public List<string> MultiTargetUids { get; set; } // For multi-target effects
    }
}