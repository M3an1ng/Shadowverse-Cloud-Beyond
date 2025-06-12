using ShadowverseBeyond.Server.Dtos.SignalR.Events; // Ensure base class namespace is used if needed

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating a unit has declared an attack against another unit.
    /// Represents the "Attack Timing Point" when the target is a unit.
    /// </summary>
    public class UnitAttackedEventDto : GameEventDto // Inherit from base event DTO
    {
        // Removed the problematic 'override Type' property

        /// <summary>
        /// The unique identifier of the attacking unit.
        /// </summary>
        public string AttackerUid { get; set; } = string.Empty;

        /// <summary>
        /// The unique identifier of the target unit being attacked.
        /// </summary>
        public string TargetUid { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitAttackedEventDto"/> class.
        /// </summary>
        /// <param name="sourcePlayerId">The ID of the player initiating the attack.</param>
        /// <param name="attackerUid">The UID of the attacking unit.</param>
        /// <param name="targetUid">The UID of the target unit.</param>
        public UnitAttackedEventDto(Guid sourcePlayerId, string attackerUid, string targetUid)
            : base(sourcePlayerId, "UnitAttacked") // Call base constructor
        {
            AttackerUid = attackerUid;
            TargetUid = targetUid;
        }
    }
}