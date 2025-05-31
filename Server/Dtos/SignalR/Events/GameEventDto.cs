// File: ShadowverseBeyond.Server.Dtos.SignalR.GameEventDto.cs
using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR
{
    /// <summary>
    /// Base class for all game events sent from server to client via SignalR.
    /// Contains common properties like event type and timestamp.
    /// </summary>
    public abstract class GameEventDto // Abstract base class
    {
        /// <summary>
        /// A string identifier for the type of event (e.g., "UnitPlayed", "TurnEnded").
        /// Used by the client to determine how to process the event data.
        /// </summary>
        public string Type { get; protected set; } // Set by derived classes

        /// <summary>
        /// The PlayerId of the player who initiated the action causing this event.
        /// </summary>
        public string SourcePlayerId { get; protected set; }

        /// <summary>
        /// Timestamp when the event occurred on the server (UTC).
        /// </summary>
        public DateTime TimestampUtc { get; protected set; }

        protected GameEventDto(string sourcePlayerId, string type) // Added sourcePlayerId parameter
        {
            SourcePlayerId = sourcePlayerId; // Set the new property
            Type = type;
            TimestampUtc = DateTime.UtcNow;
        }
    }
}