// File: Server/Domain/Entities/PlayerState.cs
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    // Note: CardState is now defined in CardState.cs
    // Placeholder record 'CardStatePlaceholder' is no longer needed here.
    // We will use the actual CardState class below.

    /// <summary>
    /// Represents the state of a single player within a game.
    /// </summary>
    public class PlayerState
    {
        public string PlayerId { get; set; } = string.Empty;

        // Leader / Core Stats
        public int LeaderHealth { get; set; } = 20; // Default starting health
        public int LeaderMaxHealth { get; set; } = 20;
        public int MaxPlayPoints { get; set; } = 0; // Starts at 0, increases per turn
        public int CurrentPlayPoints { get; set; } = 0;
        public int MaxEvolutionPoints { get; set; } = 0; // Determined by going first/second
        public int CurrentEvolutionPoints { get; set; } = 0;

        // Card Zones (Using actual CardState)
        public List<CardState> Hand { get; set; } = new List<CardState>();
        public List<CardState> Deck { get; set; } = new List<CardState>();
        public List<CardState> Graveyard { get; set; } = new List<CardState>();
        public List<CardState> BanishZone { get; set; } = new List<CardState>();
        public List<UnitState> Field { get; set; } = new List<UnitState>(); // Represents units on the field

        // Player Statuses/Effects (Consider a dedicated class/struct later)
        // public List<string> ActiveEffects { get; set; } = new List<string>();
        public bool IsVengeanceActive => LeaderHealth <= 10; // Example calculated property

        // Constructor
        public PlayerState(string playerId)
        {
            PlayerId = playerId;
            // Initialization logic (e.g., loading deck, setting initial PP/EP based on turn order)
            // would typically happen elsewhere, perhaps in GameState creation or IGameStateManager.
        }

        // Parameterless constructor often needed for ORMs or deserialization
        private PlayerState()
        {
            PlayerId = string.Empty; // Or handle nullability if preferred
            Hand = new List<CardState>();
            Deck = new List<CardState>();
            Graveyard = new List<CardState>();
            BanishZone = new List<CardState>();
        }

        // --- Potential methods for state manipulation ---

        // public void DrawCard() { /* Logic to move card from Deck to Hand */ }
        // public bool CanPlayCard(CardStatePlaceholder card, int currentTurn) { /* Check PP, conditions etc. */ }
        // public void PlayCard(CardStatePlaceholder card) { /* Remove from Hand, deduct PP */ }
        // public void TakeDamage(int amount) { LeaderHealth -= amount; /* Check for loss */ }
        // ... and so on
    }
}