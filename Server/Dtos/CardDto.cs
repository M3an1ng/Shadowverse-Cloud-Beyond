// File: Server/Dtos/CardDto.cs
using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos // Assuming a base DTO namespace
{
    /// <summary>
    /// Data Transfer Object representing a card instance (in hand, deck, etc.).
    /// Contains both definition info and instance-specific info like Uid.
    /// </summary>
    public record CardDto
    {
        // Instance specific
        public Guid Uid { get; set; } // Unique instance identifier (Reverted back to Guid)

        // Definition based (copied from CardDefinition or source)
        public string CardId { get; set; } = string.Empty; // Definition ID (e.g., "S001")
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // e.g., "Follower", "Spell", "Amulet"
        public int Cost { get; set; } // Current cost (considering modifications), mapped from CardState.CurrentCost
        public int? BaseAttack { get; set; } // Base value from definition
        public int? BaseHealth { get; set; } // Base value from definition
        // Add CurrentAttack and CurrentHealth to represent dynamic stats, even in hand/deck
        public int? CurrentAttack { get; set; } // Initially equals BaseAttack, can be modified by effects
        public int? CurrentHealth { get; set; } // Initially equals BaseHealth, can be modified by effects
        public List<string> Keywords { get; set; } = new();
        public string Description { get; set; } = string.Empty;
        // Simplified effects for DTO? Or include full definition? Start simple.
        // public List<CardEffectDto> Effects { get; init; } = new();

        // Add any other properties needed by the client to render the card in hand
    }
}