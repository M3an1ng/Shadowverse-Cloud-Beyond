using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a unit (Follower) has been played to the field.
    /// </summary>
    public class UnitPlayedEventDto : GameEventDto
    {
        public override string Type => "UnitPlayed";

        /// <summary>
        /// The identifier of the player who played the unit.
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// The state of the unit as it enters the field.
        /// Assumes UnitStateDto contains necessary info like UID, CardId, CurrentAttack, CurrentHealth, isEvolved, canAttack, etc.
        /// </summary>
        public UnitStateDto UnitData { get; set; } = new UnitStateDto(); // Use existing DTO

        /// <summary>
        /// The UID of the card instance from the hand that was used to play this unit.
        /// Helps the client remove the card from the hand visually.
        /// </summary>
        public string SourceCardUid { get; set; } = string.Empty;
    }
}