using System;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    // Add the TargetType enum (can be in this file or a separate one)
    public enum TargetIdType
    {
        Undefined, // Default or error state
        Player,
        CardInstance, // Generic card instance
        // Add other specific types if needed
    }

    /// <summary>
    /// Represents a unique identifier for a target that an ability can select.
    /// Can represent a Player or a specific CardInstance.
    /// </summary>
    public readonly struct TargetId : IEquatable<TargetId>
    {
        /// <summary>
        /// The type of the target (Player, CardInstance, etc.).
        /// </summary>
        public TargetIdType Type { get; } // <<< ADDED

        /// <summary>
        /// The specific Guid ID of the target.
        /// If Type is Player, this is the Player's Guid ID.
        /// If Type is CardInstance (or Leader, Unit, Amulet), this is the CardInstance.InstanceId.
        /// </summary>
        public Guid Id { get; }

        // Modified Constructor
        public TargetId(TargetIdType type, Guid id)
        {
            Type = type;
            Id = id;
        }

        // --- Existing IEquatable and operator overloads ---
        public bool Equals(TargetId other) => Type == other.Type && Id == other.Id; // Update Equals
        public override bool Equals(object? obj) => obj is TargetId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Type, Id); // Update GetHashCode

        public static bool operator ==(TargetId left, TargetId right) => left.Equals(right);
        public static bool operator !=(TargetId left, TargetId right) => !left.Equals(right);

        // Update ToString for clarity
        public override string ToString() => $"{Type}:{Id}";

        // --- Updated Static Factory Methods ---
        /// <summary>
        /// Creates a TargetId representing a specific card instance.
        /// </summary>
        public static TargetId Card(Guid instanceId)
        {
            // Assuming default card target is generic CardInstance
            return new TargetId(TargetIdType.CardInstance, instanceId);
        }

        /// <summary>
        /// Creates a TargetId representing a specific player.
        /// </summary>
        /// <param name="playerId">The player's Guid ID.</param>
        public static TargetId Player(Guid playerId) // Takes Guid now
        {
            return new TargetId(TargetIdType.Player, playerId);
        }

        /// <summary>
        /// Represents an empty or undefined target.
        /// </summary>
        public static TargetId Empty => new TargetId(TargetIdType.Undefined, Guid.Empty);

        /// <summary>
        /// Checks if this represents an empty or undefined target.
        /// </summary>
        public bool IsEmpty => Type == TargetIdType.Undefined || Id == Guid.Empty; // Update IsEmpty check
    }
}
