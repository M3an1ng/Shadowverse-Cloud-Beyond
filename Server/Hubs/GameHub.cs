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
using ShadowverseBeyond.Server.Domain.Entities; // Added for GameState / MatchRoom
using AutoMapper; // Added for IMapper
using System;
using System.Collections.Generic; // List<> for events
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory; // Added for Idempotency Check
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Domain.Events.Services;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Options;

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
        private readonly IMatchService _matchService; // Added for lobby validation
        private readonly ICommittedEventJournal _eventJournal;
        private readonly ISnapshotStore _snapshotStore;
        private readonly ReplayOptions _replayOptions;
        private readonly IRecordedEventMapper _recordedEventMapper;

        /// <summary>
        /// Initializes a new instance of the GameHub class.
        /// </summary>
        /// <param name="gameService">The core game logic service.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="memoryCache">The memory cache instance for idempotency checks.</param> // Added param doc
        /// <param name="clientMethodMapper">The service to map event types to client method names.</param> // ADDED
        /// <param name="mapper">The AutoMapper instance for Entity to DTO conversion.</param> // Added param doc
        public GameHub(
            IGameService gameService,
            ILogger<GameHub> logger,
            IMemoryCache memoryCache,
            IClientMethodMapper clientMethodMapper,
            IMapper mapper,
            IMatchService matchService,
            ICommittedEventJournal eventJournal,
            ISnapshotStore snapshotStore,
            IRecordedEventMapper recordedEventMapper,
            IOptions<ReplayOptions> replayOptions) // Added mapper parameter
        {
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache)); // Added assignment
            _clientMethodMapper = clientMethodMapper ?? throw new ArgumentNullException(nameof(clientMethodMapper)); // ADDED assignment
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper)); // Added assignment
            _matchService = matchService ?? throw new ArgumentNullException(nameof(matchService));
            _eventJournal = eventJournal ?? throw new ArgumentNullException(nameof(eventJournal));
            _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
            _recordedEventMapper = recordedEventMapper ?? throw new ArgumentNullException(nameof(recordedEventMapper));
            _replayOptions = replayOptions.Value;
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
        /// Client invokes this method to surrender the match.
        /// </summary>
        /// <param name="requestId">A unique identifier for this specific request, used for idempotency.</param>
        public async Task Surrender(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
            {
                _logger.LogWarning("Surrender called with null/empty requestId. ConnectionId={ConnectionId}", Context.ConnectionId);
                return;
            }

            var cacheKey = $"SurrenderRequest_{requestId}";
            if (_memoryCache.TryGetValue(cacheKey, out _))
            {
                _logger.LogWarning("Duplicate Surrender request received with ID: {RequestId}. Ignoring.", requestId);
                return;
            }

            _memoryCache.Set(cacheKey, true, new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(1)));

            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("Surrender called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                return;
            }

            try
            {
                var wrapper = new PlayerActionWrapper
                {
                    ActionType = "Surrender",
                    PlayerId = Guid.Parse(playerId),
                    ActionData = new { RequestId = requestId }
                };

                _logger.LogInformation("Player {PlayerId} in Game {GameId} enqueuing Surrender action (RequestId: {RequestId}).",
                    playerId, gameId, requestId);

                await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} surrendering: {Message}", playerId, gameId, ex.Message);
                await Clients.Caller.ReceiveError($"Surrender Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing Surrender for Player {PlayerId} in Game {GameId}", playerId, gameId);
                await Clients.Caller.ReceiveError("An unexpected server error occurred while surrendering.");
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
        /// Client invokes this method to super evolve a unit.
        /// Delegates the core logic to IGameService, gets resulting events, and broadcasts them.
        /// </summary>
        /// <param name="actionDto">DTO containing super evolution details.</param>
        public async Task PerformSuperEvolve(SuperEvolveActionDto actionDto)
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("PerformSuperEvolve called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                return;
            }

            try
            {
                _logger.LogInformation("Player {PlayerId} in Game {GameId} attempting to super evolve unit {UnitUid}",
                    playerId, gameId, actionDto.UnitUid);

                var wrapper = new PlayerActionWrapper
                {
                    ActionType = "PerformSuperEvolve",
                    PlayerId = Guid.Parse(playerId),
                    ActionData = actionDto
                };

                _logger.LogInformation("Player {PlayerId} in Game {GameId} enqueuing SuperEvolve action for unit {UnitUid}.",
                    playerId, gameId, actionDto.UnitUid);

                await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);

                _logger.LogInformation("Player {PlayerId} successfully enqueued SuperEvolve action for unit {UnitUid} in Game {GameId}.",
                    playerId, gameId, actionDto.UnitUid);
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} super evolving unit {UnitUid}: {Message}",
                    playerId, gameId, actionDto.UnitUid, ex.Message);
                await Clients.Caller.ReceiveError($"Super Evolve Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing SuperEvolve for Player {PlayerId} in Game {GameId}, Unit {UnitUid}",
                    playerId, gameId, actionDto.UnitUid);
                await Clients.Caller.ReceiveError("An unexpected server error occurred during super evolution.");
            }
        }

        /// <summary>
        /// Client invokes this method to use PP+1.
        /// </summary>
        public async Task PerformPpBoost()
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("PerformPpBoost called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                return;
            }

            try
            {
                _logger.LogInformation("Player {PlayerId} in Game {GameId} attempting to use PP+1", playerId, gameId);

                var wrapper = new PlayerActionWrapper
                {
                    ActionType = "PerformPpBoost",
                    PlayerId = Guid.Parse(playerId),
                    ActionData = new { }
                };

                await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} using PP+1: {Message}",
                    playerId, gameId, ex.Message);
                await Clients.Caller.ReceiveError($"PP+1 Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing PP+1 for Player {PlayerId} in Game {GameId}",
                    playerId, gameId);
                await Clients.Caller.ReceiveError("An unexpected server error occurred while using PP+1.");
            }
        }

        /// <summary>
        /// Client invokes this method to request activating a card's Act ability.
        /// </summary>
        public async Task RequestAct(ActActionDto actionDto)
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("RequestAct called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                return;
            }

            try
            {
                _logger.LogInformation("Player {PlayerId} in Game {GameId} attempting to use Act on card {CardUid}",
                    playerId, gameId, actionDto.CardUid);

                var wrapper = new PlayerActionWrapper
                {
                    ActionType = "RequestAct",
                    PlayerId = Guid.Parse(playerId),
                    ActionData = actionDto
                };

                await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} requesting Act: {Message}",
                    playerId, gameId, ex.Message);
                await Clients.Caller.ReceiveError($"Act Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing RequestAct for Player {PlayerId} in Game {GameId}",
                    playerId, gameId);
                await Clients.Caller.ReceiveError("An unexpected server error occurred while requesting Act.");
            }
        }

        /// <summary>
        /// Client invokes this method to request activating a card's Fusion ability.
        /// </summary>
        public async Task RequestFusion(FusionActionDto actionDto)
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("RequestFusion called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                return;
            }

            try
            {
                _logger.LogInformation("Player {PlayerId} in Game {GameId} attempting to use Fusion on card {CardUid}",
                    playerId, gameId, actionDto.CardUid);

                var wrapper = new PlayerActionWrapper
                {
                    ActionType = "RequestFusion",
                    PlayerId = Guid.Parse(playerId),
                    ActionData = actionDto
                };

                await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} requesting Fusion: {Message}",
                    playerId, gameId, ex.Message);
                await Clients.Caller.ReceiveError($"Fusion Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing RequestFusion for Player {PlayerId} in Game {GameId}",
                    playerId, gameId);
                await Clients.Caller.ReceiveError("An unexpected server error occurred while requesting Fusion.");
            }
        }

        /// <summary>
        /// Debug/ADMIN: Execute a debug command to inject events (DEBUG console only).
        /// </summary>
        public async Task ExecuteDebugCommand(DebugCommandDto commandDto)
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("ExecuteDebugCommand called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                return;
            }

            try
            {
                if (!Guid.TryParse(playerId, out var playerGuid))
                {
                    throw new HubException("debug-error: invalid player id");
                }

                var wrapper = new PlayerActionWrapper
                {
                    ActionType = "DebugCommand",
                    PlayerId = playerGuid,
                    ActionData = commandDto ?? new DebugCommandDto()
                };

                _logger.LogInformation("Player {PlayerId} in Game {GameId} enqueuing DebugCommand: {Command}", playerId, gameId, commandDto?.Command);
                await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);
            }
            catch (HubException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing ExecuteDebugCommand for Player {PlayerId} in Game {GameId}", playerId, gameId);
                await Clients.Caller.ReceiveError($"Debug command error: {ex.Message}");
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

        public async Task SubmitTargetChoice(SubmitTargetChoiceActionDto actionDto)
        {
            // --- DIAGNOSTIC LOGGING ---
            _logger.LogWarning("[CHOICE_DEBUG] SubmitTargetChoice method ENTERED. DTO: {DtoJson}", System.Text.Json.JsonSerializer.Serialize(actionDto));
            // --- END DIAGNOSTIC LOGGING ---

            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty || string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("SubmitTargetChoice called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                return;
            }

            try
            {
                _logger.LogInformation("Player {PlayerId} in Game {GameId} submitting target choice for ability {AbilityId}",
                    playerId, gameId, actionDto.SourceAbilityRuntimeId);

                // Directly call the new out-of-band method on the game service
                await _gameService.SubmitTargetChoiceAsync(gameId.ToString(), Guid.Parse(playerId), actionDto);

                _logger.LogInformation("Player {PlayerId} successfully processed SubmitTargetChoice action in Game {GameId}.",
                    playerId, gameId);
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("Game Logic Error for Player {PlayerId} in Game {GameId} submitting target choice: {Message}",
                    playerId, gameId, ex.Message);
                await Clients.Caller.ReceiveError($"Submit Target Choice Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing SubmitTargetChoice for Player {PlayerId} in Game {GameId}",
                    playerId, gameId);
                await Clients.Caller.ReceiveError("An unexpected server error occurred while submitting the target choice.");
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
            var httpContext = Context.GetHttpContext();
            if (httpContext is null)
            {
                _logger.LogError("HttpContext is null for ConnectionId {ConnectionId} in OnConnectedAsync. Aborting.", Context.ConnectionId);
                Context.Abort();
                return;
            }

            var gameIdStr = httpContext.Request.Query["gameId"].ToString();
            var playerIdStr = httpContext.Request.Query["playerId"].ToString();
            var token = httpContext.Request.Query["token"].ToString();
            var spectate = httpContext.Request.Query["spectate"].ToString();

            if (!Guid.TryParse(gameIdStr, out var gameId))
            {
                _logger.LogWarning("Invalid connection parameters. ConnectionId={ConnectionId}, gameId={GameId}, playerId={PlayerId}", Context.ConnectionId, gameIdStr, playerIdStr);
                await Clients.Caller.ReceiveError("连接参数无效，无法加入房间。");
                Context.Abort();
                return;
            }

            var isSpectator = string.Equals(spectate, "1", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(spectate, "true", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(spectate, "yes", StringComparison.OrdinalIgnoreCase);

            if (isSpectator)
            {
                var roomResult = _matchService.Get(gameIdStr);
                if (!roomResult.Success || roomResult.Room == null)
                {
                    _logger.LogWarning("Spectator attempted to join non-existent room. ConnectionId={ConnectionId}, GameId={GameId}",
                        Context.ConnectionId, gameIdStr);
                    await Clients.Caller.ReceiveError("房间不存在，无法观战。");
                    Context.Abort();
                    return;
                }

                if (_replayOptions.RequireJoinCodeForReplayReads)
                {
                    var joinCode = httpContext.Request.Query["joinCode"].ToString();
                    if (string.IsNullOrWhiteSpace(joinCode) ||
                        !string.Equals(roomResult.Room.JoinCode, joinCode, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Spectator join denied (invalid joinCode). ConnectionId={ConnectionId}, GameId={GameId}",
                            Context.ConnectionId, gameIdStr);
                        await Clients.Caller.ReceiveError("邀请码不正确，无法观战。");
                        Context.Abort();
                        return;
                    }
                }

                if (roomResult.Room.Status != MatchStatus.Started)
                {
                    _logger.LogInformation("Spectator attempted to join room not started yet. ConnectionId={ConnectionId}, GameId={GameId}, Status={Status}",
                        Context.ConnectionId, gameIdStr, roomResult.Room.Status);
                    await Clients.Caller.ReceiveError("房间未开始，暂不可观战。");
                    Context.Abort();
                    return;
                }

                Context.Items["GameId"] = gameIdStr;

                var spectatorGroupName = $"Game_{gameId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, spectatorGroupName);

                _logger.LogInformation("Spectator connected: ConnectionId={ConnectionId}, GameId={GameId}, Added to Group={GroupName}",
                    Context.ConnectionId, gameId, spectatorGroupName);

                try
                {
                    var currentStateEntity = _gameService.GetGameState(gameIdStr);
                    if (currentStateEntity == null)
                    {
                        _logger.LogWarning("Spectator requested state but server has no active GameState. GameId={GameId}", gameIdStr);
                        await Clients.Caller.ReceiveError("当前对局状态不可用，无法观战。");
                        Context.Abort();
                        return;
                    }

                    var requestServices = httpContext.RequestServices;
                    var gameContextAccessor = requestServices.GetRequiredService<IGameContextAccessor>();

                    GameStateDto currentState;
                    using (var tempContext = new GameContext(
                        requestServices,
                        _mapper,
                        gameContextAccessor,
                        currentStateEntity,
                        roomResult.Room.HostPlayerId, // Best-effort for context construction
                        Context.ConnectionAborted
                    ))
                    {
                        currentState = _mapper.Map<GameStateDto>(currentStateEntity);
                    }

                    await Clients.Caller.ReceiveGameState(currentState);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending initial state to spectator in Game {GameId}", gameIdStr);
                    await Clients.Caller.ReceiveError("服务器错误：加载观战初始状态失败。");
                    Context.Abort();
                    return;
                }

                await base.OnConnectedAsync();
                return;
            }

            if (!Guid.TryParse(playerIdStr, out var playerGuid) || string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Invalid connection parameters. ConnectionId={ConnectionId}, gameId={GameId}, playerId={PlayerId}", Context.ConnectionId, gameIdStr, playerIdStr);
                await Clients.Caller.ReceiveError("连接参数无效，无法加入房间。");
                Context.Abort();
                return;
            }

            var authResult = _matchService.ValidateConnection(gameIdStr, playerGuid, token);
            if (!authResult.Success || authResult.Room == null)
            {
                _logger.LogWarning("Match validation failed for ConnectionId={ConnectionId}, GameId={GameId}, PlayerId={PlayerId}. Error={Error}",
                    Context.ConnectionId, gameIdStr, playerIdStr, authResult.Error);
                await Clients.Caller.ReceiveError(authResult.Message ?? "房间验证失败，无法加入。");
                Context.Abort();
                return;
            }

            // Set items for filters (GameContextFilter will pick these up)
            Context.Items["GameId"] = gameIdStr;
            Context.Items["PlayerId"] = playerIdStr;

            var room = authResult.Room;
            if (room.Status == MatchStatus.Ready)
            {
                _matchService.Start(gameIdStr, playerGuid); // Best-effort attempt to mark started
                var refreshed = _matchService.Get(gameIdStr);
                if (refreshed.Success && refreshed.Room != null)
                {
                    room = refreshed.Room;
                }
            }

            string groupName = $"Game_{gameId}"; // Consistent group naming convention
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            string playerGroupName = $"Player_{playerIdStr}";
            await Groups.AddToGroupAsync(Context.ConnectionId, playerGroupName);

            _logger.LogInformation("Client connected: ConnectionId={ConnectionId}, PlayerId={PlayerId}, GameId={GameId}, Added to Group={GroupName}",
                Context.ConnectionId, playerIdStr, gameId, groupName);

            // Only create game state when match is started, to avoid "ghost seats" or hanging initialization.
            if (!room.UseAiFallback && room.Status != MatchStatus.Started)
            {
                _logger.LogInformation("Room {GameId} not started yet (Status={Status}). Skipping GameState creation.", gameId, room.Status);
                await Clients.Caller.ReceiveError("房间未开始，请等待双方准备完成后再进入对战。");
                await base.OnConnectedAsync();
                return;
            }

            // Request initial state from GameService and broadcast to group
            try
            {
                var requestServices = httpContext.RequestServices;
                var currentStateEntity = await _gameService.GetOrCreateGameStateAsync(
                    gameIdStr,
                    room.HostPlayerId,
                    room.GuestPlayerId,
                    room.UseAiFallback,
                    requestServices);

                if (currentStateEntity != null)
                {
                    var gameContextAccessor = requestServices.GetRequiredService<IGameContextAccessor>();

                    GameStateDto currentState;
                    using (var tempContext = new GameContext(
                        requestServices,
                        _mapper,
                        gameContextAccessor,
                        currentStateEntity,
                        playerGuid,
                        Context.ConnectionAborted // Use the connection's cancellation token
                    ))
                    {
                        currentState = _mapper.Map<GameStateDto>(currentStateEntity);
                    }

                    _logger.LogInformation("Broadcasting initial game state to Game {GameId}", gameId);
                    await Clients.Group(groupName).ReceiveGameState(currentState);
                }
                else
                {
                    _logger.LogWarning("Could not retrieve initial game state for Game {GameId}. Service returned null.", gameId);
                    await Clients.Caller.ReceiveError("Failed to load initial game state.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving or sending initial game state for Player {PlayerId} in Game {GameId}", playerIdStr, gameId);
                await Clients.Caller.ReceiveError("Server error loading initial game state.");
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
                    if (!string.IsNullOrEmpty(playerId) && Guid.TryParse(playerId, out var playerGuid))
                    {
                        var wrapper = new PlayerActionWrapper
                        {
                            ActionType = "Disconnect",
                            PlayerId = playerGuid,
                            ActionData = new { ConnectionId = Context.ConnectionId }
                        };

                        try
                        {
                            await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to enqueue Disconnect action: GameId={GameId} PlayerId={PlayerId}", gameId, playerId);
                        }
                    }
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
        /// Replays recorded client events from the persistent journal to the caller.
        /// Useful for reconnect/catch-up and debug replay.
        /// </summary>
        public async Task CatchUp(long fromSeq = 1, int limit = 5000)
        {
            var gameId = GetGameIdForReplay();
            if (string.IsNullOrWhiteSpace(gameId))
            {
                await Clients.Caller.ReceiveError("无法确定 GameId，补流失败。");
                return;
            }

            if (fromSeq < 1) fromSeq = 1;
            if (limit < 1) limit = 1;
            limit = Math.Min(limit, 20000);

            var records = await _eventJournal.ReadRangeAsync(gameId, fromSeq, limit, Context.ConnectionAborted);
            foreach (var record in records)
            {
                foreach (var env in record.ClientDtos)
                {
                    var dto = _recordedEventMapper.TryDeserializeClientDto(env);
                    if (dto == null) continue;
                    await _clientMethodMapper.BroadcastEventToClientAsync(Context.ConnectionId, dto);
                }
            }
        }

        /// <summary>
        /// Seeks to a specific sequence by sending an appropriate snapshot, then replaying events up to targetSeq.
        /// </summary>
        public async Task Seek(long targetSeq)
        {
            var gameId = GetGameIdForReplay();
            if (string.IsNullOrWhiteSpace(gameId))
            {
                await Clients.Caller.ReceiveError("无法确定 GameId，跳转失败。");
                return;
            }

            if (targetSeq < 1) targetSeq = 1;

            var snapshot = await _snapshotStore.GetAtOrBeforeAsync(gameId, targetSeq, Context.ConnectionAborted);
            if (snapshot == null)
            {
                await Clients.Caller.ReceiveError("没有可用快照，跳转失败。");
                return;
            }

            await Clients.Caller.ReceiveGameState(snapshot.Snapshot);

            var fromSeq = snapshot.AtSeq + 1;
            const int PageSize = 5000;
            while (fromSeq <= targetSeq)
            {
                var page = await _eventJournal.ReadRangeAsync(gameId, fromSeq, PageSize, Context.ConnectionAborted);
                if (page.Count == 0) break;

                foreach (var record in page)
                {
                    if (record.Seq > targetSeq) return;

                    foreach (var env in record.ClientDtos)
                    {
                        var dto = _recordedEventMapper.TryDeserializeClientDto(env);
                        if (dto == null) continue;
                        await _clientMethodMapper.BroadcastEventToClientAsync(Context.ConnectionId, dto);
                    }
                }

                fromSeq = page[^1].Seq + 1;
            }
        }

        private string? GetGameIdForReplay()
        {
            if (Context.Items.TryGetValue("GameId", out var gameIdItem) &&
                gameIdItem is string gameIdStrItem &&
                !string.IsNullOrWhiteSpace(gameIdStrItem))
            {
                return gameIdStrItem;
            }

            var httpContext = Context.GetHttpContext();
            return httpContext?.Request.Query["gameId"].ToString();
        }

        /// <summary>
        /// Extracts GameId and PlayerId from the current Hub connection context.
        /// Placeholder implementation relying on QueryString. Replace with robust auth/mapping later.
        /// </summary>
        /// <returns>A tuple containing GameId (Guid) and PlayerId (string?). Returns (Guid.Empty, null) if info not found.</returns>
        private Task<(Guid GameId, string? PlayerId)> GetGamePlayerInfoFromContextAsync()
        {
            // First try items set during connection validation
            if (Context.Items.TryGetValue("GameId", out var gameIdItem) &&
                Context.Items.TryGetValue("PlayerId", out var playerIdItem) &&
                gameIdItem is string gameIdStrItem &&
                playerIdItem is string playerIdStrItem &&
                Guid.TryParse(gameIdStrItem, out var gameIdFromItems))
            {
                return Task.FromResult<(Guid, string?)>((gameIdFromItems, playerIdStrItem));
            }

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
