// File: ShadowverseBeyond.Server.Dtos.SignalR.Events.GameEventDto.cs - MOVED & Namespace Updated
using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events // <<< UPDATED Namespace
{
    /// <summary>
    /// Base class for all game events sent from server to client via SignalR.
    /// Contains common properties like event type and timestamp.
    /// Should reside in the same namespace as specific event DTOs.
    /// </summary>
    public abstract class GameEventDto // Abstract base class
    {
        /// <summary>
        /// A string identifier for the type of event (e.g., "UnitPlayed", "TurnEnded").
        /// Used by the client to determine how to process the event data.
        /// Value should ideally be set via the derived class constructor or property override.
        /// </summary>
        public abstract string Type { get; } // Changed to abstract property

        // Consider removing SourcePlayerId if PlayerId exists in all/most derived events,
        // or keep it for events where the source might differ from the main subject player.
        // For now, keeping it as it was read. Revisit if needed.
        // /// <summary>
        // /// The PlayerId of the player who initiated the action causing this event.
        // /// </summary>
        // public string SourcePlayerId { get; protected set; }

        /// <summary>
        /// Timestamp when the event occurred on the server (UTC).
        /// </summary>
        public DateTime TimestampUtc { get; } // Changed to readonly property with init in ctor

        // Removed the base constructor requiring parameters.
        // Derived classes should set Type (e.g., via property override) and
        // populate their own specific fields. Timestamp is set here.
        protected GameEventDto()
        {
            TimestampUtc = DateTime.UtcNow;
        }

        // Example of how a derived class would set the Type:
        // public class TurnEndedEventDto : GameEventDto
        // {
        //     public override string Type => "TurnEnded";
        //     public string PlayerId { get; set; }
        //     // ... other properties ...
        // }
    }
}