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
using ShadowverseBeyond.Server.Domain.Constants;
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
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Infrastructure.Data;

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
        private readonly IGameChatService _gameChatService;
        private readonly ICommittedEventJournal _eventJournal;
        private readonly ISnapshotStore _snapshotStore;
        private readonly ReplayOptions _replayOptions;
        private readonly IRecordedEventMapper _recordedEventMapper;
        private static readonly TimeSpan DisconnectGracePeriod = TimeSpan.FromSeconds(GameConstants.Network.DefaultConnectionTimeoutSeconds);
        private const string DisconnectCachePrefix = "PendingDisconnect";
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _disconnectTimeouts = new();

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
            IGameChatService gameChatService,
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
            _gameChatService = gameChatService ?? throw new ArgumentNullException(nameof(gameChatService));
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
                    playerId, actionDto.CardUid, gameId);

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
        /// Client invokes this method to request a full game state sync.
        /// </summary>
        public async Task RequestInitialGameState()
        {
            var (gameId, playerId) = await GetGamePlayerInfoFromContextAsync();
            if (gameId == Guid.Empty)
            {
                _logger.LogWarning("RequestInitialGameState called by unidentified connection: {ConnectionId}", Context.ConnectionId);
                await Clients.Caller.ReceiveError("无法识别对局信息，无法同步状态。");
                return;
            }

            var gameIdStr = gameId.ToString();
            var currentStateEntity = _gameService.GetGameState(gameIdStr);
            if (currentStateEntity == null)
            {
                _logger.LogWarning("RequestInitialGameState failed: no active GameState. GameId={GameId}", gameIdStr);
                await Clients.Caller.ReceiveError("当前对局状态不可用，无法同步。");
                return;
            }

            var httpContext = Context.GetHttpContext();
            if (httpContext == null)
            {
                _logger.LogWarning("RequestInitialGameState failed: HttpContext null. ConnectionId={ConnectionId}", Context.ConnectionId);
                await Clients.Caller.ReceiveError("服务器环境异常，无法同步状态。");
                return;
            }

            Guid contextPlayerId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(playerId) && Guid.TryParse(playerId, out var parsedPlayerId))
            {
                contextPlayerId = parsedPlayerId;
            }
            else
            {
                var roomResult = _matchService.Get(gameIdStr);
                if (roomResult.Success && roomResult.Room != null)
                {
                    contextPlayerId = roomResult.Room.HostPlayerId;
                }
            }

            try
            {
                var requestServices = httpContext.RequestServices;
                var gameContextAccessor = requestServices.GetRequiredService<IGameContextAccessor>();

                GameStateDto currentState;
                using (var tempContext = new GameContext(
                    requestServices,
                    _mapper,
                    gameContextAccessor,
                    currentStateEntity,
                    contextPlayerId,
                    Context.ConnectionAborted
                ))
                {
                    currentState = _mapper.Map<GameStateDto>(currentStateEntity);
                }

                await Clients.Caller.ReceiveGameState(currentState);

                if (contextPlayerId != Guid.Empty)
                {
                    try
                    {
                        await _gameService.RefreshHandPlayabilityAsync(gameIdStr, contextPlayerId, Context.ConnectionAborted);
                        await _gameService.RefreshActPlayabilityAsync(gameIdStr, contextPlayerId, Context.ConnectionAborted);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error refreshing playability after full state sync. ConnectionId={ConnectionId}, GameId={GameId}, PlayerId={PlayerId}", Context.ConnectionId, gameIdStr, contextPlayerId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending requested game state to ConnectionId={ConnectionId}, GameId={GameId}", Context.ConnectionId, gameIdStr);
                await Clients.Caller.ReceiveError("服务器错误：同步对局状态失败。");
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
        /// Sends a room chat message to all participants in the current game.
        /// </summary>
        public async Task SendChatMessage(SendChatMessageActionDto actionDto)
        {
            if (!TryResolveChatParticipantContext(out var gameId, out var senderType, out var senderId, out var senderName, out var senderAvatarUrl))
            {
                await Clients.Caller.ReceiveError("聊天发送失败：无法识别当前连接上下文。");
                return;
            }

            var gameIdStr = gameId.ToString("D");
            var roomResult = _matchService.Get(gameIdStr);
            if (!roomResult.Success || roomResult.Room == null || roomResult.Room.Status == MatchStatus.Closed)
            {
                _gameChatService.ClearGame(gameIdStr);
                var reason = roomResult.Room?.ClosedReason ?? roomResult.Message ?? "房间已关闭。";
                await Clients.Caller.ReceiveError($"ROOM_CLOSED:{reason}");
                return;
            }

            if (!_gameChatService.TryNormalizeContent(actionDto?.Content, out var normalizedContent, out var validationError))
            {
                await Clients.Caller.ReceiveError($"聊天发送失败：{validationError}");
                return;
            }

            var participantKey = $"{gameIdStr}:{senderType}:{senderId}";
            var nowUtc = DateTime.UtcNow;
            if (!_gameChatService.TryConsumeRateLimit(participantKey, nowUtc, out var retryAfter))
            {
                var waitSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                await Clients.Caller.ReceiveError($"发送过于频繁，请在 {waitSeconds} 秒后重试。");
                return;
            }

            var message = new ChatMessageEventDto
            {
                MessageId = Guid.NewGuid(),
                GameId = gameIdStr,
                SenderType = senderType,
                SenderId = senderId,
                SenderName = senderName,
                SenderAvatarUrl = senderAvatarUrl,
                Content = normalizedContent,
                SentAtUtc = nowUtc,
                ClientMessageId = string.IsNullOrWhiteSpace(actionDto?.ClientMessageId) ? null : actionDto.ClientMessageId.Trim()
            };

            _gameChatService.AppendMessage(message);
            await Clients.Group(SignalRGroupNames.Game(gameId)).ChatMessage(message);
        }

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

                var spectatorDisplayName = NormalizeChatDisplayName(
                    httpContext.Request.Query["spectatorName"].ToString(),
                    BuildSpectatorFallbackDisplayName());
                var spectatorParticipantId = NormalizeChatParticipantId(
                    httpContext.Request.Query["spectatorUserId"].ToString(),
                    Context.ConnectionId);
                var spectatorAvatarUrl = NormalizeChatAvatarUrl(
                    httpContext.Request.Query["spectatorAvatar"].ToString());

                Context.Items["GameId"] = gameIdStr;
                Context.Items["ParticipantType"] = ChatSenderType.Spectator;
                Context.Items["ParticipantId"] = spectatorParticipantId;
                Context.Items["DisplayName"] = spectatorDisplayName;
                Context.Items["AvatarUrl"] = spectatorAvatarUrl;

                var spectatorGroupName = SignalRGroupNames.Game(gameId);
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
                    await SendRecentChatHistoryToCallerAsync(gameIdStr);
                    await BroadcastSystemChatMessageAsync(
                        gameId,
                        $"观战者 {spectatorDisplayName} 加入了观战");
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
            Context.Items["ParticipantType"] = ChatSenderType.Player;
            Context.Items["ParticipantId"] = playerIdStr;
            var fallbackDisplayName = playerGuid == authResult.Room.HostPlayerId ? "玩家1" : "玩家2";
            Context.Items["DisplayName"] = await ResolveParticipantDisplayNameAsync(playerGuid, fallbackDisplayName);

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

            string groupName = SignalRGroupNames.Game(gameId); // Consistent group naming convention
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            string playerGroupName = SignalRGroupNames.Player(gameIdStr, playerGuid);
            await Groups.AddToGroupAsync(Context.ConnectionId, playerGroupName);

            _logger.LogInformation("Client connected: ConnectionId={ConnectionId}, PlayerId={PlayerId}, GameId={GameId}, Added to Group={GroupName}",
                Context.ConnectionId, playerIdStr, gameId, groupName);

            var hadPendingDisconnect = CancelDisconnectTimeout(gameId, playerGuid);
            await NotifyPlayerConnectionStatusAsync(gameId, playerGuid, true, hadPendingDisconnect ? "Reconnect" : "Connected");
            _gameService.MarkPlayerReconnected(gameId.ToString(), playerGuid);

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

                    _logger.LogInformation("Sending initial game state to caller for Game {GameId}, Player {PlayerId}", gameId, playerIdStr);
                    await Clients.Caller.ReceiveGameState(currentState);
                    await SendRecentChatHistoryToCallerAsync(gameIdStr);
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
                string groupName = SignalRGroupNames.Game(gameId);
                try
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                    _logger.LogInformation("Removed ConnectionId {ConnectionId} from Group {GroupName}", Context.ConnectionId, groupName);

                    if (!string.IsNullOrEmpty(playerId) && Guid.TryParse(playerId, out var playerGuid))
                    {
                        var playerGroupName = SignalRGroupNames.Player(gameId.ToString("D"), playerGuid);
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, playerGroupName);
                        await NotifyPlayerConnectionStatusAsync(gameId, playerGuid, false, "Disconnect");
                        _gameService.MarkPlayerDisconnected(gameId.ToString(), playerGuid);
                        ScheduleDisconnectTimeout(gameId, playerGuid);
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

        private string GetDisconnectCacheKey(Guid gameId, Guid playerId)
        {
            return $"{DisconnectCachePrefix}_{gameId}_{playerId}";
        }

        private async Task NotifyPlayerConnectionStatusAsync(Guid gameId, Guid playerId, bool isConnected, string reason)
        {
            var groupName = SignalRGroupNames.Game(gameId);
            var dto = new PlayerConnectionStatusEventDto
            {
                PlayerId = playerId,
                IsConnected = isConnected,
                Reason = reason
            };
            await Clients.Group(groupName).PlayerConnectionStatusUpdated(dto);
        }

        private void ScheduleDisconnectTimeout(Guid gameId, Guid playerId)
        {
            var cacheKey = GetDisconnectCacheKey(gameId, playerId);
            var cts = new CancellationTokenSource();
            _disconnectTimeouts.AddOrUpdate(
                cacheKey,
                _ => cts,
                (_, existing) =>
                {
                    existing.Cancel();
                    return cts;
                });

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DisconnectGracePeriod, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    cts.Dispose();
                    return;
                }

                if (cts.IsCancellationRequested)
                {
                    cts.Dispose();
                    return;
                }

                if (!_disconnectTimeouts.TryRemove(cacheKey, out var storedCts) || !ReferenceEquals(storedCts, cts))
                {
                    cts.Dispose();
                    return;
                }

                storedCts.Dispose();

                try
                {
                    _logger.LogInformation("Disconnect timeout elapsed. Enqueuing disconnect action. GameId={GameId} PlayerId={PlayerId}", gameId, playerId);
                    var gameState = _gameService.GetGameState(gameId.ToString());
                    if (gameState == null || gameState.IsGameOver)
                    {
                        return;
                    }

                    var wrapper = new PlayerActionWrapper
                    {
                        ActionType = "Disconnect",
                        PlayerId = playerId,
                        ActionData = new { Reason = "Timeout" }
                    };

                    await _gameService.EnqueuePlayerActionAsync(gameId.ToString(), wrapper);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to process disconnect timeout: GameId={GameId} PlayerId={PlayerId}", gameId, playerId);
                }
            });
        }

        private bool CancelDisconnectTimeout(Guid gameId, Guid playerId)
        {
            var cacheKey = GetDisconnectCacheKey(gameId, playerId);
            if (_disconnectTimeouts.TryRemove(cacheKey, out var cts))
            {
                cts.Cancel();
                return true;
            }

            return false;
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
            var seenSeq = new HashSet<long>();
            var seenEventIds = new HashSet<Guid>();
            foreach (var record in records)
            {
                if (!seenSeq.Add(record.Seq) || !seenEventIds.Add(record.EventId))
                {
                    continue;
                }

                var replayDtos = await BuildReplayDtosAsync(gameId, record);
                foreach (var dto in replayDtos)
                {
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
            var seenSeq = new HashSet<long>();
            var seenEventIds = new HashSet<Guid>();
            while (fromSeq <= targetSeq)
            {
                var page = await _eventJournal.ReadRangeAsync(gameId, fromSeq, PageSize, Context.ConnectionAborted);
                if (page.Count == 0) break;

                foreach (var record in page)
                {
                    if (record.Seq > targetSeq) return;
                    if (!seenSeq.Add(record.Seq) || !seenEventIds.Add(record.EventId))
                    {
                        continue;
                    }

                    var replayDtos = await BuildReplayDtosAsync(gameId, record);
                    foreach (var dto in replayDtos)
                    {
                        await _clientMethodMapper.BroadcastEventToClientAsync(Context.ConnectionId, dto);
                    }
                }

                fromSeq = page[^1].Seq + 1;
            }
        }

        private async Task<IReadOnlyList<GameEventDto>> BuildReplayDtosAsync(string gameId, RecordedCommittedEventDto record)
        {
            var replayDtos = new List<GameEventDto>();
            foreach (var env in record.ClientDtos)
            {
                var dto = _recordedEventMapper.TryDeserializeClientDto(env);
                if (dto != null)
                {
                    replayDtos.Add(dto);
                }
            }

            if (replayDtos.Count > 0)
            {
                return replayDtos;
            }

            var projectedDtos = await TryProjectDtosFromRawAsync(gameId, record);
            if (projectedDtos.Count > 0)
            {
                return projectedDtos;
            }

            var fallbackLog = BuildFallbackReplayLogDto(record);
            if (fallbackLog != null)
            {
                replayDtos.Add(fallbackLog);
            }

            return replayDtos;
        }

        private async Task<IReadOnlyList<GameEventDto>> TryProjectDtosFromRawAsync(string gameId, RecordedCommittedEventDto record)
        {
            if (string.IsNullOrWhiteSpace(record.RawEventJson))
            {
                return Array.Empty<GameEventDto>();
            }

            RawEventEnvelopeDto? rawEvent;
            try
            {
                rawEvent = JsonSerializer.Deserialize<RawEventEnvelopeDto>(record.RawEventJson);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Replay raw event JSON parse failed for GameId={GameId} Seq={Seq}.", gameId, record.Seq);
                return Array.Empty<GameEventDto>();
            }

            if (rawEvent == null)
            {
                return Array.Empty<GameEventDto>();
            }

            var httpContext = Context.GetHttpContext();
            var requestServices = httpContext?.RequestServices;
            if (requestServices == null)
            {
                return Array.Empty<GameEventDto>();
            }

            var gameState = _gameService.GetGameState(gameId);
            if (gameState == null)
            {
                return Array.Empty<GameEventDto>();
            }

            var requestingPlayerId = ResolveReplayRequestingPlayerId(gameState, record);
            var rawEventRegistry = requestServices.GetRequiredService<IRawEventGameEventRegistry>();
            if (!rawEventRegistry.TryCreateGameEvent(rawEvent, gameState, requestingPlayerId, out var gameEvent, out var errorMessage) || gameEvent == null)
            {
                _logger.LogDebug(
                    "Replay raw projection skipped for GameId={GameId} Seq={Seq} EventType={EventType}. Reason={Reason}",
                    gameId,
                    record.Seq,
                    record.EventType,
                    errorMessage ?? "raw event type is not yet supported.");
                return Array.Empty<GameEventDto>();
            }

            var eventProcessor = requestServices.GetRequiredService<IEventProcessor>();
            var gameContextAccessor = requestServices.GetRequiredService<IGameContextAccessor>();

            try
            {
                // Apply-only replay projection: do not execute event Commit here.
                using var replayContext = new GameContext(
                    requestServices,
                    _mapper,
                    gameContextAccessor,
                    gameState,
                    requestingPlayerId,
                    Context.ConnectionAborted);

                var processorResult = await eventProcessor.ProcessEventAsync(gameEvent, replayContext);
                return processorResult.ResultingDtos;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Replay raw projection failed for GameId={GameId} Seq={Seq}.", gameId, record.Seq);
                return Array.Empty<GameEventDto>();
            }
        }

        private static Guid ResolveReplayRequestingPlayerId(GameState gameState, RecordedCommittedEventDto record)
        {
            if (record.SourceId.HasValue && record.SourceId.Value != Guid.Empty)
            {
                var sourceId = record.SourceId.Value;
                if (sourceId == gameState.Player1.PlayerId || sourceId == gameState.Player2.PlayerId)
                {
                    return sourceId;
                }

                var sourceCard = gameState.FindCardInstance(sourceId);
                if (sourceCard != null)
                {
                    return sourceCard.OwnerPlayerId;
                }
            }

            if (gameState.CurrentPlayerId.HasValue && gameState.CurrentPlayerId.Value != Guid.Empty)
            {
                return gameState.CurrentPlayerId.Value;
            }

            return gameState.Player1.PlayerId;
        }

        private static LogEventDto? BuildFallbackReplayLogDto(RecordedCommittedEventDto record)
        {
            if (string.IsNullOrWhiteSpace(record.Announcement))
            {
                return null;
            }

            return new LogEventDto(
                record.EventId,
                record.EventType,
                record.Announcement,
                record.SourceId ?? Guid.Empty);
        }

        private async Task SendRecentChatHistoryToCallerAsync(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return;
            }

            try
            {
                var messages = _gameChatService.GetRecentMessages(gameId, 100);
                var historyDto = new ChatHistoryEventDto
                {
                    GameId = gameId,
                    Messages = messages.ToList()
                };
                await Clients.Caller.ChatHistory(historyDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send chat history. GameId={GameId}, ConnectionId={ConnectionId}", gameId, Context.ConnectionId);
            }
        }

        private async Task BroadcastSystemChatMessageAsync(Guid gameId, string rawContent)
        {
            if (gameId == Guid.Empty)
            {
                return;
            }

            if (!_gameChatService.TryNormalizeContent(rawContent, out var normalizedContent, out _))
            {
                return;
            }

            var gameIdStr = gameId.ToString("D");
            var message = new ChatMessageEventDto
            {
                MessageId = Guid.NewGuid(),
                GameId = gameIdStr,
                SenderType = ChatSenderType.System,
                SenderId = "system",
                SenderName = "系统",
                SenderAvatarUrl = string.Empty,
                Content = normalizedContent,
                SentAtUtc = DateTime.UtcNow
            };

            _gameChatService.AppendMessage(message);

            try
            {
                await Clients.Group(SignalRGroupNames.Game(gameId)).ChatMessage(message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast system chat message. GameId={GameId}", gameIdStr);
            }
        }

        private bool TryResolveChatParticipantContext(
            out Guid gameId,
            out ChatSenderType senderType,
            out string senderId,
            out string senderName,
            out string senderAvatarUrl)
        {
            gameId = Guid.Empty;
            senderType = ChatSenderType.System;
            senderId = Context.ConnectionId;
            senderName = "系统";
            senderAvatarUrl = string.Empty;

            string? gameIdStr = null;
            if (Context.Items.TryGetValue("GameId", out var gameIdItem) && gameIdItem is string gameIdFromItems)
            {
                gameIdStr = gameIdFromItems;
            }
            else
            {
                gameIdStr = Context.GetHttpContext()?.Request.Query["gameId"].ToString();
            }

            if (!Guid.TryParse(gameIdStr, out gameId))
            {
                return false;
            }

            if (Context.Items.TryGetValue("ParticipantType", out var participantTypeItem))
            {
                if (participantTypeItem is ChatSenderType enumValue)
                {
                    senderType = enumValue;
                }
                else if (participantTypeItem is string strValue && Enum.TryParse<ChatSenderType>(strValue, true, out var parsedType))
                {
                    senderType = parsedType;
                }
            }
            else if (Context.Items.ContainsKey("PlayerId"))
            {
                senderType = ChatSenderType.Player;
            }
            else
            {
                senderType = ChatSenderType.Spectator;
            }

            if (Context.Items.TryGetValue("ParticipantId", out var participantIdItem) &&
                participantIdItem is string participantId &&
                !string.IsNullOrWhiteSpace(participantId))
            {
                senderId = participantId;
            }
            else if (Context.Items.TryGetValue("PlayerId", out var playerIdItem) &&
                     playerIdItem is string playerId &&
                     !string.IsNullOrWhiteSpace(playerId))
            {
                senderId = playerId;
            }

            if (Context.Items.TryGetValue("DisplayName", out var displayNameItem) &&
                displayNameItem is string displayName &&
                !string.IsNullOrWhiteSpace(displayName))
            {
                senderName = displayName;
            }
            else
            {
                senderName = senderType switch
                {
                    ChatSenderType.Player => "玩家",
                    ChatSenderType.Spectator => "观战者",
                    _ => "系统"
                };
            }

            if (Context.Items.TryGetValue("AvatarUrl", out var avatarItem) &&
                avatarItem is string avatar &&
                !string.IsNullOrWhiteSpace(avatar))
            {
                senderAvatarUrl = avatar.Trim();
            }

            return true;
        }

        private static string NormalizeChatDisplayName(string? rawValue, string fallbackValue)
        {
            var value = string.IsNullOrWhiteSpace(rawValue)
                ? fallbackValue
                : rawValue.Trim();

            if (value.Length > 24)
            {
                value = value.Substring(0, 24);
            }

            return string.IsNullOrWhiteSpace(value) ? fallbackValue : value;
        }

        private static string BuildSpectatorFallbackDisplayName()
        {
            var code = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
            return $"游客{code}";
        }

        private static string NormalizeChatParticipantId(string? rawValue, string fallbackValue)
        {
            var value = string.IsNullOrWhiteSpace(rawValue)
                ? fallbackValue
                : rawValue.Trim();

            if (value.Length > 64)
            {
                value = value.Substring(0, 64);
            }

            return string.IsNullOrWhiteSpace(value) ? fallbackValue : value;
        }

        private static string NormalizeChatAvatarUrl(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return string.Empty;
            }

            var value = rawValue.Trim();
            if (value.Length > 512)
            {
                return string.Empty;
            }

            if (value.StartsWith("/", StringComparison.Ordinal)
                || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            return string.Empty;
        }

        private async Task<string> ResolveParticipantDisplayNameAsync(Guid playerId, string fallbackDisplayName)
        {
            try
            {
                var serviceProvider = Context.GetHttpContext()?.RequestServices;
                var db = serviceProvider?.GetService(typeof(ApplicationDbContext)) as ApplicationDbContext;
                if (db == null)
                {
                    return fallbackDisplayName;
                }

                var username = await db.Users
                    .AsNoTracking()
                    .Where(user => user.Id == playerId)
                    .Select(user => user.Username)
                    .FirstOrDefaultAsync();

                return string.IsNullOrWhiteSpace(username)
                    ? fallbackDisplayName
                    : username.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve participant display name. PlayerId={PlayerId}", playerId);
                return fallbackDisplayName;
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

            // Spectator connections may only carry GameId without PlayerId.
            if (Context.Items.TryGetValue("GameId", out var gameOnlyIdItem) &&
                gameOnlyIdItem is string gameOnlyIdStr &&
                Guid.TryParse(gameOnlyIdStr, out var gameIdOnly))
            {
                return Task.FromResult<(Guid, string?)>((gameIdOnly, null));
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

            if (Guid.TryParse(gameIdStr, out var gameIdWithoutPlayer))
            {
                return Task.FromResult<(Guid, string?)>((gameIdWithoutPlayer, null));
            }

            _logger.LogWarning("Could not extract valid GameId/PlayerId from QueryString for ConnectionId {ConnectionId}. gameId='{GameIdStr}', playerId='{PlayerIdFromQuery}'",
                Context.ConnectionId, gameIdStr, playerIdFromQuery);
            return Task.FromResult((Guid.Empty, (string?)null));
        }
    }
}
