using ShadowverseBeyond.Server.Domain.Entities;
// File: Server/Domain/Interfaces/IGameStateManager.cs
using System.Threading.Tasks;
// Removed duplicate using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Domain.Interfaces
{
    // Note: GameState class is now defined in Server/Domain/Entities/GameState.cs
    // Removed the GameStatePlaceholder record definition.

    /// <summary>
    /// Manages the persistence and retrieval of game state.
    /// Implementations could use in-memory stores, databases, caches, etc.
    /// </summary>
    public interface IGameStateManager
    {
        /// <summary>
        /// Retrieves the current state for a given game.
        /// </summary>
        /// <param name="gameId">The unique identifier of the game.</param>
        /// <returns>The current GameState, or null if not found.</returns>
        Task<GameState?> GetGameStateAsync(string gameId); // Using actual GameState type

        /// <summary>
        /// Saves the provided game state.
        /// This might involve creating or updating the state persistence.
        /// </summary>
        /// <param name="gameState">The game state object to save.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveGameStateAsync(GameState gameState); // Using actual GameState type

        // Consider adding other necessary methods later, e.g.,
        // Task CreateNewGameAsync(string gameId, PlayerState player1, PlayerState player2 /*, initial setup data */);
        // Task DeleteGameStateAsync(string gameId);
    }
}