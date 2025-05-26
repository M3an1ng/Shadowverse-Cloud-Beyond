// File: Server/Infrastructure/Persistence/InMemoryGameStateManager.cs
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Interfaces;

namespace ShadowverseBeyond.Server.Infrastructure.Persistence
{
    /// <summary>
    /// In-memory implementation of the game state manager using a concurrent dictionary.
    /// Suitable for development and scenarios where persistence across restarts is not required.
    /// </summary>
    public class InMemoryGameStateManager : IGameStateManager
    {
        private readonly ConcurrentDictionary<string, GameState> _gameStates = new();

        /// <summary>
        /// Retrieves the game state for the specified game ID.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <returns>The GameState object if found; otherwise, null.</returns>
        public Task<GameState?> GetGameStateAsync(string gameId)
        {
            _gameStates.TryGetValue(gameId, out var gameState);
            // Return Task.FromResult to wrap the result in a completed task, matching the async signature.
            return Task.FromResult(gameState);
        }

        /// <summary>
        /// Saves (adds or updates) the provided game state.
        /// </summary>
        /// <param name="gameState">The game state to save.</param>
        /// <returns>A task representing the completion of the save operation.</returns>
        public Task SaveGameStateAsync(GameState gameState)
        {
            if (gameState == null)
            {
                // Or handle appropriately, maybe log a warning. For now, ignore null save.
                return Task.CompletedTask;
                // throw new ArgumentNullException(nameof(gameState)); // Alternatively, throw if null is invalid
            }

            _gameStates.AddOrUpdate(gameState.GameId, gameState, (key, existingVal) => gameState);
            return Task.CompletedTask;
        }

        // Optional: Add methods for removing game states, listing active games, etc. if needed.
    }
}