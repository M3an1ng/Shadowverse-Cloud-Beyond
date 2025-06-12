// File: Server/Hubs/GameHub.cs
//
// 相关文档:
// - API集成: 文档/03-前端架构/03-API集成.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 回合管理系统: 文档/05-游戏系统/03-回合管理系统.md
// - 战斗系统: 文档/05-游戏系统/02-战斗系统.md
// - 卡牌出牌系统: 文档/05-游戏系统/04-卡牌出牌系统.md
//
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Text.Json; // Added for JSON Serialization in logging
using ShadowverseBeyond.Server.Models; // Add using for GameActionContext
using ShadowverseBeyond.Server.Dtos.SignalR; // Added for base GameEventDto
using ShadowverseBeyond.Server.Hubs.ClientInterfaces; // IGameClient
using ShadowverseBeyond.Server.Services;            // IGameService
using ShadowverseBeyond.Server.Dtos;            // GameStateDto etc.
using ShadowverseBeyond.Server.Dtos.SignalR.Actions; // Specific Action DTOs
using ShadowverseBeyond.Server.Exceptions; // GameLogicException
using ShadowverseBeyond.Server.Services.Abstractions; // Added for IClientMethodMapper
using ShadowverseBeyond.Server.Domain.Entities; // Added for GameState
using AutoMapper; // Added for IMapper
using System;
using System.Collections.Generic; // List<> for events
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory; // Added for Idempotency Check

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
        private readonly IMemoryCache _memoryCache; // Added for Idempotency Check
        private readonly IClientMethodMapper _clientMethodMapper; // ADDED
        private readonly IMapper _mapper; // Added for Entity to DTO mapping

        /// <summary>
        /// Initializes a new instance of the GameHub class.
        /// </summary>
        /// <param name="gameService">The core game logic service.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="memoryCache">The memory cache instance for idempotency checks.</param> // Added param doc
        /// <param name="clientMethodMapper">The service to map event types to client method names.</param> // ADDED
        /// <param name="mapper">The AutoMapper instance for Entity to DTO conversion.</param> // Added param doc
        public GameHub(IGameService gameService, ILogger<GameHub> logger, IMemoryCache memoryCache, IClientMethodMapper clientMethodMapper, IMapper mapper) // Added mapper parameter
        {
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache)); // Added assignment
            _clientMethodMapper = clientMethodMapper ?? throw new ArgumentNullException(nameof(clientMethodMapper)); // ADDED assignment
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper)); // Added assignment
            _logger.LogInformation("<<<<< GameHub INSTANCE CREATED >>>>>"); // Existing log
        }

        // --- Core Game Actions (Client -> Server) ---
        /// <summary>
        /// Client invokes this method to perform the "Play Card" action.
        /// Delegates the core logic to IGameService and handles exceptions.
        /// </summary>
        /// <param name="actionDto">DTO containing card play details.</param>
        public async Task PlayCard(PlayCardActionDto actionDto)
        {
            _logger.LogWarning("[BUG-015_TRACE] Received PlayCard request. Raw CardUid from client: '{RawCardUid}'", actionDto.CardUid); // Added for BUG-015 diagnosis
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

                // Create PlayerActionWrapper and enqueue the action
                var wrapper = new PlayerActionWrapper
                {
                    ActionType = "PlayCard", // Identifier for the action type
                    PlayerId = Guid.Parse(playerId), // Parse string playerId to Guid
                    ActionData = actionDto // Pass the original DTO as data
                    // RequestId and Timestamp default in PlayerActionWrapper constructor
                };

                _logger.LogInformation("Player {PlayerId} in Game {GameId} enqueuing PlayCard action for Card {CardUid}.",
                    playerId, gameId, actionDto.CardUid);

                // Call the new GameService method to enqueue the action
                await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);

                _logger.LogInformation("Player {PlayerId} successfully enqueued PlayCard action for Card {CardUid} in Game {GameId}.",
                    playerId, gameId, actionDto.CardUid);

                // Event broadcasting is now handled by the GameService processing loop.
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
        /// <param name="requestId">A unique identifier for this specific request, used for idempotency.</param> // Added param doc
        public async Task EndTurn(string requestId) // Added requestId parameter
        {
            // --- ADDED DEBUG LOGGING ---
            _logger.LogInformation("HUB_DEBUG: Entering EndTurn method. Received Request ID type: {Type}, Value: '{Value}'", requestId?.GetType().Name ?? "null", requestId ?? "null");
            if (string.IsNullOrEmpty(requestId))
            {
                _logger.LogError("HUB_CRITICAL: EndTurn method entered BUT requestId is NULL or EMPTY. Argument binding likely failed!");
                // We might still get the InvalidDataException before this point if binding fails severely.
            }
            // --- END ADDED DEBUG LOGGING ---

            // Idempotency Check
            var cacheKey = $"EndTurnRequest_{requestId}";
            if (_memoryCache.TryGetValue(cacheKey, out _))
            {
                _logger.LogWarning("Duplicate EndTurn request received with ID: {RequestId}. Ignoring.", requestId);
                // Optional: Send a specific message back? For now, just ignore.
                // await Clients.Caller.ReceiveError($"Duplicate request ignored: {requestId}");
                return; // Already processed this request
            }

            // Mark this request ID as processed. Set expiration (e.g., 1 minute)
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(1)); // Adjust expiration as needed
            _memoryCache.Set(cacheKey, true, cacheEntryOptions);

            _logger.LogInformation("<<<<< GameHub.EndTurn() METHOD ENTERED - RequestId: {RequestId}, ConnectionId: {ConnectionId} >>>>>", requestId, Context.ConnectionId);

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
                // Create PlayerActionWrapper and enqueue the action
                var wrapper = new PlayerActionWrapper
                {
                    ActionType = "EndTurn",
                    PlayerId = Guid.Parse(playerId), // Parse string playerId to Guid
                    ActionData = new { RequestId = requestId } // Pass requestId for potential tracking/idempotency in service
                };

                _logger.LogInformation("Player {PlayerId} in Game {GameId} enqueuing EndTurn action (RequestId: {RequestId}).",
                    playerId, gameId, requestId);

                // Call the new GameService method to enqueue the action
                await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);

                _logger.LogInformation("Player {PlayerId} successfully enqueued EndTurn action (RequestId: {RequestId}) in Game {GameId}.",
                    playerId, gameId, requestId);

                // Turn transition logic (determining next player, starting their turn, triggering AI)
                // and event broadcasting are now handled by the GameService processing loop.
                // Remove all related logic from the Hub method.


            } // Closing brace for the main try block started at L145
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

                // Create PlayerActionWrapper and enqueue the action
                var wrapper = new PlayerActionWrapper
                {
                    ActionType = "PerformAttack",
                    PlayerId = Guid.Parse(playerId), // Parse string playerId to Guid
                    ActionData = actionDto
                };

                _logger.LogInformation("Player {PlayerId} in Game {GameId} enqueuing Attack action: {Attacker} -> {Target}.",
                    playerId, gameId, actionDto.AttackerUid, actionDto.TargetUid);

                // Call the new GameService method to enqueue the action
                await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);

                _logger.LogInformation("Player {PlayerId} successfully enqueued Attack action in Game {GameId}.",
                    playerId, gameId);

                // Event broadcasting is now handled by the GameService processing loop.
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

                // Create PlayerActionWrapper and enqueue the action
                var wrapper = new PlayerActionWrapper
                {
                    ActionType = "PerformEvolve",
                    PlayerId = Guid.Parse(playerId), // Parse string playerId to Guid
                    ActionData = actionDto
                };

                _logger.LogInformation("Player {PlayerId} in Game {GameId} enqueuing Evolve action for unit {UnitUid}.",
                    playerId, gameId, actionDto.UnitUid);

                // Call the new GameService method to enqueue the action
                await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);

                _logger.LogInformation("Player {PlayerId} successfully enqueued Evolve action for unit {UnitUid} in Game {GameId}.",
                   playerId, gameId, actionDto.UnitUid);

                // Event broadcasting is now handled by the GameService processing loop.
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

                // Convert List<string> to List<Guid> first, as this is needed for the ActionData
                List<Guid> parsedUids = cardUidsToReplace
                    .Select(uidStr => Guid.TryParse(uidStr, out Guid parsedGuid) ? parsedGuid : Guid.Empty) // TryParse, return Guid.Empty on failure
                    .Where(g => g != Guid.Empty) // Filter out Guid.Empty results from failed parses
                    .ToList();

                // Optional: Log if any submitted UIDs were invalid
                int failedParses = cardUidsToReplace.Count - parsedUids.Count;
                if (failedParses > 0)
                {
                    _logger.LogWarning("[GameHub:{GameId}] Player {PlayerId} submitted {FailedCount} invalid GUID format(s) during mulligan. Proceeding with valid ones only.", gameId, playerId, failedParses);
                }

                // Create PlayerActionWrapper and enqueue the action
                var wrapper = new PlayerActionWrapper
                {
                    ActionType = "SubmitMulligan",
                    PlayerId = Guid.Parse(playerId), // Parse string playerId to Guid
                    ActionData = new { CardUidsToReplace = parsedUids } // Pass the parsed GUID list
                };

                _logger.LogInformation("Player {PlayerId} in Game {GameId} enqueuing SubmitMulligan action with {Count} cards to replace.",
                    playerId, gameId, parsedUids.Count);

                // Call the new GameService method to enqueue the action
                await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);

                _logger.LogInformation("Player {PlayerId} successfully enqueued SubmitMulligan action in Game {GameId}.",
                    playerId, gameId);

                // Event broadcasting is now handled by the GameService processing loop.
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
        // --- New Method for Playability Check ---

        /// <summary>
        /// Client invokes this method to check if a specific card in their hand is currently playable.
        /// Returns the playability status and target information directly to the caller.
        /// </summary>
        /// <param name="cardInstanceId">The InstanceId of the card in hand to check.</param>
        // Removed CheckCardPlayability method (Lines 392-445) as playability is now pushed proactively by the server.

        // --- Connection Management ---

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
                    // Parse playerId to Guid and pass it to GetOrCreateGameStateAsync
                    Guid playerGuid = Guid.Parse(playerId);
                    GameState? currentStateEntity = await _gameService.GetOrCreateGameStateAsync(gameId.ToString(), playerGuid); // Pass playerId
                    GameStateDto? currentState = currentStateEntity != null ? _mapper.Map<GameStateDto>(currentStateEntity) : null;
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
        // --- Helper Method for Broadcasting Events ---
        /// <summary>
        /// Broadcasts a single game event to the specified group using the appropriate
        /// strongly-typed client method based on the event's type.
        /// Mirrors the logic from GameService.BroadcastEventAsync but uses Hub context.
        /// </summary>
        /// <param name="groupName">The name of the SignalR group to broadcast to.</param>
        /// <param name="gameEvent">The event DTO to broadcast.</param>
        private async Task BroadcastEventToGroupAsync(string groupName, GameEventDto gameEvent)
        {
            if (gameEvent == null)
            {
                _logger.LogWarning("BroadcastEventToGroupAsync called with null event for group {GroupName}. Skipping.", groupName);
                return;
            }

            var gameId = groupName.Replace("Game_", ""); // Extract gameId for logging consistency if needed

            // Log the attempt (similar to GameService version)
            string eventDetails = "N/A";
            try { eventDetails = System.Text.Json.JsonSerializer.Serialize(gameEvent, new System.Text.Json.JsonSerializerOptions { WriteIndented = false }); }
            catch (Exception jsonEx) { eventDetails = $"Error serializing: {jsonEx.Message}"; }
            _logger.LogInformation("[HUB_BROADCAST_ATTEMPT:{GameId}] Broadcasting event of type {EventType} to group {GroupName}. Details: {EventDetails}",
                gameId, gameEvent.GetType().Name, groupName, eventDetails);

            // Delegate the broadcasting logic entirely to the injected mapper service.
            // The mapper service will handle determining the correct client method and sending the event.
            _logger.LogDebug("[GameHub:{GameId}] Delegating broadcast of event {EventType} to group {GroupName} via IClientMethodMapper.",
                gameId, gameEvent.GetType().Name, groupName);
            await _clientMethodMapper.BroadcastEventToGroupAsync(groupName, gameEvent);
            // The mapper's implementation should handle its own logging and error handling.
        }



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