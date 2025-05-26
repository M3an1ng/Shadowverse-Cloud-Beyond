using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a Spell card has been played.
    /// </summary>
    public class SpellPlayedEventDto : GameEventDto
    {
        public override string Type => "SpellPlayed";

        /// <summary>
        /// The identifier of the player who played the spell.
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// The definition ID of the spell card that was played.
        /// </summary>
        public int CardId { get; set; }

        /// <summary>
        /// The UID of the card instance from the hand that was used to play this spell.
        /// Helps the client remove the card from the hand visually.
        /// </summary>
        public string SourceCardUid { get; set; } = string.Empty;

        /// <summary>
        /// The unique identifier (UID) of the target, if the spell had one.
        /// Null or empty if the spell was not targeted.
        /// </summary>
        public string? TargetUid { get; set; }

        // Note: The actual effects of the spell (damage, draw, etc.)
        // should likely be communicated through separate events like
        // DamageDealtEventDto, HandUpdateEventDto, etc., triggered
        // immediately after this event (or bundled).
        // This event primarily signals the *act* of playing the spell.
    }
}