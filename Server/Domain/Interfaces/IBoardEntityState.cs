using System;

namespace Shadowverse.Domain.Interfaces
{
    /// <summary>
    /// Represents an entity on the game board that has health and can be targeted.
    /// This includes both Players (Leaders) and Units (Followers).
    /// </summary>
    public interface IBoardEntityState
    {
        /// <summary>
        /// Unique identifier for the entity on the board.
        /// For Players, this might be their PlayerId or a specific Leader Uid.
        /// For Units, this is their assigned Uid.
        /// </summary>
        string Uid { get; }

        /// <summary>
        /// Current health of the entity.
        /// </summary>
        int Health { get; set; } // Need setter for ApplyDamage

        // Potentially add other common properties later if needed,
        // e.g., Attack, MaxHealth, Type (Player/Unit)
    }
}