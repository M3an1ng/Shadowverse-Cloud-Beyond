// File: Server/Services/IGameService.cs
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Domain.Entities; // Added for GameState domain entity
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Dtos; // For GameStateDto etc.
using ShadowverseBeyond.Server.Dtos.SignalR.Actions; // <<< ADDED for Action DTOs
using ShadowverseBeyond.Server.Dtos.SignalR; // <<< CORRECTED for Event DTOs (Base class is here)
using ShadowverseBeyond.Server.Models; // <<< ADDED for GameActionContext
using System; // For Guid
using System.Collections.Generic; // Needed for List

// Corrected Namespace
namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// Interface for the core game logic service.
    /// Handles game state management, action processing, and interactions with the SignalR Hub context.
    /// </summary>
    public interface IGameService
    {
        // --- Core Game State Access ---

        /// <summary>
        /// Gets the current game state for the given game ID, or creates a new one if it doesn't exist.
        /// Ensures a game state is available for processing actions or requests.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <param name="hostPlayerId">Host/seat A player ID (mandatory for creation).</param>
        /// <param name="guestPlayerId">Guest/seat B player ID (null allowed when using AI fallback).</param>
        /// <param name="useAiFallback">When true and guestPlayerId is null, create an AI opponent.</param>
        /// <param name="scopeServiceProvider">Scope service provider (from HttpContext) for dependency resolution.</param>
        /// <returns>The GameState domain entity for the game, or null if creation failed unexpectedly.</returns>
        Task<GameState?> GetOrCreateGameStateAsync(string gameId, Guid? hostPlayerId, Guid? guestPlayerId, bool useAiFallback, IServiceProvider scopeServiceProvider); // Return Domain Entity

        /// <summary>
        /// Retrieves the current game state for the specified game ID.
        /// Does not create a new game if the state does not exist.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <returns>The GameStateDto if found, otherwise null.</returns>
        GameState? GetGameState(string gameId); // Return Domain Entity

        // BroadcastEventsAsync removed - Broadcasting responsibility moved to Hub after context processing.

        // /// <summary>
        // /// Handles logic when a player disconnects from the SignalR Hub.
        // /// May involve updating game state (e.g., marking player as disconnected) or notifying opponent.
        // /// </summary>
        // /// <param name="gameId">The ID of the game instance.</param>
        // /// <param name="playerId">The ID of the disconnected player.</param>
        // /// <param name="connectionId">The specific SignalR connection ID that disconnected.</param>
        // Task HandlePlayerDisconnectAsync(string gameId, string playerId, string connectionId);
        /// <summary>
        /// Enqueues a player action received from the Hub for asynchronous processing.
        /// This is the new entry point for player actions, replacing direct calls to PlayCardAsync, AttackAsync, etc. from the Hub.
        /// The actual processing will happen in a dedicated game loop managed by the GameService implementation.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="action">The wrapper object containing action details.</param>
        /// <returns>A task representing the asynchronous operation of enqueuing.</returns>
        Task EnqueuePlayerActionAsync(string gameId, PlayerActionWrapper action);

        /// <summary>
        /// Handles the submission of a player's target choice, bypassing the main action queue.
        /// </summary>
        Task SubmitTargetChoiceAsync(string gameId, Guid playerId, SubmitTargetChoiceActionDto dto);

        /// <summary>
        /// 【BUG-019修复】广播事件列表给前端，确保AI和人类玩家使用相同的事件广播机制
        /// Broadcasts a list of events to all clients in the game group.
        /// </summary>
        /// <param name="gameId">The game ID.</param>
        /// <param name="events">The list of events to broadcast.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task BroadcastEventsAsync(string gameId, List<GameEventDto> events);

    }
}
