using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a Spell card has been played.
    /// </summary>
    public class SpellPlayedEventDto : GameEventDto
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpellPlayedEventDto"/> class.
        /// </summary>
        /// <param name="playerId">The ID of the player playing the spell.</param>
        /// <param name="cardId">The definition ID of the spell card.</param>
        /// <param name="sourceCardUid">The UID of the card instance from the hand.</param>
        /// <param name="targetUid">The UID of the target, if any.</param>
        // Pass both sourcePlayerId (using the 'playerId' parameter) and the event type string to the base constructor
        public SpellPlayedEventDto(string playerId, string cardId, string sourceCardUid, string? targetUid) : base(playerId, "SpellPlayed") // Changed: int cardId -> string cardId
        {
            CardId = cardId;
            SourceCardUid = sourceCardUid;
            TargetUid = targetUid;
        }

        /// <summary>
        /// The definition ID (string format, e.g., "S001") of the spell card that was played.
        /// </summary>
        public string CardId { get; set; } = string.Empty; // Changed: int -> string, added initializer

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