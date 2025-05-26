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
        public string Uid { get; init; } = string.Empty; // Unique instance identifier (Changed from Guid to string)

        // Definition based (copied from CardDefinition or source)
        public string CardId { get; init; } = string.Empty; // Definition ID (e.g., "S001")
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty; // e.g., "Follower", "Spell", "Amulet"
        public int Cost { get; init; } // Current cost (considering modifications), mapped from CardState.CurrentCost
        public int? BaseAttack { get; init; } // Base value from definition
        public int? BaseHealth { get; init; } // Base value from definition
        public List<string> Keywords { get; init; } = new();
        public string Description { get; init; } = string.Empty;
        // Simplified effects for DTO? Or include full definition? Start simple.
        // public List<CardEffectDto> Effects { get; init; } = new();

        // Add any other properties needed by the client to render the card in hand
    }
}