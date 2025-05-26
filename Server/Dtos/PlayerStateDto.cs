// File: Server/Dtos/PlayerStateDto.cs
using System;
using System.Collections.Generic;
using Shadowverse.Domain.Interfaces; // Add using for the interface

namespace ShadowverseBeyond.Server.Dtos // Assuming a base DTO namespace
{
    /// <summary>
    /// Data Transfer Object representing the state of a single player in the game.
    /// </summary>
    public record PlayerStateDto : IBoardEntityState // Implement the interface
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
        public int LeaderHealth { get; set; }
        public int MaxLeaderHealth { get; init; } // Usually constant, but init for flexibility

        // Card Zones
        public int DeckCount { get; set; } // Only send count, not the full deck content
        public List<CardDto> Hand { get; set; } = new(); // Full card info needed for the player's own hand
        public List<CardDto> Deck { get; set; } = new(); // Stores the actual cards in the player's deck. Needed for drawing.
        public List<UnitStateDto> Field { get; set; } = new(); // Units on the player's side of the field

        // Turn State
        public int PlayedCardCountThisTurn { get; set; }
        public bool HasCompletedMulligan { get; set; } = false; // Flag indicating if the player has submitted their mulligan choice.
        public bool HasTakenFirstTurn { get; set; } = false; // Indicates if the player has finished their first turn (for draw 2 logic)

        // Add Graveyard count, Banished count, etc., if needed by UI
        // public int GraveyardCount { get; set; }
        // Explicit implementation for IBoardEntityState.Uid
        string IBoardEntityState.Uid => this.PlayerId;

        // Explicit implementation for IBoardEntityState.Health
        int IBoardEntityState.Health
        {
            get => this.LeaderHealth;
            set => this.LeaderHealth = value;
        }
    }
}