// File: Server/Services/IGameService.cs
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
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
        /// <summary>
        /// Processes the "Play Card" action initiated by a player via the SignalR Hub.
        /// Handles PP cost deduction, target validation, card effect resolution,
        /// and returns a list of resulting game events to be broadcast by the Hub.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="playerId">The ID of the player performing the action.</param>
        /// <param name="actionDto">The DTO containing the details of the card play action.</param>
        /// <exception cref="GameLogicException">Thrown if the action is invalid according to game rules.</exception>
        Task<bool> PlayCardAsync(string gameId, string playerId, PlayCardActionDto actionDto, GameActionContext context);

        /// <summary>
        /// Processes the "Attack" action initiated by a player.
        /// Handles target validation, damage calculation, effects triggering on attack/defense/damage,
        /// unit destruction checks, and returns resulting game events.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="playerId">The ID of the player performing the action.</param>
        /// <param name="actionDto">The DTO containing the details of the attack action.</param>
        /// <exception cref="GameLogicException">Thrown if the action is invalid according to game rules.</exception>
        Task<bool> AttackAsync(string gameId, string playerId, AttackActionDto actionDto, GameActionContext context);

        /// <summary>
        /// Processes the "Evolve Unit" action initiated by a player.
        /// Handles EP cost deduction, evolution eligibility checks, stat changes,
        /// applying evolution effects, and returns resulting game events.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="playerId">The ID of the player performing the action.</param>
        /// <param name="actionDto">The DTO containing the details of the evolve action.</param>
        /// <exception cref="GameLogicException">Thrown if the action is invalid according to game rules.</exception>











        Task<bool> EvolveAsync(string gameId, string playerId, EvolveActionDto actionDto, GameActionContext context);

        /// <summary>
        /// Processes the "End Turn" action initiated by a player.
        /// Handles end-of-turn effects for the current player and determines the next player.
        /// Does NOT handle start-of-turn effects for the next player or return events (See V2 Plan in M7).
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="endingPlayerId">The ID of the player ending their turn.</param>
        /// <returns>The ID of the next player whose turn should start.</returns>
        /// <exception cref="GameLogicException">Thrown if the action is invalid (e.g., not the player's turn).</exception>
        // V2 Refactor (BUG-009 Fix): Signature changed to return tuple including IsNextPlayerAi and Events.
        // M8 Change: Return type changed to Task, parameter renamed
        Task EndTurnAsync(string gameId, string playerId, GameActionContext context); // Renamed endingPlayerId to playerId

        /// <summary>
        /// Processes the "Submit Mulligan" action initiated by a player via the SignalR Hub.
        /// Handles card replacement, tracks mulligan completion for both players,
        /// transitions the game phase, and returns resulting game events/state updates.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="playerId">The ID of the player submitting their mulligan decision.</param>
        /// <param name="cardUidsToReplace">A list of card UIDs the player wishes to replace.</param>
        /// <exception cref="GameLogicException">Thrown if the action is invalid (e.g., not Mulligan phase, player already submitted).</exception>
        Task SubmitMulliganAsync(string gameId, string playerId, List<Guid> cardUidsToReplace, GameActionContext context);

        /// <summary>
        /// Processes the start-of-turn logic for the specified player.
        /// This includes incrementing turn count, updating PP/EP, drawing cards,
        /// resetting unit states (e.g., attack readiness), and generating necessary start-of-turn events.
        /// This method is called by the component responsible for turn transition (e.g., GameHub, AiTurnExecutor)
        /// after the previous turn has fully ended (See V2 Plan in M7).
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="startingPlayerId">The ID of the player whose turn is starting.</param>
        /// <returns>A tuple containing the list of generated game events and a boolean flag indicating if the starting player is an AI.</returns>
        /// <exception cref="GameLogicException">Thrown if the game or player state is invalid.</exception>
        // M8 Change: Removed context parameter, returns GameStateUpdateEventDto
        Task<TurnStartedEventDto> StartPlayerTurnAsync(string gameId, string startingPlayerId);
        /// <summary>
        /// Explicitly triggers the execution of an AI player's turn.
        /// This is typically called by the Hub after StartPlayerTurnAsync indicates it's an AI's turn.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="aiPlayerId">The ID of the AI player whose turn should be executed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task TriggerAiTurnAsync(string gameId, string aiPlayerId);

        // --- Optional / Future Methods ---

        /// <summary>
        /// Gets the current game state for the given game ID, or creates a new one if it doesn't exist.
        /// Ensures a game state is available for processing actions or requests.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <returns>The GameStateDto for the game, or null if creation failed unexpectedly.</returns>
        Task<GameStateDto?> GetOrCreateGameStateAsync(string gameId);

        /// <summary>
        /// Retrieves the current game state for the specified game ID.
        /// Does not create a new game if the state does not exist.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <returns>The GameStateDto if found, otherwise null.</returns>
        Task<GameStateDto?> GetGameStateAsync(string gameId);

        // BroadcastEventsAsync removed - Broadcasting responsibility moved to Hub after context processing.

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