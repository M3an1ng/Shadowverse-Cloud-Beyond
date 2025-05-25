// File: Server/Dtos/GameStateDto.cs
using System;
using System.Collections.Generic;

using ShadowverseBeyond.Server.Dtos.Enums;
namespace ShadowverseBeyond.Server.Dtos // Assuming a base DTO namespace
{
    /// <summary>
    /// Data Transfer Object representing the entire state of the game at a specific point in time.
    /// This is typically sent to clients upon connection or after major state changes.
    /// </summary>
    public record GameStateDto
    {
        public string GameId { get; init; } = string.Empty; // Identifier for the game session
        public string CurrentPlayerId { get; set; } = string.Empty; // Whose turn it is
        public int TurnNumber { get; set; } // Current turn number
        public GamePhase Phase { get; set; } = GamePhase.Initialization; // Changed type to GamePhase Enum and updated default

        public string FirstPlayerId { get; set; } = string.Empty; // ID of the player who went first
        public GameStatus GameStatus { get; set; } = GameStatus.Starting; // Current status of the game (e.g., Starting, InProgress, Finished)
        public List<PlayerStateDto> Players { get; set; } = new(); // State for both players

        public string? GameMessage { get; set; } // Optional message for the UI (e.g., "Player 1's Turn")
        public bool IsGameOver { get; set; } // Flag indicating game end
        public string? WinnerId { get; set; } // ID of the winner if game is over

        // Consider adding other global state if needed, e.g., pending actions, history log snippet
    }
}