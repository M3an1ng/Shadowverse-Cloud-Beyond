// File: ShadowverseBeyond.Server.Hubs.ClientInterfaces.IGameClient.cs
using ShadowverseBeyond.Server.Dtos.SignalR; // Correct DTO namespace
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Dtos; // Add for GameStateDto etc.

namespace ShadowverseBeyond.Server.Hubs.ClientInterfaces
{
    /// <summary>
    /// Defines the methods that the server Hub can invoke on connected clients.
    /// Used for strong-typed Hubs (Hub<IGameClient>).
    /// </summary>
    public interface IGameClient
    {
        /// <summary>
        /// Sends a specific game event DTO to the client.
        /// The client should inspect the EventType of the DTO to determine how to process it.
        /// </summary>
        /// <param name="gameEvent">The event data.</param>
        Task ReceiveGameEvent(GameEventDto gameEvent);

        /// <summary>
        /// Sends an error message related to game logic or player action to the client.
        /// </summary>
        /// <param name="errorMessage">The error message content.</param>
        Task ReceiveError(string errorMessage);
/// &lt;summary&gt;
        /// Notifies the client that a unit (follower) has been played onto the board.
        /// &lt;/summary&gt;
        Task UnitPlayed(UnitPlayedEventDto eventDto);

        /// &lt;summary&gt;
        /// Notifies the client of an update to a player's Play Points (PP).
        /// &lt;/summary&gt;
        Task PpUpdate(PpUpdateEventDto eventDto);

        // --- Methods requiring DTOs not yet created (Commented out for now) ---
        Task ReceiveGameState(GameStateDto gameState); // Sends the full initial or updated game state
        // Task RequestTargetSelection(TargetSelectionRequestDto request); // Keep commented for now
    }
}