using System;
using System.Diagnostics.CodeAnalysis; // For [SetsRequiredMembers]
using ShadowverseBeyond.Server.Dtos; // For UnitStateDto (Ensure namespace is correct if different)

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a unit (Follower) has been played to the field.
    /// </summary>
    public class UnitPlayedEventDto : GameEventDto
    {
        // No Type override here - it's handled by the base constructor call

        /// <summary>
        /// The identifier of the player who played the unit.
        /// </summary>
        public required string PlayerId { get; set; } // Marked as required

        /// <summary>
        /// The state of the unit as it enters the field.
        /// Assumes UnitStateDto contains necessary info like UID, CardId, CurrentAttack, CurrentHealth, isEvolved, canAttack, etc.
        /// </summary>
        public required UnitStateDto UnitData { get; set; } // Marked as required

        /// <summary>
        /// The UID of the card instance from the hand that was used to play this unit.
        /// Helps the client remove the card from the hand visually.
        /// </summary>
        public required string SourceCardUid { get; set; } // Marked as required

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitPlayedEventDto"/> class.
        /// </summary>
        /// <param name="sourcePlayerId">The player ID triggering the event (usually the one playing the card).</param>
        /// <param name="playerId">The identifier of the player who owns and played the unit.</param>
        /// <param name="unitData">The state of the unit as it enters the field.</param>
        /// <param name="sourceCardUid">The UID of the card from the hand that was played.</param>
        [SetsRequiredMembers]
        public UnitPlayedEventDto(string sourcePlayerId, string playerId, UnitStateDto unitData, string sourceCardUid)
            : base(sourcePlayerId, "UnitPlayed") // Correctly call base constructor
        {
            PlayerId = playerId;
            UnitData = unitData;
            SourceCardUid = sourceCardUid;
        }
    }
}