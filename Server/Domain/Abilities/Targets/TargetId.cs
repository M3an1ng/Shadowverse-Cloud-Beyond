using System;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// Represents a unique identifier for a target that an ability can select.
    /// Can represent a Player or a specific CardInstance.
    /// </summary>
    public readonly struct TargetId : IEquatable<TargetId>
    {
        /// <summary>
        /// The type of the target (Player, CardInstance, etc.).
        /// </summary>
        public TargetType Type { get; } // <<< ADDED

        /// <summary>
        /// The specific Guid ID of the target.
        /// If Type is Player, this is the Player's Guid ID.
        /// If Type is CardInstance (or Leader, Unit, Amulet), this is the CardInstance.InstanceId.
        /// </summary>
        public Guid Id { get; }

        // Modified Constructor
        public TargetId(TargetType type, Guid id)
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
            return new TargetId(TargetType.Card, instanceId);
        }

        /// <summary>
        /// Creates a TargetId representing a specific player.
        /// </summary>
        /// <param name="playerId">The player's Guid ID.</param>
        public static TargetId Player(Guid playerId) // Takes Guid now
        {
            return new TargetId(TargetType.Player, playerId);
        }

        /// <summary>
        /// Represents an empty or undefined target.
        /// </summary>
        public static TargetId Undefined => new TargetId(TargetType.Undefined, Guid.Empty);

        /// <summary>
        /// Represents a system-level or non-specific source, distinct from an undefined/error state.
        /// </summary>
        public static TargetId None => new TargetId(TargetType.None, Guid.Empty);

        /// <summary>
        /// Checks if this represents a specifically defined target (neither Undefined nor None).
        /// </summary>
        public bool IsSpecified => Type != TargetType.Undefined && Type != TargetType.None;
    }
}
