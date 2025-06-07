// File: Server/Domain/Entities/PlayerState.cs
using System.Collections.Generic;
using ShadowverseBeyond.Server.Dtos; // Added for DTO types

namespace ShadowverseBeyond.Server.Domain.Entities
{
    // Note: CardInstance is now defined in CardInstance.cs
    // This class has been updated to use CardInstance instead of CardState
    // Leader health management has been moved to LeaderState in GameState

    /// <summary>
    /// Represents the state of a single player within a game.
    /// Leader health is now managed through the associated LeaderState in GameState.
    /// </summary>
    public class PlayerState
    {
        public string PlayerId { get; set; } = string.Empty;

        // Core Stats (Leader health removed - now managed via LeaderState)
        public int MaxPlayPoints { get; set; } = 0; // Starts at 0, increases per turn
        public int CurrentPlayPoints { get; set; } = 0;
        public int MaxEvolutionPoints { get; set; } = 0; // Determined by going first/second
        public int CurrentEvolutionPoints { get; set; } = 0;

        // Card Zones (Using CardInstance instead of CardState)
        public CardInstance Leader { get; set; } = null!; // Must be initialized externally (e.g., in GameService)
        public List<CardInstance> Hand { get; set; } = new List<CardInstance>();
        public List<CardInstance> Deck { get; set; } = new List<CardInstance>();
        public List<CardInstance> Field { get; set; } = new List<CardInstance>();
        public List<CardInstance> Graveyard { get; set; } = new List<CardInstance>();
        public List<CardInstance> Extra { get; set; } = new List<CardInstance>();

        // Note: Field units are now managed separately in GameState as Player1Units/Player2Units
        // This removes the redundant Field property from PlayerState

        // Player Statuses/Effects (Consider a dedicated class/struct later)
        // public List<string> ActiveEffects { get; set; } = new List<string>();

        // Note: IsVengeanceActive calculation moved to service layer since it depends on LeaderState

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
            Hand = new List<CardInstance>();
            Deck = new List<CardInstance>();
            Field = new List<CardInstance>();
            Graveyard = new List<CardInstance>();
            Extra = new List<CardInstance>();
        }

        // --- Potential methods for state manipulation ---

        // public void DrawCard() { /* Logic to move card from Deck to Hand */ }
        // public bool CanPlayCard(CardStatePlaceholder card, int currentTurn) { /* Check PP, conditions etc. */ }
        // public void PlayCard(CardStatePlaceholder card) { /* Remove from Hand, deduct PP */ }
        // public void TakeDamage(int amount) { LeaderHealth -= amount; /* Check for loss */ }
        // ... and so on
        /// <summary>
        /// Converts this PlayerState domain entity to its corresponding Data Transfer Object (DTO).
        /// </summary>
        /// <returns>A PlayerStateDto representing the current player state.</returns>
        public PlayerStateDto ToDto()
        {
            // Assume CardInstance has a ToDto() method
            var handDto = Hand.Select(card => card.ToDto()).ToList();
            var deckDto = Deck.Select(card => card.ToDto()).ToList(); // Consider if full deck DTO is needed
            var fieldDto = Field.Select(card => card.ToDto()).ToList();
            var graveyardDto = Graveyard.Select(card => card.ToDto()).ToList();

            return new PlayerStateDto
            {
                PlayerId = this.PlayerId,
                MaxPP = this.MaxPlayPoints,
                CurrentPP = this.CurrentPlayPoints,
                MaxEP = this.MaxEvolutionPoints,
                CurrentEP = this.CurrentEvolutionPoints,
                DeckCount = this.Deck.Count,
                Hand = handDto,
                Deck = deckDto, // Sending full deck DTO for now
                Field = fieldDto,
                Graveyard = graveyardDto,

                // Properties not directly available in PlayerState entity:
                IsAI = false, // Default or determine elsewhere
                IsFirst = false, // Default or determine from GameState
                CanEvolve = false, // Default or determine based on game rules/turn
                LeaderCard = null, // Should be populated from GameState.PlayerXLeader.ToDto()
                PlayedCardCountThisTurn = 0, // Default or track elsewhere
                HasCompletedMulligan = false, // Default or track elsewhere
                HasTakenFirstTurn = false, // Default or track elsewhere
                TurnNumber = 0 // Default or track elsewhere (PlayerState doesn't track its own turn number)
            };
        }
    }
}