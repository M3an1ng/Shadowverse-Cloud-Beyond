// File: Server/Dtos/SignalR/Events/TurnStartedEventDto.cs
using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a new turn has started.
    /// Sent at the beginning of a player's turn after start-of-turn effects resolve.
    /// </summary>
    public class TurnStartedEventDto : GameEventDto
    {
        /// <summary>
        /// The ID of the player whose turn is starting (now the current player).
        /// </summary>
        public string CurrentPlayerId { get; set; } // Renamed from StartingPlayerId

        /// <summary>
        /// The current game phase (e.g., "Main").
        /// </summary>
        public string Phase { get; set; } // Added Phase property

        /// <summary>
        /// The turn number that is starting for the current player.
        /// </summary>
        public int TurnNumber { get; set; } // Removed required modifier

        // Constructor updated to accept and set new properties
        public TurnStartedEventDto(string currentPlayerId, int turnNumber, string phase)
            : base(currentPlayerId, "TurnStarted") // Call base with currentPlayerId
        {
            CurrentPlayerId = currentPlayerId;
            TurnNumber = turnNumber;
            Phase = phase; // Set the Phase property
        }
    }
}