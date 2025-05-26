// File: ShadowverseBeyond.Server.Hubs.ClientInterfaces.IGameClient.cs
using ShadowverseBeyond.Server.Dtos; // For GameStateDto etc.
using ShadowverseBeyond.Server.Dtos.SignalR.Events; // <<< USE EVENTS NAMESPACE for GameEventDto and specific events
using System.Threading.Tasks;

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
        /// The client should inspect the Type property of the DTO to determine how to process it.
        /// </summary>
        /// <param name="gameEvent">The event data (base class from .Events namespace).</param>
        Task ReceiveGameEvent(GameEventDto gameEvent); // Should now resolve to Events.GameEventDto due to using statement

        /// <summary>
        /// Sends an error message related to game logic or player action to the client.
        /// </summary>
        /// <param name="errorMessage">The error message content.</param>
        Task ReceiveError(string errorMessage);
        /// <summary>
        /// Notifies the client that a unit (follower) has been played onto the board.
        /// </summary>
        Task UnitPlayed(UnitPlayedEventDto eventDto);

        /// <summary>
        /// Notifies the client of an update to a player's Play Points (PP).
        /// </summary>
        Task PpUpdate(PpUpdateEventDto eventDto);

        // --- Methods requiring DTOs not yet created (Commented out for now) ---
        Task ReceiveGameState(GameStateDto gameState); // Sends the full initial or updated game state
        // Task RequestTargetSelection(TargetSelectionRequestDto request); // Keep commented for now
    }
}