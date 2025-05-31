using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that the game has ended.
    /// </summary>
    public class GameOverEventDto : GameEventDto
    {
        // Type is now set in the base constructor

        /// <summary>
        /// The identifier of the player who won the game.
        /// </summary>
        public string WinnerId { get; set; } = string.Empty;

        /// <summary>
        /// The identifier of the player who lost the game.
        /// </summary>
        public string LoserId { get; set; } = string.Empty;

        /// <summary>
        /// Optional: Reason for the game ending (e.g., "Player Conceded", "Leader Health Reached Zero", "Deck Out").
        /// </summary>
        public string? Reason { get; set; }

        // Constructor to initialize properties and call base constructor
        public GameOverEventDto(string playerId, string winnerId, string loserId, string? reason = null)
            : base(playerId, "GameOver") // Set base PlayerId and Type
        {
            this.WinnerId = winnerId;
            this.LoserId = loserId;
            this.Reason = reason;
        }
    }
}