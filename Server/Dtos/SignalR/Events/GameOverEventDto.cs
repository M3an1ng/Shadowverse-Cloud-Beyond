using System;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events
{
    /// <summary>
    /// Event indicating that the game has ended.
    /// </summary>
    public class GameOverEventDto : GameEventDto
    {
        public override string Type => "GameOver";

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
    }
}