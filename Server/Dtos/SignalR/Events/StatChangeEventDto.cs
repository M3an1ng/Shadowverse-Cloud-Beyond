using ShadowverseBeyond.Server.Dtos.SignalR; // Assuming GameEventDto is here
using System.Diagnostics.CodeAnalysis; // Add required using

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a unit's core combat stats (Attack, Health) have changed.
    /// Cost is optional and included when the effective cost changed with the same stat update.
    /// </summary>
    public class StatChangeEventDto : GameEventDto
    {
        // Constructor to initialize properties and call the base constructor
        [SetsRequiredMembers] // Add attribute here
        public StatChangeEventDto(
            Guid sourcePlayerId,
            Guid targetEntityUid,
            int? newAttack,
            int? newCurrentHealth,
            int? newMaxHealth,
            int? newCost = null)
            : base(sourcePlayerId, "StatChange")
        {
            TargetEntityUid = targetEntityUid;
            NewAttack = newAttack;
            NewCurrentHealth = newCurrentHealth;
            NewMaxHealth = newMaxHealth;
            NewCost = newCost;
        }
        /// <summary>
        /// The unique identifier of the entity (Unit or Player Leader) whose stats changed.
        /// </summary>
        public required Guid TargetEntityUid { get; set; }

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

        /// <summary>
        /// The new effective cost. Null if cost didn't change or is not part of this update.
        /// </summary>
        public int? NewCost { get; set; }
    }
}
