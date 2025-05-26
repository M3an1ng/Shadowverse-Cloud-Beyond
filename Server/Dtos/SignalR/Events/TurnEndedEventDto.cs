using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that a player's turn has ended and providing info about the next turn.
    /// </summary>
    public class TurnEndedEventDto : GameEventDto
    {
        public override string Type => "TurnEnded";

        /// <summary>
        /// The identifier of the player whose turn is now starting.
        /// (e.g., "Player1", "Player2", or a connection ID/user ID)
        /// </summary>
        public string NextPlayerId { get; set; } = string.Empty;

        /// <summary>
        /// The new turn number that is beginning.
        /// </summary>
        public int TurnNumber { get; set; }

        // Consider adding initial state for the new turn, like PP/EP, draw details?
        // Or handle those via separate PpUpdate/EpUpdate/HandUpdate events triggered by StartTurn.
        // Let's keep it simple for now based on the M7 doc list.
    }
}