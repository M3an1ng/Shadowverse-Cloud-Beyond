using ShadowverseBeyond.Server.Dtos.SignalR.Events; // Ensure base class namespace is used if needed

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating a unit has declared an attack against a player (leader).
    /// Represents the "Attack Timing Point" when the target is a player.
    /// </summary>
    public class PlayerAttackedEventDto : GameEventDto // Inherit from base event DTO
    {
        /// <summary>
        /// Gets the type of the game event.
        /// </summary>
        /// <summary>
        /// The unique identifier of the attacking unit.
        /// </summary>
        public string AttackerUid { get; set; } = string.Empty;

        /// <summary>
        /// The identifier of the player (leader) being attacked.
        /// </summary>
        public string TargetPlayerId { get; set; } = string.Empty; // Use PlayerId for the leader

        // Added constructor according to standard fix pattern
        public PlayerAttackedEventDto(Guid playerId, string attackerUid, string targetPlayerId)
            : base(playerId, "PlayerAttacked") // Use attacker's owner ID for base playerId
        {
            AttackerUid = attackerUid;
            TargetPlayerId = targetPlayerId;
        }

        // Removed override Type and parameterless constructor
    }
}