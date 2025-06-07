// File: Server/Dtos/PlayerStateDto.cs
using System;
using System.Collections.Generic;
using Shadowverse.Domain.Interfaces; // Add using for the interface

namespace ShadowverseBeyond.Server.Dtos // Assuming a base DTO namespace
{
    /// <summary>
    /// Data Transfer Object representing the state of a single player in the game.
    /// </summary>
    public record PlayerStateDto // Remove IBoardEntityState implementation
    {
        public string PlayerId { get; init; } = string.Empty;
        public bool IsAI { get; init; } // Flag to indicate if the player is AI controlled
        public bool IsFirst { get; set; } // Indicates if this player goes first

        // Resources & Limits
        public int CurrentPP { get; set; }
        public int MaxPP { get; set; }
        public int CurrentEP { get; set; }
        public int MaxEP { get; set; }
        public bool CanEvolve { get; set; } // Whether the player can use an evolution point this turn

        // Leader Info
        // public int LeaderHealth { get; set; } // Removed: Use LeaderCard.CurrentHealth
        // public int MaxLeaderHealth { get; init; } // Removed: Use LeaderCard.MaxHealth
        public CardInstanceDto? LeaderCard { get; set; } // Represents the leader card instance (Nullable)

        // Card Zones
        public int DeckCount { get; set; } // Only send count, not the full deck content
        public List<CardInstanceDto> Hand { get; set; } = new(); // Represents card instances in the player's hand
        public List<CardInstanceDto> Deck { get; set; } = new(); // Represents card instances in the player's deck. Needed for drawing.
        public List<CardInstanceDto> Field { get; set; } = new(); // Represents card instances (followers, amulets) on the player's field
        public List<CardInstanceDto> Graveyard { get; set; } = new(); // Represents card instances in the graveyard
        public List<CardInstanceDto> Extra { get; set; } = new List<CardInstanceDto>();

        // Turn State
        public int PlayedCardCountThisTurn { get; set; }
        public bool HasCompletedMulligan { get; set; } = false; // Flag indicating if the player has submitted their mulligan choice.
        public bool HasTakenFirstTurn { get; set; } = false; // Indicates if the player has finished their first turn (for draw 2 logic)
        public int TurnNumber { get; set; } = 0; // Renamed: Tracks the player's individual turn number, starts at 0 before their first turn begins.

        // Add Graveyard count, Banished count, etc., if needed by UI
        // public int GraveyardCount { get; set; }
        // Removed IBoardEntityState implementation
    }
}