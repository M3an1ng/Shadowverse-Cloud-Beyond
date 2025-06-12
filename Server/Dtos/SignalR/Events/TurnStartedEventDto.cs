using ShadowverseBeyond.Server.Dtos.SignalR; // Correct namespace for GameEventDto
using System; // Added for ArgumentNullException
using System.Text.Json.Serialization;

namespace ShadowverseBeyond.Server.Dtos.SignalR.Events // Keep this namespace
{
    /// <summary>
    /// Event DTO carrying the full game state snapshot,
    /// specifically sent at the start of a player's turn.
    /// </summary>
    public class TurnStartedEventDto : GameEventDto // Renamed class
    {
        /// <summary>
        /// The complete game state at the time the event was generated.
        /// </summary>
        public GameStateDto GameState { get; set; } // Keep this property

        // Constructor now accepts sourcePlayerId and calls the base constructor correctly
        public TurnStartedEventDto(GameStateDto gameState, Guid sourcePlayerId)
            : base(sourcePlayerId, "TurnStarted") // Call base constructor with sourcePlayerId and type "TurnStarted"
        {
            // Base constructor handles setting Type and TimestampUtc
            GameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            // No need to set Type here anymore
        }

        // Parameterless constructor removed as it's likely unnecessary and caused errors
    }
}