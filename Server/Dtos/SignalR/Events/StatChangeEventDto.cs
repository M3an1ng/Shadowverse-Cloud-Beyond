using ShadowverseBeyond.Server.Dtos.SignalR; // Assuming GameEventDto is here

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a unit's core combat stats (Attack, Health) have changed.
    /// </summary>
    public class StatChangeEventDto : GameEventDto
    {
        public override string Type => "StatChange";

        /// <summary>
        /// The unique identifier of the entity (Unit or Player Leader) whose stats changed.
        /// </summary>
        public required string TargetEntityUid { get; set; }

        /// <summary>
        /// The new attack value. Null if attack didn't change.
        /// </summary>
        public int? NewAttack { get; set; }

        /// <summary>
        /// The new current health value. Null if current health didn't change.
        /// </summary>
        public int? NewCurrentHealth { get; set; }

        /// <summary>
        /// The new maximum health value. Null if maximum health didn't change.
        /// </summary>
        public int? NewMaxHealth { get; set; }
    }
}