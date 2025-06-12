using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a player's turn has ended and providing info about the next turn.
    /// </summary>
    public class TurnEndedEventDto : GameEventDto
    {
        // Constructor to initialize properties and call the base constructor (TurnNumber removed)
        public TurnEndedEventDto(Guid sourcePlayerId, Guid nextPlayerId)
            : base(sourcePlayerId, "TurnEnded") // sourcePlayerId is the player whose turn just ended
        {
            NextPlayerId = nextPlayerId;
            // TurnNumber = turnNumber; // Removed assignment
        }
        /// <summary>
        /// The identifier of the player whose turn is now starting.
        /// (e.g., "Player1", "Player2", or a connection ID/user ID)
        /// </summary>
        public Guid NextPlayerId { get; set; } = Guid.Empty;

        // TurnNumber property removed as per refactoring requirement

        // Consider adding initial state for the new turn, like PP/EP, draw details?
        // Or handle those via separate PpUpdate/EpUpdate/HandUpdate events triggered by StartTurn.
        // Let's keep it simple for now based on the M7 doc list.
    }
}