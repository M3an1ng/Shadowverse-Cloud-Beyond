// File: Server/Domain/Entities/GameState.cs
using ShadowverseBeyond.Server.Dtos.Enums; // For GameStatus, GamePhase
using System.Collections.Generic;
using System.Linq; // Added for FirstOrDefault

namespace ShadowverseBeyond.Server.Domain.Entities
{
    // Note: PlayerState and UnitState are now defined in separate files within this namespace.
    // Removed the placeholder records for PlayerStatePlaceholder and UnitStatePlaceholder.


    /// <summary>
    /// Represents the complete state of a single game instance.
    /// This is the core domain entity used for game logic processing.
    /// </summary>
    public class GameState
    {
        public string GameId { get; set; } = string.Empty;
        public GameStatus Status { get; set; } = GameStatus.Starting; // Use Starting as initial status
        public GamePhase Phase { get; set; } = GamePhase.Mulligan;
        public int TurnNumber { get; set; } = 0;
        public string CurrentTurnPlayerId { get; set; } = string.Empty; // ID of the player whose turn it is

        // Use actual PlayerState class
        public PlayerState Player1 { get; set; } // Needs proper initialization
        public PlayerState Player2 { get; set; } // Needs proper initialization

        // Unified field representation using CardInstance
        public CardInstance Player1Leader { get; set; } = null!; // Must be initialized externally (e.g., in GameService)
        public CardInstance Player2Leader { get; set; } = null!; // Must be initialized externally

        public List<CardInstance> Player1Field { get; set; } = new List<CardInstance>(); // All field entities (units + amulets)
        public List<CardInstance> Player2Field { get; set; } = new List<CardInstance>(); // All field entities (units + amulets)
        // Constructor requiring GameId
        public GameState(string gameId)
        {
            GameId = gameId;
            // Initialize player states using the actual PlayerState constructor
            Player1 = new PlayerState($"{gameId}_Player1"); // Assuming PlayerState has a constructor taking PlayerId
            Player2 = new PlayerState($"{gameId}_Player2"); // Assuming PlayerState has a constructor taking PlayerId
            // Initialize field lists
            Player1Field = new List<CardInstance>();
            Player2Field = new List<CardInstance>();
            // Note: Leader initialization (Player1Leader, Player2Leader) should happen
            // in the service layer (e.g., GameService) after creating the GameState,
            // as it likely involves fetching leader definitions based on player selection.
        }

        // Parameterless constructor often needed for ORMs or deserialization
        private GameState()
        {
            // Ensure non-nullable properties are initialized if using parameterless constructor
            GameId = string.Empty;
            CurrentTurnPlayerId = string.Empty;
            // Use actual PlayerState constructor
            Player1 = new PlayerState("Default_P1"); // Needs parameterless constructor or null handling in PlayerState if this path is used
            Player2 = new PlayerState("Default_P2");
            // Initialize field lists
            Player1Field = new List<CardInstance>();
            Player2Field = new List<CardInstance>();
            // Player1Leader/Player2Leader remain null! here, expecting external initialization if this constructor path is used.
        }

        // Helper Methods Added

        /// <summary>
        /// Gets the PlayerState object for the specified player ID.
        /// </summary>
        /// <param name="playerId">The ID of the player.</param>
        /// <returns>The PlayerState if found; otherwise, null.</returns>
        public PlayerState? GetPlayerState(string playerId)
        {
            if (Player1?.PlayerId == playerId) return Player1;
            if (Player2?.PlayerId == playerId) return Player2;
            return null;
        }

        /// <summary>
        /// Gets the PlayerState object for the opponent of the specified player ID.
        /// </summary>
        /// <param name="playerId">The ID of the player whose opponent is requested.</param>
        /// <returns>The opponent's PlayerState if found; otherwise, null.</returns>
        public PlayerState? GetOpponentPlayerState(string playerId)
        {
            if (Player1?.PlayerId == playerId) return Player2;
            if (Player2?.PlayerId == playerId) return Player1;
            return null; // Or throw if playerId doesn't match either player
        }

        /// <summary>
        /// Finds a card instance (Leader or Field entity) within the game state by its unique instance ID.
        /// </summary>
        /// <param name="uid">The unique instance ID of the entity to find.</param>
        /// <returns>The CardInstance if found; otherwise, null.</returns>
        public CardInstance? FindEntityByUid(string uid)
        {
            // Check Leaders
            if (Player1Leader?.InstanceId == uid) return Player1Leader;
            if (Player2Leader?.InstanceId == uid) return Player2Leader;

            // Check Field entities (both units and amulets)
            CardInstance? foundEntity = Player1Field.FirstOrDefault(c => c.InstanceId == uid);
            if (foundEntity != null) return foundEntity;

            foundEntity = Player2Field.FirstOrDefault(c => c.InstanceId == uid);
            if (foundEntity != null) return foundEntity;

            return null; // Not found
        }
    } // End of GameState class
} // End of namespace