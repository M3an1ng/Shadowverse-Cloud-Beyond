// File: Server/Dtos/UnitStateDto.cs
using System;
using System.Collections.Generic;
using Shadowverse.Domain.Interfaces; // Add using for the interface

namespace ShadowverseBeyond.Server.Dtos // Assuming a base DTO namespace
{
    /// <summary>
    /// Data Transfer Object representing the state of a unit (Follower or Amulet) on the battlefield.
    /// </summary>
    public record UnitStateDto : IBoardEntityState // Implement the interface
    {
        // Instance specific
        public Guid Uid { get; init; } // Unique instance identifier
        public string CardId { get; init; } = string.Empty; // Definition ID
        public string Name { get; init; } = string.Empty; // Copied from definition for display
        public string Type { get; init; } = string.Empty; // "Follower" or "Amulet"
        public int Cost { get; init; } // Add Cost property, initialized from definition

        // State for Followers (null for Amulets?)
        public int? CurrentAttack { get; set; } // Current attack value including buffs/debuffs
        public int? MaxAttack { get; init; }    // Base attack + permanent buffs (can change?)
        public int? CurrentHealth { get; set; } // Current health value including damage
        public int? MaxHealth { get; set; }    // Base health + permanent buffs (can change?)
        public bool CanAttack { get; set; }     // Whether the unit can currently initiate an attack
        public bool IsEvolved { get; set; }     // Evolution status
        public int AttacksMadeThisTurn { get; set; } = 0; // Tracks attacks made this turn (Renamed, set setter for compatibility, default 0 as per M7)

        public int MaxAttacksPerTurn { get; init; } = 1; // Max attacks per turn (Default 1, as per M7)
        // Common State / Keywords
        public List<string> Keywords { get; set; } = new(); // Current active keywords (e.g., Ward)

        // Add other state properties as needed (e.g., status effects, last turn attacked)
        // Explicit implementation for IBoardEntityState
        string IBoardEntityState.Uid => this.Uid.ToString(); // Convert Guid to string

        int IBoardEntityState.Health
        {
            // Assuming this interface is primarily used for damageable entities (Followers),
            // where CurrentHealth should not be null. Throw if accessed unexpectedly.
            get => this.CurrentHealth ?? throw new InvalidOperationException("Cannot get Health for an entity with null CurrentHealth.");
            set => this.CurrentHealth = value;
        }
    }
}