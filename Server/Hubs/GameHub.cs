// File: Server/Hubs/GameHub.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging;
using System.Text.Json; // Added for JSON Serialization in logging
using ShadowverseBeyond.Server.Dtos.SignalR; // Added for base GameEventDto
using ShadowverseBeyond.Server.Hubs.ClientInterfaces; // IGameClient
using ShadowverseBeyond.Server.Services;            // IGameService
using ShadowverseBeyond.Server.Dtos;            // GameStateDto etc.
using ShadowverseBeyond.Server.Dtos.SignalR.Actions; // Specific Action DTOs
using ShadowverseBeyond.Server.Dtos.SignalR.Events;  // Specific Event DTOs (incl. base GameEventDto)
using ShadowverseBeyond.Server.Exceptions; // GameLogicException
using System;
using System.Collections.Generic; // List<> for events
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory; // Added for Idempotency Check
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
        private readonly IMemoryCache _memoryCache; // Added for Idempotency Check

        /// <summary>
        /// Initializes a new instance of the GameHub class.
        /// </summary>
        /// <param name="gameService">The core game logic service.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="memoryCache">The memory cache instance for idempotency checks.</param> // Added param doc
        public GameHub(IGameService gameService, ILogger<GameHub> logger, IMemoryCache memoryCache) // REMOVED IAiService param
        {
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache)); // Added assignment
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
                        // **FIXED: Use specific client methods based on event type**
                        await BroadcastEventToGroupAsync(groupName, gameEvent); // Use helper
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
                // Delegate to core game logic service and get resulting events
                _logger.LogInformation("[GameHub:{GameId}] >>>>> PRE-CALL: Attempting to call _gameService.EndTurnAsync for Player {PlayerId}...", gameId, playerId);
                // Call EndTurnAsync and get the result tuple
                var endTurnResult = await _gameService.EndTurnAsync(gameId.ToString(), playerId);
                _logger.LogInformation("[GameHub:{GameId}] <<<<< POST-CALL: Completed _gameService.EndTurnAsync for Player {PlayerId}. Success: {Success}, IsNextAI: {IsNextAI}, NextPlayer: {NextPlayerId}, Events: {EventCount}",
                   gameId, playerId, endTurnResult.Success, endTurnResult.IsNextPlayerAi, endTurnResult.NextPlayerId ?? "N/A", endTurnResult.Events?.Count ?? 0);

                // Check if the EndTurn operation itself was successful
                if (!endTurnResult.Success)
                {
                    _logger.LogError("[GameHub:{GameId}] _gameService.EndTurnAsync reported failure for Player {PlayerId}.", gameId, playerId);
                    // Optionally send a specific error if the service indicated failure
                    await Clients.Caller.ReceiveError("Failed to process end turn logic in the game service.");
                    return; // Stop processing if the service failed
                }

                // --- Refactor: Start next player's turn explicitly ---
                List<GameEventDto> startTurnEvents = new List<GameEventDto>();
                if (!string.IsNullOrEmpty(endTurnResult.NextPlayerId))
                {
                    _logger.LogInformation("[GameHub:{GameId}] Calling StartPlayerTurnAsync for next player {NextPlayerId}.", gameId, endTurnResult.NextPlayerId);
                    try
                    {
                        // Call StartPlayerTurnAsync and capture the result tuple
                        var startTurnResultTuple = await _gameService.StartPlayerTurnAsync(gameId.ToString(), endTurnResult.NextPlayerId);
                        startTurnEvents = startTurnResultTuple.Events ?? new List<GameEventDto>(); // Capture events from the tuple
                        _logger.LogInformation("[GameHub:{GameId}] StartPlayerTurnAsync completed for {NextPlayerId}. Got {EventCount} events.", gameId, endTurnResult.NextPlayerId, startTurnEvents.Count);
                    }
                    catch (Exception startEx)
                    {
                        _logger.LogError(startEx, "[GameHub:{GameId}] Error calling StartPlayerTurnAsync for {NextPlayerId}.", gameId, endTurnResult.NextPlayerId);
                        await Clients.Caller.ReceiveError($"Server error starting next turn for {endTurnResult.NextPlayerId}.");
                        // Consider if we should return here or continue broadcasting EndTurn events
                    }
                }
                else
                {
                    _logger.LogWarning("[GameHub:{GameId}] NextPlayerId from EndTurnAsync was null or empty. Cannot start next turn.", gameId);
                }

                // Combine events from EndTurn and StartTurn
                var allEventsToBroadcast = new List<GameEventDto>();
                if (endTurnResult.Events != null) allEventsToBroadcast.AddRange(endTurnResult.Events);
                allEventsToBroadcast.AddRange(startTurnEvents); // Add start turn events
                _logger.LogInformation("[GameHub:{GameId}] Combined events: {EndTurnEventCount} from EndTurn + {StartTurnEventCount} from StartTurn = {TotalEventCount} total.",
                    gameId, endTurnResult.Events?.Count ?? 0, startTurnEvents.Count, allEventsToBroadcast.Count);
                // --- End Refactor ---

                // Broadcast the COMBINED events generated by EndTurnAsync AND StartPlayerTurnAsync
                if (allEventsToBroadcast.Count > 0) // Use the combined list
                {
                    string groupName = $"Game_{gameId}";
                    _logger.LogInformation("[GameHub:{GameId}] Broadcasting {EventCount} combined events to group {GroupName}.", gameId, allEventsToBroadcast.Count, groupName); // Update log message
                    int eventIndex = 0;
                    foreach (var gameEvent in allEventsToBroadcast) // Use the combined list
                    {
                        eventIndex++;
                        try
                        {
                            if (gameEvent == null)
                            {
                                _logger.LogWarning("[GameHub:{GameId}] Skipping broadcast for null event at index {Index} from combined list.", gameId, eventIndex);
                                continue;
                            }
                            string eventType = gameEvent.GetType().Name;
                            _logger.LogDebug("[GameHub:{GameId}] Broadcasting Combined Event #{Index} ({EventType}) to group {GroupName}...", gameId, eventIndex, eventType, groupName);
                            await BroadcastEventToGroupAsync(groupName, gameEvent);
                            _logger.LogDebug("[GameHub:{GameId}] Successfully broadcasted Combined Event #{Index} ({EventType}) to group {GroupName}.", gameId, eventIndex, eventType, groupName);
                        }
                        catch (Exception ex)
                        {
                            string eventTypeInfo = gameEvent?.GetType()?.Name ?? "Unknown (event was null)";
                            _logger.LogError(ex, "[GameHub:{GameId}] --- FAILED to broadcast Combined Event #{Index} ({EventTypeInfo}) to group {GroupName} ---", gameId, eventIndex, eventTypeInfo, groupName);
                        }
                    }
                    _logger.LogInformation("[GameHub:{GameId}] Finished broadcasting loop for {EventCount} combined events to group {GroupName}.", gameId, allEventsToBroadcast.Count, groupName);
                }
                else
                {
                    _logger.LogInformation("[GameHub:{GameId}] EndTurnAsync and StartPlayerTurnAsync completed successfully for Player {PlayerId}, but returned no combined events.", gameId, playerId);
                }


                // Check if the next player is AI and trigger their turn if necessary (AFTER broadcasting all events)
                if (endTurnResult.IsNextPlayerAi && !string.IsNullOrEmpty(endTurnResult.NextPlayerId))
                {
                    _logger.LogInformation("[GameHub:{GameId}] Next player {PlayerId} is AI. Triggering AI action phase via _gameService.TriggerAiTurnAsync (fire-and-forget).", gameId, endTurnResult.NextPlayerId);
                    // Use fire-and-forget for the trigger from the Hub
                    _ = _gameService.TriggerAiTurnAsync(gameId.ToString(), endTurnResult.NextPlayerId);
                }
                else if (!endTurnResult.IsNextPlayerAi && !string.IsNullOrEmpty(endTurnResult.NextPlayerId))
                {
                    _logger.LogInformation("[GameHub:{GameId}] Next player {PlayerId} is Human. Turn started via events broadcast above. No Hub AI trigger needed.", gameId, endTurnResult.NextPlayerId);
                }
                else // Handles cases where NextPlayerId is null or empty (e.g., game ended)
                {
                    _logger.LogWarning("[GameHub:{GameId}] EndTurnAsync completed, but NextPlayerId is null or empty. Assuming game ended or an issue occurred in GameService.", gameId);
                    // Game Over events should have been included in the broadcast list if the game ended normally.
                }
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
                        // **FIXED: Use specific client methods based on event type**
                        await BroadcastEventToGroupAsync(groupName, gameEvent); // Use helper
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
                        // **FIXED: Use specific client methods based on event type**
                        await BroadcastEventToGroupAsync(groupName, gameEvent); // Use helper
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
                // Convert List<string> to List<Guid>, handling potential parse errors
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

                _logger.LogDebug("Attempting to call _gameService.SubmitMulliganAsync for Game {GameId}, Player {PlayerId} with {GuidCount} parsed GUIDs.", gameId, playerId, parsedUids.Count); // Adjusted log before call
                await _gameService.SubmitMulliganAsync(gameId.ToString(), playerId, parsedUids); // Pass the parsed List<Guid>

                // GameService is responsible for broadcasting events/state updates after mulligan completion.
                _logger.LogInformation("Player {PlayerId} successfully submitted mulligan in Game {GameId}", playerId, gameId);
                // --- AI Trigger logic removed, now handled by GameService after mulligan completion ---
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

            try
            {
                // Switch on the event type and call the corresponding IGameClient method
                switch (gameEvent)
                {
                    case TurnStartedEventDto e: await Clients.Group(groupName).TurnStarted(e); break;
                    case TurnEndedEventDto e: await Clients.Group(groupName).TurnEnded(e); break;
                    case PpUpdateEventDto e: await Clients.Group(groupName).PpUpdate(e); break;
                    case EpUpdateEventDto e: await Clients.Group(groupName).EpUpdate(e); break;
                    case HandUpdateEventDto e: await Clients.Group(groupName).HandUpdate(e); break;
                    case DeckUpdateEventDto e: await Clients.Group(groupName).DeckUpdate(e); break;
                    case UnitPlayedEventDto e: await Clients.Group(groupName).UnitPlayed(e); break;
                    case SpellPlayedEventDto e: await Clients.Group(groupName).SpellPlayed(e); break;
                    case AmuletPlayedEventDto e: await Clients.Group(groupName).AmuletPlayed(e); break;
                    case UnitEvolvedEventDto e: await Clients.Group(groupName).UnitEvolved(e); break;
                    case UnitDestroyedEventDto e: await Clients.Group(groupName).UnitDestroyed(e); break;
                    case AttackDeclaredEventDto e: await Clients.Group(groupName).AttackDeclared(e); break;
                    case DamageDealtEventDto e: await Clients.Group(groupName).DamageDealt(e); break;
                    case HealthChangedEventDto e: await Clients.Group(groupName).HealthChanged(e); break;
                    case StatChangeEventDto e: await Clients.Group(groupName).StatChange(e); break;
                    case GraveyardUpdateEventDto e: await Clients.Group(groupName).GraveyardUpdate(e); break;
                    case GameOverEventDto e: await Clients.Group(groupName).GameOver(e); break;
                    case PlayerMulliganCompletedEventDto e: await Clients.Group(groupName).PlayerMulliganCompleted(e); break;
                    // Add other specific event types from IGameClient here
                    default:
                        _logger.LogWarning("[GameHub:{GameId}] Unknown or unhandled event type encountered in BroadcastEventToGroupAsync: {EventType} for group {GroupName}",
                            gameId, gameEvent.GetType().Name, groupName);
                        // Fallback to generic if absolutely necessary? Or just log? Let's just log.
                        // await Clients.Group(groupName).ReceiveGameEvent(gameEvent); // Avoid falling back to the problematic generic method
                        break;
                }
                _logger.LogDebug("[GameHub:{GameId}] Successfully broadcasted event {EventType} to group {GroupName}.", gameId, gameEvent.GetType().Name, groupName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameHub:{GameId}] Error broadcasting event {EventType} to group {GroupName}.", gameId, gameEvent.GetType().Name, groupName);
                // Don't re-throw; log the error and continue if possible.
            }
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