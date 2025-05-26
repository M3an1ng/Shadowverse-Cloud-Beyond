// File: Server/Services/IGameService.cs
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Dtos; // For GameStateDto etc.
using ShadowverseBeyond.Server.Dtos.SignalR.Actions; // <<< ADDED for Action DTOs
using ShadowverseBeyond.Server.Dtos.SignalR.Events; // <<< ADDED for Event DTOs
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
        /// <summary>
        /// Processes the "Play Card" action initiated by a player via the SignalR Hub.
        /// Handles PP cost deduction, target validation, card effect resolution,
        /// and returns a list of resulting game events to be broadcast by the Hub.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="playerId">The ID of the player performing the action.</param>
        /// <param name="actionDto">The DTO containing the details of the card play action.</param>
        /// <exception cref="GameLogicException">Thrown if the action is invalid according to game rules.</exception>
        Task<List<GameEventDto>> PlayCardAsync(string gameId, string playerId, PlayCardActionDto actionDto);

        /// <summary>
        /// Processes the "Attack" action initiated by a player.
        /// Handles target validation, damage calculation, effects triggering on attack/defense/damage,
        /// unit destruction checks, and returns resulting game events.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="playerId">The ID of the player performing the action.</param>
        /// <param name="actionDto">The DTO containing the details of the attack action.</param>
        /// <exception cref="GameLogicException">Thrown if the action is invalid according to game rules.</exception>
        Task<List<GameEventDto>> AttackAsync(string gameId, string playerId, AttackActionDto actionDto);

        /// <summary>
        /// Processes the "Evolve Unit" action initiated by a player.
        /// Handles EP cost deduction, evolution eligibility checks, stat changes,
        /// applying evolution effects, and returns resulting game events.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="playerId">The ID of the player performing the action.</param>
        /// <param name="actionDto">The DTO containing the details of the evolve action.</param>
        /// <exception cref="GameLogicException">Thrown if the action is invalid according to game rules.</exception>











        Task<List<GameEventDto>> EvolveAsync(string gameId, string playerId, EvolveActionDto actionDto);

        /// <summary>
        /// Processes the "End Turn" action initiated by a player via the SignalR Hub.
        /// Handles end-of-turn effects, switches the active player, handles start-of-turn effects
        /// for the new player (e.g., PP increase, card draw), and returns resulting game events.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="playerId">The ID of the player ending their turn.</param>
        /// <exception cref="GameLogicException">Thrown if the action is invalid (e.g., not the player's turn).</exception>
        Task<List<GameEventDto>> EndTurnAsync(string gameId, string playerId);

        /// <summary>
        /// Processes the "Submit Mulligan" action initiated by a player via the SignalR Hub.
        /// Handles card replacement, tracks mulligan completion for both players,
        /// transitions the game phase, and returns resulting game events/state updates.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="playerId">The ID of the player submitting their mulligan decision.</param>
        /// <param name="cardUidsToReplace">A list of card UIDs the player wishes to replace.</param>
        /// <exception cref="GameLogicException">Thrown if the action is invalid (e.g., not Mulligan phase, player already submitted).</exception>
        Task SubmitMulliganAsync(string gameId, string playerId, List<string> cardUidsToReplace);

        // --- Optional / Future Methods ---

        /// <summary>
        /// Gets the current game state for the given game ID, or creates a new one if it doesn't exist.
        /// Ensures a game state is available for processing actions or requests.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <returns>The GameStateDto for the game, or null if creation failed unexpectedly.</returns>
        Task<GameStateDto?> GetOrCreateGameStateAsync(string gameId);

        // /// <summary>
        // /// Handles logic when a player disconnects from the SignalR Hub.
        // /// May involve updating game state (e.g., marking player as disconnected) or notifying opponent.
        // /// </summary>
        // /// <param name="gameId">The ID of the game instance.</param>
        // /// <param name="playerId">The ID of the disconnected player.</param>
        // /// <param name="connectionId">The specific SignalR connection ID that disconnected.</param>
        // Task HandlePlayerDisconnectAsync(string gameId, string playerId, string connectionId);
    }
}