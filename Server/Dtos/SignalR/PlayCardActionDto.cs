// File: ShadowverseBeyond.Server.Dtos.SignalR.PlayCardActionDto.cs
using System;
using System.Collections.Generic; // Required for List<>

namespace ShadowverseBeyond.Server.Dtos.SignalR
{
    /// <summary>
    /// DTO for the client action of playing a card.
    /// </summary>
    public class PlayCardActionDto
    {
        /// <summary>
        /// Unique identifier string of the card instance being played from the player's hand.
        /// (Changed from Guid to string based on GameService usage)
        /// </summary>
        public string? CardUidInHand { get; set; } // Changed type to string? (nullable)

        /// <summary>
        /// Optional unique ID for a single unit on the field targeted by the card's effect.
        /// Null if no single target or target is opponent leader. Use TargetUnitIds for multi-target.
        /// (Added property based on GameService usage)
        /// </summary>
        public Guid? TargetUniqueId { get; set; } // Added property

        /// <summary>
        /// Optional list of unique IDs for units on the field targeted by the card's effect.
        /// Null or empty if no targets or target is the opponent leader.
        /// </summary>
        public List<Guid>? TargetUnitIds { get; set; }

        /// <summary>
        /// Optional 0-based index on the player's side of the field where the unit will be played.
        /// Required for playing follower cards. Null otherwise.
        /// </summary>
        public int? FieldIndex { get; set; }
    }
}