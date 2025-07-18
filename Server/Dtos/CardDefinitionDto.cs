// File: Server/Dtos/CardDefinitionDto.cs
using System.Collections.Generic;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// Data Transfer Object representing the static definition of a card.
    /// Contains immutable properties like base stats, cost, text, type, etc.
    /// Used for card database lookups and displaying card information outside of active gameplay.
    /// </summary>
    public record CardDefinitionDto
    {
        // Changed CardId to int
        public required int CardId { get; init; }
        public required string Name { get; init; }
        public required CardType Type { get; init; } // "Follower", "Spell", "Amulet"
        public required string Rarity { get; init; } // Added Rarity
        public required string CardSet { get; init; } // Added CardSet
        // Removed Class, Rarity as they are not directly in CardDefinition entity
        public required int Cost { get; init; }
        public int? Attack { get; init; } // Base Attack (null for non-followers)
        public int? Health { get; init; } // Base Health (null for non-followers)
        public string? Description { get; init; }
        // Removed FlavorText
        // Kept CanEvolve (bool?), derived from entity's EvolveId
        public bool? CanEvolve { get; init; }
        public List<string> Keywords { get; init; } = new List<string>();
        // Removed EffectDescriptions, ImageUrl
        // Consider adding properties for IsCollectible, IsEvolved, EvolveId if needed by client? For now, keep it simple.
    }
}