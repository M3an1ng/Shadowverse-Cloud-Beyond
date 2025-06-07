// File: Server/Domain/Entities/GameState.cs
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos; // Added for DTO types
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

        // Constructor requiring GameId
        public GameState(string gameId)
        {
            GameId = gameId;
            // Initialize player states using the actual PlayerState constructor
            Player1 = new PlayerState($"{gameId}_Player1"); // Assuming PlayerState has a constructor taking PlayerId
            Player2 = new PlayerState($"{gameId}_Player2"); // Assuming PlayerState has a constructor taking PlayerId
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
        /// Converts this GameState domain entity to its corresponding Data Transfer Object (DTO).
        /// </summary>
        /// <returns>A GameStateDto representing the current game state.</returns>
        public GameStateDto ToDto()
        {
            // Assume PlayerState has a ToDto() method
            var playerDtos = new List<PlayerStateDto>();
            if (Player1 != null)
            {
                playerDtos.Add(Player1.ToDto()); // Assumes PlayerState.ToDto() exists
            }
            if (Player2 != null)
            {
                playerDtos.Add(Player2.ToDto()); // Assumes PlayerState.ToDto() exists
            }

            // Use the correct GameStatus enum member
            bool isGameOver = Status == GameStatus.Finished;
            // Cannot determine winner solely from GameState entity currently.
            // Winner determination logic likely resides in GameService or needs WinnerId added to GameState.
            string? winnerId = null; // Set to null for now

            return new GameStateDto
            {
                GameId = this.GameId,
                CurrentPlayerId = this.CurrentTurnPlayerId,
                Phase = this.Phase,
                // FirstPlayerId = ??? // Cannot determine from current GameState properties, leave empty for now
                GameStatus = this.Status,
                Players = playerDtos,
                IsGameOver = isGameOver,
                WinnerId = winnerId,
                GameMessage = null // Or set based on Phase/Status if needed
            };
        }
    } // End of GameState class
} // End of namespace