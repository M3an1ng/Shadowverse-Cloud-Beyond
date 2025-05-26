using ShadowverseBeyond.Server.Dtos.SignalR.Events; // Ensure base class namespace is used if needed

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating a unit has declared an attack against another unit.
    /// Represents the "Attack Timing Point" when the target is a unit.
    /// </summary>
    public class UnitAttackedEventDto : GameEventDto // Inherit from base event DTO
    {
        /// <summary>
        /// Gets the type of the game event.
        /// </summary>
        public override string Type => "UnitAttacked";
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
        /// Sets the event type for client-side discrimination.
        /// </summary>
        public UnitAttackedEventDto()
        {
        }
    }
}