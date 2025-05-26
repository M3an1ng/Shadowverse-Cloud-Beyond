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

        // Represent leaders, units, and amulets separately
        public LeaderState Player1Leader { get; set; } = null!; // Must be initialized externally (e.g., in GameService)
        public LeaderState Player2Leader { get; set; } = null!; // Must be initialized externally

        public List<UnitState> Player1Units { get; set; } = new List<UnitState>(); // Replaces Player1Board
        public List<UnitState> Player2Units { get; set; } = new List<UnitState>(); // Replaces Player2Board

        public List<AmuletState> Player1Amulets { get; set; } = new List<AmuletState>();
        public List<AmuletState> Player2Amulets { get; set; } = new List<AmuletState>();
        // Constructor requiring GameId
        public GameState(string gameId)
        {
            GameId = gameId;
            // Initialize player states using the actual PlayerState constructor
            Player1 = new PlayerState($"{gameId}_Player1"); // Assuming PlayerState has a constructor taking PlayerId
            Player2 = new PlayerState($"{gameId}_Player2"); // Assuming PlayerState has a constructor taking PlayerId
            // Initialize lists
            Player1Units = new List<UnitState>();
            Player2Units = new List<UnitState>();
            Player1Amulets = new List<AmuletState>();
            Player2Amulets = new List<AmuletState>();
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
            // Initialize lists
            Player1Units = new List<UnitState>();
            Player2Units = new List<UnitState>();
            Player1Amulets = new List<AmuletState>();
            Player2Amulets = new List<AmuletState>();
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
        /// Finds a board entity (Leader, Unit, or Amulet) within the game state by its unique instance ID.
        /// Requires LeaderState and AmuletState to have an InstanceId property (potentially via inheriting BoardEntityState).
        /// </summary>
        /// <param name="uid">The unique instance ID of the entity to find.</param>
        /// <returns>The BoardEntityState if found; otherwise, null.</returns>
        public BoardEntityState? FindEntityByUid(string uid)
        {
            // Check Leaders (Assuming LeaderState : BoardEntityState)
            if (Player1Leader?.InstanceId == uid) return Player1Leader;
            if (Player2Leader?.InstanceId == uid) return Player2Leader;

            // Check Units (Assuming UnitState : BoardEntityState)
            BoardEntityState? foundEntity = Player1Units.FirstOrDefault(u => u.InstanceId == uid); // Corrected lambda operator
            if (foundEntity != null) return foundEntity;

            foundEntity = Player2Units.FirstOrDefault(u => u.InstanceId == uid); // Corrected lambda operator
            if (foundEntity != null) return foundEntity;

            // Check Amulets (Assuming AmuletState : BoardEntityState)
            foundEntity = Player1Amulets.FirstOrDefault(a => a.InstanceId == uid); // Corrected lambda operator
            if (foundEntity != null) return foundEntity;

            foundEntity = Player2Amulets.FirstOrDefault(a => a.InstanceId == uid); // Corrected lambda operator
            if (foundEntity != null) return foundEntity;

            // PlayerState itself usually doesn't inherit BoardEntityState and uses PlayerId, not InstanceId.
            // So, direct player targeting logic should likely compare against Player1/Player2 directly if needed.

            return null; // Not found
        }
    } // End of GameState class
} // End of namespace