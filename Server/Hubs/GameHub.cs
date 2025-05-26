// File: Server/Hubs/GameHub.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces; // IGameClient
using ShadowverseBeyond.Server.Services;            // IGameService
using ShadowverseBeyond.Server.Dtos;            // GameStateDto etc.
using ShadowverseBeyond.Server.Dtos.SignalR.Actions; // Specific Action DTOs
using ShadowverseBeyond.Server.Dtos.SignalR.Events;  // Specific Event DTOs (incl. base GameEventDto)
using ShadowverseBeyond.Server.Exceptions; // GameLogicException
using System;
using System.Collections.Generic; // List<> for events
using System.Threading.Tasks;
using System.Collections.Generic; // List<> - Although not directly used here, might be needed for other methods

// Corrected Namespace
namespace ShadowverseBeyond.Server.Hubs
{
    /// <summary>
    /// SignalR Hub for handling real-time game interactions.
    /// Uses strong typing with IGameClient for client-side methods.
    /// </summary>
    public class GameHub : Hub<IGameClient>
    {
        private readonly IGameService _gameService;
        private readonly ILogger<GameHub> _logger;

        /// <summary>
        /// Initializes a new instance of the GameHub class.
        /// </summary>
        /// <param name="gameService">The core game logic service.</param>
        /// <param name="logger">The logger instance.</param>
        public GameHub(IGameService gameService, ILogger<GameHub> logger)
        {
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // --- Core Game Actions (Client -> Server) ---

        /// <summary>
        /// Client invokes this method to perform the "Play Card" action.
        /// Delegates the core logic to IGameService and handles exceptions.
        /// </summary>
        /// <param name="actionDto">DTO containing card play details.</param>
        public async Task PerformPlayCard(PlayCardActionDto actionDto)
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("PerformPlayCard called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                // Optionally send an error back to the caller even if unidentified
                // await Clients.Caller.ReceiveError("Unauthorized: Could not identify player or game.");
                return;
            }

            try
            {
                // Corrected log property CardUidInHand -> CardUid in the line below
                _logger.LogInformation("Player {PlayerId} in Game {GameId} attempting to play card {CardUid}",
                    playerId, gameId, actionDto.CardUid);
                // Removed extraneous braces {} that previously wrapped lines 63-80

                // Delegate to core game logic service and get resulting events
                var events = await _gameService.PlayCardAsync(gameId.ToString(), playerId, actionDto);

                // Log message below already correctly used CardUid
                _logger.LogInformation("Player {PlayerId} successfully executed PlayCard {CardUid} in Game {GameId}. Broadcasting {EventCount} events.",
                    playerId, gameId, actionDto.CardUid, events.Count);

                // Broadcast events returned by the service
                if (events.Count > 0)
                {
                    string groupName = $"Game_{gameId}";
                    foreach (var gameEvent in events)
                    {
                        // Use the generic ReceiveGameEvent method as identified in IGameClient
                        await Clients.Group(groupName).ReceiveGameEvent(gameEvent);
                    }
                    _logger.LogDebug("Broadcasted {EventCount} events to group {GroupName} after PlayCard.", events.Count, groupName);
                }
            } // End of try block
            // Catch block now correctly follows the main try logic
            catch (GameLogicException ex)
            {
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} playing card {CardUid}: {Message}",
                    playerId, gameId, actionDto.CardUid, ex.Message); // Corrected property name based on DTO
                await Clients.Caller.ReceiveError($"Play Card Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing PlayCard for Player {PlayerId} in Game {GameId}, Card {CardUid}",
                    playerId, gameId, actionDto.CardUid); // Corrected property name based on DTO
                await Clients.Caller.ReceiveError("An unexpected server error occurred while playing the card.");
            }
        }

        /// <summary>
        /// Client invokes this method to end their current turn.
        /// Delegates the core logic to IGameService and handles exceptions.
        /// </summary>
        public async Task EndTurn()
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("EndTurn called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                // await Clients.Caller.ReceiveError("Unauthorized: Could not identify player or game.");
                return;
            }

            try
            {
                _logger.LogInformation("Player {PlayerId} ending turn in Game {GameId}", playerId, gameId);
                // Delegate to core game logic service and get resulting events
                var events = await _gameService.EndTurnAsync(gameId.ToString(), playerId);

                _logger.LogInformation("Player {PlayerId} successfully executed EndTurn in Game {GameId}. Broadcasting {EventCount} events.",
                                       playerId, gameId, events.Count);

                // Broadcast events returned by the service
                if (events.Count > 0)
                {
                    string groupName = $"Game_{gameId}";
                    foreach (var gameEvent in events)
                    {
                        // Use the generic ReceiveGameEvent method as identified in IGameClient
                        await Clients.Group(groupName).ReceiveGameEvent(gameEvent);
                    }
                    _logger.LogDebug("Broadcasted {EventCount} events to group {GroupName} after EndTurn.", events.Count, groupName);
                }
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} ending turn: {Message}", playerId, gameId, ex.Message);
                await Clients.Caller.ReceiveError($"End Turn Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing EndTurn for Player {PlayerId} in Game {GameId}", playerId, gameId);
                await Clients.Caller.ReceiveError("An unexpected server error occurred while ending the turn.");
            }
        }

        /// <summary>
        /// Client invokes this method to declare an attack.
        /// Delegates the core logic to IGameService, gets resulting events, and broadcasts them.
        /// </summary>
        /// <param name="actionDto">DTO containing attack details.</param>
        public async Task PerformAttack(AttackActionDto actionDto)
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("PerformAttack called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                // Consider sending error back to caller?
                return;
            }

            try
            {
                _logger.LogInformation("Player {PlayerId} in Game {GameId} attempting attack: {Attacker} -> {Target}",
                    playerId, gameId, actionDto.AttackerUid, actionDto.TargetUid);

                // Delegate to core game logic service and get resulting events
                var events = await _gameService.AttackAsync(gameId.ToString(), playerId, actionDto);

                _logger.LogInformation("Player {PlayerId} successfully executed Attack in Game {GameId}. Broadcasting {EventCount} events.",
                                      playerId, gameId, events.Count);

                // Broadcast events returned by the service
                if (events.Count > 0)
                {
                    string groupName = $"Game_{gameId}";
                    foreach (var gameEvent in events)
                    {
                        // Use the generic ReceiveGameEvent method as identified in IGameClient
                        await Clients.Group(groupName).ReceiveGameEvent(gameEvent);
                    }
                    _logger.LogDebug("Broadcasted {EventCount} events to group {GroupName} after Attack.", events.Count, groupName);
                }
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} during attack ({Attacker} -> {Target}): {Message}",
                    playerId, gameId, actionDto.AttackerUid, actionDto.TargetUid, ex.Message);
                await Clients.Caller.ReceiveError($"Attack Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing Attack for Player {PlayerId} in Game {GameId} ({Attacker} -> {Target})",
                    playerId, gameId, actionDto.AttackerUid, actionDto.TargetUid);
                await Clients.Caller.ReceiveError("An unexpected server error occurred during the attack.");
            }
        }

        /// <summary>
        /// Client invokes this method to evolve a unit.
        /// Delegates the core logic to IGameService, gets resulting events, and broadcasts them.
        /// </summary>
        /// <param name="actionDto">DTO containing evolution details.</param>
        public async Task PerformEvolve(EvolveActionDto actionDto)
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("PerformEvolve called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                // Consider sending error back to caller?
                return;
            }

            try
            {
                _logger.LogInformation("Player {PlayerId} in Game {GameId} attempting to evolve unit {UnitUid}",
                    playerId, gameId, actionDto.UnitUid);

                // Delegate to core game logic service and get resulting events
                var events = await _gameService.EvolveAsync(gameId.ToString(), playerId, actionDto);

                _logger.LogInformation("Player {PlayerId} successfully executed Evolve on {UnitUid} in Game {GameId}. Broadcasting {EventCount} events.",
                                       playerId, gameId, actionDto.UnitUid, events.Count);

                // Broadcast events returned by the service
                if (events.Count > 0)
                {
                    string groupName = $"Game_{gameId}";
                    foreach (var gameEvent in events)
                    {
                        // Use the generic ReceiveGameEvent method as identified in IGameClient
                        await Clients.Group(groupName).ReceiveGameEvent(gameEvent);
                    }
                    _logger.LogDebug("Broadcasted {EventCount} events to group {GroupName} after Evolve.", events.Count, groupName);
                }
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} evolving unit {UnitUid}: {Message}",
                    playerId, gameId, actionDto.UnitUid, ex.Message);
                await Clients.Caller.ReceiveError($"Evolve Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing Evolve for Player {PlayerId} in Game {GameId}, Unit {UnitUid}",
                   playerId, gameId, actionDto.UnitUid);
                await Clients.Caller.ReceiveError("An unexpected server error occurred during evolution.");
            }
        }


        /// <summary>
        /// Client invokes this method to submit their mulligan decision.
        /// Delegates the core logic to IGameService and handles exceptions.
        /// </summary>
        /// <param name="cardUidsToReplace">A list of card UIDs the player wants to replace.</param>
        public async Task SubmitMulligan(List<string> cardUidsToReplace)
        {
            _logger.LogWarning("<<<<< SubmitMulligan method ENTERED for Connection {ConnectionId} - Attempting to get Player/Game info >>>>>", Context.ConnectionId); // Add entry log
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("SubmitMulligan called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                // await Clients.Caller.ReceiveError("Unauthorized: Could not identify player or game.");
                return;
            }

            // Validate input - although empty list is valid (means keeping all cards)
            if (cardUidsToReplace == null)
            {
                _logger.LogWarning("SubmitMulligan called with null list by Player {PlayerId} in Game {GameId}", playerId, gameId);
                await Clients.Caller.ReceiveError("Invalid mulligan submission: data was null.");
                return;
            }

            try
            {
                _logger.LogInformation("Player {PlayerId} submitting mulligan in Game {GameId}. Replacing {Count} cards.",
                    playerId, gameId, cardUidsToReplace.Count);

                // Delegate to core game logic service
                // Note: Ensure IGameService has a corresponding SubmitMulliganAsync method
                _logger.LogDebug("Attempting to call _gameService.SubmitMulliganAsync for Game {GameId}, Player {PlayerId}", gameId, playerId);
                await _gameService.SubmitMulliganAsync(gameId.ToString(), playerId, cardUidsToReplace);

                // GameService is responsible for broadcasting events/state updates after mulligan completion.
                _logger.LogInformation("Player {PlayerId} successfully submitted mulligan in Game {GameId}", playerId, gameId);

            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} submitting mulligan: {Message}",
                    playerId, gameId, ex.Message);
                await Clients.Caller.ReceiveError($"Mulligan Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing SubmitMulligan for Player {PlayerId} in Game {GameId}",
                    playerId, gameId);
                _logger.LogError("Full Exception Details for SubmitMulligan failure: {ExceptionString}", ex.ToString()); // Log full details
                await Clients.Caller.ReceiveError("An unexpected server error occurred during mulligan.");
            }
        }

        // --- Connection Management ---

        /// <summary>
        /// Called when a new client connects to the Hub.
        /// Identifies the game and player, adds the connection to the game's SignalR group.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync(); // Use helper

            if (gameId != Guid.Empty && !string.IsNullOrEmpty(playerId))
            {
                string groupName = $"Game_{gameId}"; // Consistent group naming convention
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

                _logger.LogInformation("Client connected: ConnectionId={ConnectionId}, PlayerId={PlayerId}, GameId={GameId}, Added to Group={GroupName}",
                    Context.ConnectionId, playerId, gameId, groupName);

                // Optional: Request initial state from GameService and send to caller
                try
                {
                    GameStateDto? currentState = await _gameService.GetOrCreateGameStateAsync(gameId.ToString()); // Use the new method, remove playerId arg
                    if (currentState != null)
                    {
                        _logger.LogInformation("Sending initial game state to Player {PlayerId} in Game {GameId}", playerId, gameId);
                        await Clients.Caller.ReceiveGameState(currentState);
                    }
                    else
                    {
                        _logger.LogWarning("Could not retrieve initial game state for Player {PlayerId} in Game {GameId}. Service returned null.", playerId, gameId);
                        await Clients.Caller.ReceiveError("Failed to load initial game state.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving or sending initial game state for Player {PlayerId} in Game {GameId}", playerId, gameId);
                    await Clients.Caller.ReceiveError("Server error loading initial game state.");
                }
            }
            else
            {
                _logger.LogWarning("Client connected with invalid parameters: ConnectionId={ConnectionId}. Could not identify game/player. Aborting connection.",
                    Context.ConnectionId);
                Context.Abort(); // Abort connection if essential info is missing
            }

            await base.OnConnectedAsync();
        }


        /// <summary>
        /// Called when a client disconnects from the Hub.
        /// Removes the connection from the game's SignalR group and logs the event.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Attempt to identify the game/player again for logging/cleanup purposes
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync(); // Use helper

            if (exception != null)
            {
                _logger.LogWarning(exception, "Client disconnected abnormally: ConnectionId={ConnectionId}, PlayerId={PlayerId}, GameId={GameId}",
                    Context.ConnectionId, playerId ?? "N/A", gameId != Guid.Empty ? gameId.ToString() : "N/A");
            }
            else
            {
                _logger.LogInformation("Client disconnected gracefully: ConnectionId={ConnectionId}, PlayerId={PlayerId}, GameId={GameId}",
                    Context.ConnectionId, playerId ?? "N/A", gameId != Guid.Empty ? gameId.ToString() : "N/A");
            }

            // Remove from group (SignalR might handle this, but explicit is safer)
            if (gameId != Guid.Empty)
            {
                string groupName = $"Game_{gameId}";
                try
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                    _logger.LogInformation("Removed ConnectionId {ConnectionId} from Group {GroupName}", Context.ConnectionId, groupName);

                    // Optional: Notify GameService about the disconnection
                    // if (!string.IsNullOrEmpty(playerId)) {
                    //     await _gameService.HandlePlayerDisconnectAsync(gameId.ToString(), playerId, Context.ConnectionId);
                    // }
                }
                catch (Exception ex)
                {
                    // Log error if removal fails, but don't block disconnection
                    _logger.LogError(ex, "Error removing ConnectionId {ConnectionId} from Group {GroupName}", Context.ConnectionId, groupName);
                }
            }
            else
            {
                _logger.LogWarning("Could not determine group for disconnected client: ConnectionId={ConnectionId}", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // --- Helper Method ---

        /// <summary>
        /// Extracts GameId and PlayerId from the current Hub connection context.
        /// Placeholder implementation relying on QueryString. Replace with robust auth/mapping later.
        /// </summary>
        /// <returns>A tuple containing GameId (Guid) and PlayerId (string?). Returns (Guid.Empty, null) if info not found.</returns>
        private Task<(Guid GameId, string? PlayerId)> GetGamePlayerInfoFromContextAsync()
        {
            // Prioritize authenticated user if available
            // var authenticatedPlayerId = Context.UserIdentifier; // Returns null if not authenticated
            // if (!string.IsNullOrEmpty(authenticatedPlayerId)) {
            //     // Need a way to get GameId associated with this player/connection
            //     // This might involve a ConnectionMappingService or similar lookup
            //     _logger.LogInformation("Identified player via authentication: {PlayerId}", authenticatedPlayerId);
            //     // Placeholder: return GetGameIdForPlayer(authenticatedPlayerId);
            // }

            // Fallback to QueryString (Less secure, good for early dev/testing)
            var httpContext = Context.GetHttpContext();
            if (httpContext == null)
            {
                _logger.LogWarning("HttpContext is null for ConnectionId {ConnectionId}. Cannot extract query parameters.", Context.ConnectionId);
                return Task.FromResult((Guid.Empty, (string?)null));
            }

            var gameIdStr = httpContext.Request.Query["gameId"].ToString();
            var playerIdFromQuery = httpContext.Request.Query["playerId"].ToString();

            if (Guid.TryParse(gameIdStr, out Guid gameId) && !string.IsNullOrEmpty(playerIdFromQuery))
            {
                // Consider validating if the PlayerId from query matches authenticated user if both exist
                _logger.LogDebug("Identified game/player via query string: GameId={GameId}, PlayerId={PlayerId}", gameId, playerIdFromQuery);
                return Task.FromResult<(Guid, string?)>((gameId, playerIdFromQuery)); // Explicitly match return type signature for nullability
            }

            _logger.LogWarning("Could not extract valid GameId/PlayerId from QueryString for ConnectionId {ConnectionId}. gameId='{GameIdStr}', playerId='{PlayerIdFromQuery}'",
                Context.ConnectionId, gameIdStr, playerIdFromQuery);
            return Task.FromResult((Guid.Empty, (string?)null));
        }
    }
}