using ShadowverseBeyond.Server.Domain.Abilities.Targets;
// Server/Services/GameService.cs
//
// 相关文档:
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 回合管理系统: 文档/05-游戏系统/03-回合管理系统.md
// - 战斗系统: 文档/05-游戏系统/02-战斗系统.md
// - 游戏事件系统: 文档/05-游戏系统/04-游戏事件系统.md
//
using AutoMapper; // Added for IMapper
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Hubs; // GameHub
using ShadowverseBeyond.Server.Hubs.ClientInterfaces; // IGameClient
using ShadowverseBeyond.Server.Dtos.SignalR;        // DTOs (Restored for GameEventDto base class)
// Removed: using ShadowverseBeyond.Server.Dtos.SignalR;        // DTOs (Caused Ambiguity)
using ShadowverseBeyond.Server.Dtos.Enums; // Add using for CardType enum
using ShadowverseBeyond.Server.Domain.Events.ActionRequests; // Added for request events
using ShadowverseBeyond.Server.Models; // Added for PlayerActionWrapper, ActionType enum
using System.Text.Json;
// Removed duplicate: using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Exceptions; // GameLogicException
using Shadowverse.Domain.Interfaces; // Corrected namespace for ICard (and potentially others)
using ShadowverseBeyond.Server.Dtos;             // Add for GameStateDto etc.
using ShadowverseBeyond.Server.Domain.Entities; // Corrected namespace for CardDefinition etc.
using System;
using System.Collections.Concurrent; // For ConcurrentDictionary
using System.Collections.Generic; // Add for List<>
using System.Linq; // For helper methods potentially
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Dtos.SignalR.Events; // Base Event DTO
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
// Updated Namespace
// using ShadowverseBeyond.Server.Models; // Already included above
using ShadowverseBeyond.Server.Services.Abstractions; // Added for IClientMethodMapper
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // Added for IConditionFactory
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // Added for IEffectExecutor
using ShadowverseBeyond.Server.Domain.Abilities.Common; // Added for ValueConfig
using ShadowverseBeyond.Server.Domain.Events; // Added for IEventProcessor and PlayCardEvent
using ShadowverseBeyond.Server.Domain.Factories; // Added for ICardFactory

using ShadowverseBeyond.Server.Domain.Abilities; // Added for AbilityManager
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events.Services; // Added for keyword constants

namespace ShadowverseBeyond.Server.Services
{
    public class GameService : IGameService
    {
        // Dictionary to track the player whose action chain is currently being processed per game - Declaration moved/exists elsewhere
        // Removed duplicate declaration from line 57
        private readonly IHubContext<GameHub> _hubContext; // Changed type to non-generic client
        private readonly ILogger<GameService> _logger;
        private readonly IMapper _mapper; // Added IMapper field

        // Removed EventBroadcastHandler delegate and _eventHandlers dictionary

        private readonly IAiService _aiService; // Added for AI logic
        private readonly Abstractions.IGameDataProvider _gameDataProvider; // Inject GameDataProvider for cached data access
        private readonly Abstractions.IEffectQueueService _effectQueueService; // 【架构重构】注入效果队列服务

        // Central Game State Storage - Stores GameState domain objects
        private readonly ConcurrentDictionary<string, GameState> _gameStates = new(); // Use Domain Entity
        // Lock objects per game to prevent concurrent modification of a single game's state
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _gameCreationSemaphores = new();
        // --- Queue and Processing Loop Management ---
        private readonly ConcurrentDictionary<string, ConcurrentQueue<PlayerActionWrapper>> _playerActionQueues = new();
        // 【架构重构】移除传统事件队列，改为立即事件广播
        // private readonly ConcurrentDictionary<string, ConcurrentQueue<IGameEvent>> _eventQueues = new();
        private readonly ConcurrentDictionary<string, string?> _currentlyProcessingPlayerActionId = new(); // Tracks the PlayerId whose action chain is being processed
        private readonly ConcurrentDictionary<string, Task> _gameProcessingLoops = new(); // Stores the running Task for each game's loop
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _gameLoopCancellationTokens = new(); // Stores cancellation tokens for each loop

        // Dictionary to track pending AI requests to prevent flooding
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, DateTime>> _pendingAiRequests = new();
        // Flag to prevent re-entrant EndTurn processing for the same game - REMOVED by Roo
        // private readonly ConcurrentDictionary<string, bool> _isProcessingEndTurn = new();

        // --- Game Constants ---
        // Use centralized GameConstants instead of local constants
        private const int MaxBoardSize = GameConstants.Field.MaxFieldSize; // Maximum number of followers/amulets per side

        /// <summary>
        /// Enqueues a player action received from the Hub and ensures the processing loop for the game is running.
        /// </summary>
        /// <param name="gameId">The ID of the game.</param>
        /// <param name="actionWrapper">The player action to enqueue.</param>
        public void EnqueuePlayerAction(string gameId, PlayerActionWrapper actionWrapper)
        {
            if (string.IsNullOrEmpty(gameId) || actionWrapper == null)
            {
                _logger.LogWarning("[EnqueuePlayerActionAsync] Invalid arguments: gameId or actionWrapper is null/empty.");
                return; // Or throw ArgumentNullException
            }

            _logger.LogDebug("[EnqueuePlayerActionAsync:{GameId}] Received action {ActionType} from Player {PlayerId}.", gameId, actionWrapper.ActionType, actionWrapper.PlayerId);

            if (actionWrapper.ActionType == "PlayCard")
            {
                _logger.LogWarning("🔥 [PLAYCARD_DEBUG] EnqueuePlayerActionAsync: Received PlayCard action for Player {PlayerId}", actionWrapper.PlayerId);

                // Check if this is an AI player and if there are already pending requests
                var gameState = GetGameState(gameId);
                if (gameState != null && gameState.Player2.IsAI && actionWrapper.PlayerId == gameState.Player2.PlayerId)
                {
                    var pendingRequests = _pendingAiRequests.GetOrAdd(gameId, _ => new ConcurrentDictionary<Guid, DateTime>());
                    var currentTime = DateTime.UtcNow;

                    // Clean up old pending requests (older than 5 seconds)
                    var expiredRequests = pendingRequests.Where(kvp => currentTime - kvp.Value > TimeSpan.FromSeconds(5)).ToList();
                    foreach (var expired in expiredRequests)
                    {
                        pendingRequests.TryRemove(expired.Key, out _);
                    }

                    // Check if there's already a pending request for this AI player
                    if (pendingRequests.ContainsKey(actionWrapper.PlayerId))
                    {
                        _logger.LogWarning("🔥 [PLAYCARD_DEBUG] EnqueuePlayerActionAsync: AI Player {PlayerId} already has a pending PlayCard request. Rejecting duplicate.", actionWrapper.PlayerId);
                        return; // Reject the duplicate request
                    }

                    // Add this request to pending
                    pendingRequests.TryAdd(actionWrapper.PlayerId, currentTime);
                    _logger.LogWarning("🔥 [PLAYCARD_DEBUG] EnqueuePlayerActionAsync: Added AI Player {PlayerId} to pending requests", actionWrapper.PlayerId);
                }
            }

            // 1. Get or add the action queue for the game
            var actionQueue = _playerActionQueues.GetOrAdd(gameId, _ => new ConcurrentQueue<PlayerActionWrapper>());

            // 2. Enqueue the action
            actionQueue.Enqueue(actionWrapper);
            _logger.LogTrace("[EnqueuePlayerActionAsync:{GameId}] Action {ActionType} enqueued. Queue size: {QueueSize}", gameId, actionWrapper.ActionType, actionQueue.Count);

            if (actionWrapper.ActionType == "PlayCard")
            {
                _logger.LogWarning("🔥 [PLAYCARD_DEBUG] EnqueuePlayerActionAsync: PlayCard action enqueued successfully for Player {PlayerId}, Queue size: {QueueSize}", actionWrapper.PlayerId, actionQueue.Count);
            }


            // 3. Ensure the processing loop is running for this game
            // Check if a loop task exists and if it's still active
            if (!_gameProcessingLoops.TryGetValue(gameId, out var existingLoop) || existingLoop.IsCompleted || existingLoop.IsFaulted || existingLoop.IsCanceled)
            {
                if (actionWrapper.ActionType == "PlayCard")
                {
                    _logger.LogWarning("🔥 [PLAYCARD_DEBUG] EnqueuePlayerActionAsync: Starting/restarting processing loop for game {GameId}", gameId);
                }

                // Attempt to start a new loop if none is active
                // Use a lock specific to loop creation to prevent race conditions starting multiple loops
                object loopLock = _gameLoopCreationLocks.GetOrAdd(gameId, _ => new object());
                lock (loopLock)
                {
                    // Double-check inside the lock
                    if (!_gameProcessingLoops.TryGetValue(gameId, out existingLoop) || existingLoop.IsCompleted || existingLoop.IsFaulted || existingLoop.IsCanceled)
                    {
                        _logger.LogInformation("[EnqueuePlayerActionAsync:{GameId}] No active processing loop found or loop terminated. Starting a new one.", gameId);

                        // Create and store a new cancellation token source
                        var cts = new CancellationTokenSource();
                        _gameLoopCancellationTokens.AddOrUpdate(gameId, cts, (_, oldCts) => { oldCts.Dispose(); return cts; }); // Dispose old one if replacing

                        // Start the processing loop as a background task
                        var processingTask = Task.Run(() => GameProcessingLoopAsync(gameId, cts.Token), cts.Token);

                        // Store the new loop task, replacing any old/completed one
                        _gameProcessingLoops.AddOrUpdate(gameId, processingTask, (_, oldTask) => processingTask);

                        _logger.LogInformation("[EnqueuePlayerActionAsync:{GameId}] New processing loop started.", gameId);
                    }
                    else
                    {
                        _logger.LogDebug("[EnqueuePlayerActionAsync:{GameId}] Processing loop was already started by another thread.", gameId);
                    }
                }
            }
            else
            {
                _logger.LogTrace("[EnqueuePlayerActionAsync:{GameId}] Processing loop is already active.", gameId);
            }

            // Enqueuing itself is fast, loop runs in background
        }

        // Need a lock object dictionary for loop creation synchronization
        private readonly ConcurrentDictionary<string, object> _gameLoopCreationLocks = new();
        // Lock objects per game for general game state operations
        private readonly ConcurrentDictionary<string, object> _gameLocks = new();
        // Define the fixed 20-card test deck list
        // Updated to use integer IDs
        private static readonly List<int> _fixedTestDeckCardIds = new List<int>
        {
            1001, 1001, 1001, // Goblin x3
            1002, 1002, 1002, // Fighter x3
            1005, 1005, 1005, // Shield Guardian x3
            1006, 1006, 1006, // Bellringer Angel x3 (Assuming F006 is Bellringer Angel with ID 1006)
            2001, 2001, 2001, // Insight x3
            2002, 2002, 2002, // Angelic Snipe x3
            1003,                 // Knight x1
            1004                  // Heavy Goblin x1 (Assuming F004 is Heavy Goblin with ID 1004)
        };

        // Duplicate declaration removed by Roo.
        // Update constructor signature
        private readonly IClientMethodMapper _clientMethodMapper; // Added field declaration
        // Inject IClientMethodMapper, IBuffManager, and IMapper
        // Removed the duplicate field declaration added previously.
        // The original field declaration should exist elsewhere in the class.

        private readonly IEventProcessor _eventProcessor;
        private readonly IEffectExecutor _effectExecutor;
        private readonly IAbilityTriggerService _abilityTriggerService;
        private readonly ICardFactory _cardFactory;
        private readonly AbilityManager _abilityManager;
        private readonly IEventHistoryService _eventHistoryService;
        private readonly IConditionEvaluator _conditionEvaluator;
        private readonly TargetSelectionService _targetSelectionService;
        private readonly IServiceProvider _serviceProvider;

        public GameService(
            IHubContext<GameHub> hubContext,
            ILogger<GameService> logger,
            IMapper mapper,
            IAiService aiService,
            IClientMethodMapper clientMethodMapper,
            Abstractions.IGameDataProvider gameDataProvider,
            Abstractions.IEffectQueueService effectQueueService,
            IEventProcessor eventProcessor,
            IEffectExecutor effectExecutor,
            IAbilityTriggerService abilityTriggerService,
            ICardFactory cardFactory,
            AbilityManager abilityManager,
            Domain.Events.Services.IEventHistoryService eventHistoryService,
            IConditionEvaluator conditionEvaluator,
            TargetSelectionService targetSelectionService,
            IServiceProvider serviceProvider)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _clientMethodMapper = clientMethodMapper ?? throw new ArgumentNullException(nameof(clientMethodMapper));
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
            _effectQueueService = effectQueueService ?? throw new ArgumentNullException(nameof(effectQueueService));
            _eventProcessor = eventProcessor ?? throw new ArgumentNullException(nameof(eventProcessor));
            _effectExecutor = effectExecutor ?? throw new ArgumentNullException(nameof(effectExecutor));
            _abilityTriggerService = abilityTriggerService ?? throw new ArgumentNullException(nameof(abilityTriggerService));
            _cardFactory = cardFactory ?? throw new ArgumentNullException(nameof(cardFactory));
            _abilityManager = abilityManager ?? throw new ArgumentNullException(nameof(abilityManager));
            _eventHistoryService = eventHistoryService ?? throw new ArgumentNullException(nameof(eventHistoryService));
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
            _targetSelectionService = targetSelectionService ?? throw new ArgumentNullException(nameof(targetSelectionService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }


        /// <summary>
        /// Ensures that the processing loop for the specified game is running.
        /// If the loop is not running or has completed/faulted, it starts a new one.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        private void EnsureProcessingLoopRunningAsync(string gameId)
        {
            // Check if a loop task already exists for this game
            if (_gameProcessingLoops.TryGetValue(gameId, out var existingTask) && !existingTask.IsCompleted && !existingTask.IsFaulted && !existingTask.IsCanceled)
            {
                // Loop is already running fine
                _logger.LogTrace("[GameService:{GameId}] Processing loop already running.", gameId);
                return;
            }

            // If the task exists but is completed/faulted/canceled, remove it before starting a new one
            if (existingTask != null)
            {
                _logger.LogWarning("[GameService:{GameId}] Previous processing loop found in state: {Status}. Removing before starting new loop.", gameId, existingTask.Status);
                _gameProcessingLoops.TryRemove(gameId, out _);
                // Also remove the corresponding cancellation token source if it exists
                if (_gameLoopCancellationTokens.TryRemove(gameId, out var oldCts))
                {
                    oldCts.Dispose(); // Dispose the old token source
                }
            }

            _logger.LogInformation("[GameService:{GameId}] Starting new processing loop.", gameId);

            // Create a new cancellation token source for this loop
            var cts = new CancellationTokenSource();
            if (!_gameLoopCancellationTokens.TryAdd(gameId, cts))
            {
                // Handle potential race condition if another thread added it just now
                _logger.LogWarning("[GameService:{GameId}] Failed to add CancellationTokenSource, possibly due to race condition. Loop might already be starting.", gameId);
                cts.Dispose(); // Dispose the newly created cts as it won't be used
                // Re-check if loop is running now
                if (_gameProcessingLoops.TryGetValue(gameId, out existingTask) && !existingTask.IsCompleted)
                {
                    return; // Another thread started it successfully
                }
                else
                {
                    // If still not running, log an error or retry? For now, log error.
                    _logger.LogError("[GameService:{GameId}] Race condition detected, but loop still not running after check. Loop start failed.", gameId);
                    return; // Avoid starting the loop without a valid CTS entry
                }
            }

            // Create and store the new processing loop task
            var processingTask = Task.Run(() => GameProcessingLoopAsync(gameId, cts.Token));

            // Try to add the task to the dictionary
            if (!_gameProcessingLoops.TryAdd(gameId, processingTask))
            {
                // Handle potential race condition if another thread added the task just now
                _logger.LogWarning("[GameService:{GameId}] Failed to add processing task, possibly due to race condition. Loop might already be running.", gameId);
                // Clean up the cancellation token source we added
                if (_gameLoopCancellationTokens.TryRemove(gameId, out var addedCts))
                {
                    addedCts.Cancel(); // Cancel the token associated with the task we didn't store
                    addedCts.Dispose();
                }
                // The task processingTask will run but won't be tracked by us, which is not ideal.
                // Consider more robust locking if this becomes an issue.
            }
            else
            {
                _logger.LogInformation("[GameService:{GameId}] Processing loop task created and stored.", gameId);
                // Optional: Add continuation to handle task completion/faulting for cleanup
                processingTask.ContinueWith(t =>
                {
                    _logger.LogWarning(t.Exception, "[GameService:{GameId}] Processing loop task completed with status: {Status}.", gameId, t.Status);
                    // Clean up resources when the loop finishes or faults
                    _gameProcessingLoops.TryRemove(gameId, out _);
                    if (_gameLoopCancellationTokens.TryRemove(gameId, out var finalCts))
                    {
                        finalCts.Dispose();
                    }
                }, TaskContinuationOptions.ExecuteSynchronously); // Execute synchronously to ensure cleanup happens promptly
            }
        }

        // 【架构重构】游戏处理循环 - 使用效果队列架构
        private async Task GameProcessingLoopAsync(string gameId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[GameProcessingLoop:{GameId}] Loop started with effect queue architecture.", gameId);
            var actionQueue = _playerActionQueues.GetOrAdd(gameId, _ => new ConcurrentQueue<PlayerActionWrapper>());
            // 【架构重构】移除传统事件队列，改为立即事件处理
            // var eventQueue = _eventQueues.GetOrAdd(gameId, _ => new ConcurrentQueue<IGameEvent>());

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    bool processedItem = false; // Flag to check if any work was done in this iteration

                    // --- 0. 【架构重构】优先处理效果队列 ---
                    if (_effectQueueService.HasPendingEffects(gameId))
                    {
                        try
                        {
                            _logger.LogDebug("[GameProcessingLoop:{GameId}] Processing effect queue with {EffectCount} pending effects",
                                gameId, _effectQueueService.GetPendingEffectCount(gameId));

                            // 【架构重构】实际处理效果队列中的请求
                            await ProcessEffectQueueForGameAsync(gameId);
                            processedItem = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[GameProcessingLoop:{GameId}] Error processing effect queue", gameId);
                        }
                    }

                    // 【架构重构】移除传统事件队列处理，改为立即事件处理
                    // 事件现在在效果执行时立即处理，不再需要单独的事件队列循环

                    // --- 2. Process Action Queue (简化处理) ---
                    // 【架构重构】移除事件队列依赖，直接处理动作队列
                    if (actionQueue.TryDequeue(out var actionWrapper))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _logger.LogDebug("[GameProcessingLoop:{GameId}] Dequeued Player Action: {ActionType} for Player {PlayerId}", gameId, actionWrapper.ActionType, actionWrapper.PlayerId);

                        if (actionWrapper.ActionType == "PlayCard")
                        {
                            _logger.LogWarning("🔥 [PLAYCARD_DEBUG] GameProcessingLoop: Dequeued PlayCard action for Player {PlayerId}", actionWrapper.PlayerId);
                        }

                        try
                        {
                            // Execute the initial step of the player action and get the resulting event
                            var initialEvent = await ExecutePlayerActionInitialStepAsync(gameId, actionWrapper);

                            // 【架构重构】立即处理生成的初始事件，而不是排队
                            if (initialEvent != null)
                            {
                                _logger.LogDebug("[GameProcessingLoop:{GameId}] Processing initial event {EventType} from Action {ActionType} immediately",
                                    gameId, initialEvent.GetType().Name, actionWrapper.ActionType);

                                // 设置当前处理的玩家ID
                                _currentlyProcessingPlayerActionId.AddOrUpdate(gameId, actionWrapper.PlayerId.ToString(), (_, existingPlayerId) => actionWrapper.PlayerId.ToString());
                                _logger.LogInformation("[GameProcessingLoop:{GameId}] Started processing action chain for Player {PlayerId}.", gameId, actionWrapper.PlayerId);

                                // 创建GameContext用于事件处理
                                GameState? gameState = null;
                                // No lock needed here for read, ConcurrentDictionary is safe
                                _gameStates.TryGetValue(gameId, out gameState);

                                if (gameState != null)
                                {
                                    var gameContext = new GameContext(
                                        gameState,
                                        actionWrapper.PlayerId,
                                        _mapper,
                                        _serviceProvider, // IServiceProvider is now available in GameContext constructor
                                        triggeringEvent: null,
                                        sourceCard: null
                                    );

                                    // 立即处理所有初始事件
                                    await ProcessGeneratedEventsImmediatelyAsync(gameId, new List<IGameEvent> { initialEvent }, gameContext);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("[GameProcessingLoop:{GameId}] Action {ActionType} for Player {PlayerId} did not generate any initial domain events.", gameId, actionWrapper.ActionType, actionWrapper.PlayerId);
                                // 如果没有事件，动作立即完成
                                _currentlyProcessingPlayerActionId.TryRemove(new KeyValuePair<string, string?>(gameId, actionWrapper.PlayerId.ToString()));
                            }
                            processedItem = true;
                        }
                        catch (OperationCanceledException) { throw; } // Re-throw cancellation
                        catch (GameLogicException glex) // Catch specific game logic errors from initial step
                        {
                            _logger.LogWarning(glex, "[GameProcessingLoop:{GameId}] Game logic error executing initial step for action {ActionType} by Player {PlayerId}. Action rejected.", gameId, actionWrapper.ActionType, actionWrapper.PlayerId);
                            // TODO: Implement sending error back to the specific player
                            // await SendErrorToPlayerAsync(actionWrapper.PlayerId, glex.Message);
                            _logger.LogError("[GameProcessingLoop:{GameId}] Placeholder: Need to implement SendErrorToPlayerAsync for Player {PlayerId}, Error: {ErrorMessage}", gameId, actionWrapper.PlayerId, glex.Message);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[GameProcessingLoop:{GameId}] Error executing initial step for action {ActionType} by Player {PlayerId}.", gameId, actionWrapper.ActionType, actionWrapper.PlayerId);
                            // Handle unexpected errors during action execution
                        }
                    } // End of action queue processing

                    // --- 3. Handle Action Completion (Hand Playability Update) ---
                    // 【架构重构】简化动作完成检测：只检查动作队列是否为空
                    if (actionQueue.IsEmpty && _currentlyProcessingPlayerActionId.TryGetValue(gameId, out var completedPlayerId) && completedPlayerId != null)
                    {
                        _logger.LogInformation("[GameProcessingLoop:{GameId}] Action chain for Player {PlayerId} completed (Action queue empty).", gameId, completedPlayerId);
                        // Clear the processing flag *before* starting the update
                        // Use TryUpdate with comparison for atomicity
                        if (_currentlyProcessingPlayerActionId.TryUpdate(gameId, null, completedPlayerId))
                        {
                            try
                            {
                                // Clear pending AI request if this was an AI action
                                if (_pendingAiRequests.TryGetValue(gameId, out var pendingRequests))
                                {
                                    if (Guid.TryParse(completedPlayerId, out var playerGuid))
                                    {
                                        if (pendingRequests.TryRemove(playerGuid, out _))
                                        {
                                            _logger.LogWarning("🔥 [PLAYCARD_DEBUG] Action completion: Cleared pending AI request for Player {PlayerId}", completedPlayerId);
                                        }
                                    }
                                }

                                // 【修复】直接调用可玩性更新，作为系统维护操作
                                // 这是客户端同步操作，不是游戏逻辑效果，应该直接集成到处理流程中
                                await UpdateAndBroadcastHandPlayabilityAsync(gameId, Guid.Parse(completedPlayerId));

                                // 【新增】同时为当前回合玩家更新可玩性（如果不同的话）
                                // 确保回合开始时玩家也有最新的可玩性信息
                                if (_gameStates.TryGetValue(gameId, out var gameState) && gameState != null)
                                {
                                    var currentPlayerId = gameState.CurrentPlayerId;
                                    if (currentPlayerId.HasValue && currentPlayerId.Value != Guid.Parse(completedPlayerId))
                                    {
                                        _logger.LogDebug("[GameProcessingLoop:{GameId}] Also updating hand playability for current turn player {CurrentPlayerId}.", gameId, currentPlayerId.Value);
                                        await UpdateAndBroadcastHandPlayabilityAsync(gameId, currentPlayerId.Value);
                                    }
                                }

                                processedItem = true;
                            }
                            catch (OperationCanceledException) { throw; } // Re-throw cancellation
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[GameProcessingLoop:{GameId}] Error during hand playability update for Player {PlayerId}.", gameId, completedPlayerId);
                                // Log error but continue the loop
                            }
                        }
                        else
                        {
                            // This might happen if another thread cleared the flag just before TryUpdate, or if the value wasn't the expected completedPlayerId.
                            _logger.LogWarning("[GameProcessingLoop:{GameId}] Failed to clear processing player ID flag for Player {PlayerId} (expected value comparison failed or flag already cleared).", gameId, completedPlayerId);
                        }
                    }

                    // --- 4. Wait if Idle ---
                    if (!processedItem)
                    {
                        // If no items were processed in this iteration, wait a short period
                        // to avoid busy-waiting when queues are empty.
                        try
                        {
                            // Use Task.Delay with the cancellation token
                            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken); // Adjust delay as needed
                        }
                        catch (OperationCanceledException)
                        {
                            // Expected when cancellation is requested during delay
                            _logger.LogInformation("[GameProcessingLoop:{GameId}] Delay canceled.", gameId);
                            break; // Exit the loop immediately
                        }
                    }
                } // End of while (!cancellationToken.IsCancellationRequested)
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[GameProcessingLoop:{GameId}] Loop canceled.", gameId);
                // Perform any necessary cleanup specific to cancellation
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "[GameProcessingLoop:{GameId}] Unhandled exception in processing loop. Loop terminating.", gameId);
                // Consider marking the game state as errored or notifying admins
            }
            finally
            {
                _logger.LogInformation("[GameProcessingLoop:{GameId}] Loop finished.", gameId);
                // Ensure cleanup happens even if the loop exits unexpectedly
                // Remove the loop task and cancellation token source from dictionaries
                _gameProcessingLoops.TryRemove(gameId, out _);
                if (_gameLoopCancellationTokens.TryRemove(gameId, out var cts))
                {
                    cts.Dispose();
                }
                // Clear the currently processing player ID on exit
                _currentlyProcessingPlayerActionId.TryRemove(gameId, out _);
                // 【架构重构】只检查动作队列的清理
                if (actionQueue != null && !actionQueue.IsEmpty)
                {
                    _logger.LogWarning("[GameProcessingLoop:{GameId}] Loop terminated with {Count} items remaining in action queue.", gameId, actionQueue.Count);
                }
                // 事件队列已移除，不再需要检查

                // Clear registered cards for the game state
                if (_gameStates.TryGetValue(gameId, out var gameStateToClear) && gameStateToClear != null)
                {
                    gameStateToClear.ClearAllRegisteredCards();
                    _logger.LogInformation("[GameProcessingLoop:{GameId}] Cleared all registered card instances for game.", gameId);
                }
            }
        } // End of GameProcessingLoopAsync

        /// <summary>
        /// 【架构重构】处理指定游戏的效果队列
        /// 从效果队列中取出请求并通过IEffectExecutor执行
        /// </summary>
        private async Task ProcessEffectQueueForGameAsync(string gameId)
        {
            _logger.LogDebug("🔧 [效果队列架构] Processing effect queue for game {GameId}", gameId);

            // 获取游戏状态和锁
            GameState? gameState = null;
            if (!_gameStates.TryGetValue(gameId, out gameState) || gameState == null)
            {
                _logger.LogError("🔧 [效果队列架构] GameState not found for game {GameId}, cannot process effect queue", gameId);
                return;
            }

            var eventHistoryService = _eventHistoryService;

            // 处理效果队列中的所有待处理效果
            while (_effectQueueService.HasPendingEffects(gameId))
            {
                try
                {
                    // 获取下一个效果请求
                    var effectRequest = _effectQueueService.GetNextEffectRequest(gameId);
                    if (effectRequest == null)
                    {
                        _logger.LogDebug("🔧 [效果队列架构] No more effect requests in queue for game {GameId}", gameId);
                        break;
                    }

                    _logger.LogInformation("🔧 [效果队列架构] Processing effect request: {EffectType} for player {PlayerId} in game {GameId}",
                        effectRequest.EffectConfig?.Type ?? "unknown", effectRequest.RequestingPlayerId, gameId);

                    // 【BUG-019调试】特别记录destroy类型的效果处理
                    if (effectRequest.EffectConfig?.Type?.ToLower() == "destroy")
                    {
                        _logger.LogWarning("🔧 [BUG-019_DEBUG] Processing DESTROY effect request for player {PlayerId} with {TargetCount} targets",
                            effectRequest.RequestingPlayerId, effectRequest.SelectedTargets?.Count ?? 0);
                    }

                    // 获取 TriggeringEvent
                    IGameEvent? triggeringEvent = null;
                    if (effectRequest.TriggeredEventId != Guid.Empty)
                    {
                        triggeringEvent = await _eventHistoryService.GetEventByIdAsync(gameId, effectRequest.TriggeredEventId);
                        if (triggeringEvent == null)
                        {
                            _logger.LogWarning("🔧 [ProcessEffectQueue] TriggeringEvent with ID {TriggeredEventId} not found for game {GameId}.",
                                effectRequest.TriggeredEventId, gameId);
                        }
                    }

                    // 获取 CurrentAbility (父能力)
                    IAbility? currentAbility = null;
                    if (effectRequest.ParentAbilityId.HasValue && effectRequest.ParentAbilityId.Value != Guid.Empty)
                    {
                        currentAbility = _abilityManager.GetAbilityByRuntimeId(effectRequest.ParentAbilityId.Value);
                        if (currentAbility == null)
                        {
                            _logger.LogWarning("🔧 [ProcessEffectQueue] CurrentAbility (ParentAbility) with ID {ParentAbilityId} not found.",
                                effectRequest.ParentAbilityId.Value);
                        }
                    }

                    // 获取 SourceCard (能力的源卡牌)
                    CardInstance? sourceCardOfAbility = null;
                    if (currentAbility?.SourceCard != null && currentAbility.SourceCard.InstanceId != Guid.Empty)
                    {
                        sourceCardOfAbility = gameState.FindCardInstanceById(currentAbility.SourceCard.InstanceId);
                    }
                    else if (effectRequest.SourceCardId.HasValue && effectRequest.SourceCardId.Value != Guid.Empty)
                    {
                        sourceCardOfAbility = gameState.FindCardInstanceById(effectRequest.SourceCardId.Value);
                        if (sourceCardOfAbility == null) // Log if explicitly provided SourceCardId from effectRequest is not found
                        {
                            _logger.LogWarning("🔧 [ProcessEffectQueue] SourceCard with ID {SourceCardId} from effectRequest not found.", effectRequest.SourceCardId.Value);
                        }
                    }

                    if (sourceCardOfAbility == null)
                    {
                        string debugMsg = "🔧 [ProcessEffectQueue] Could not determine SourceCard. ";
                        if (currentAbility != null)
                        {
                            debugMsg += $"Ability: {currentAbility.AbilityTag} (RuntimeId: {currentAbility.RuntimeId}), AbilitySourceCardId: {currentAbility.SourceCard?.InstanceId}. ";
                        }
                        else
                        {
                            debugMsg += "No currentAbility. ";
                        }
                        if (effectRequest.SourceCardId.HasValue)
                        {
                            debugMsg += $"EffectRequestSourceCardId: {effectRequest.SourceCardId.Value}.";
                        }
                        else
                        {
                            debugMsg += "No SourceCardId in effectRequest.";
                        }
                        _logger.LogWarning(debugMsg);
                    }
                    // 【架构重构】直接使用EffectRequest中的EffectConfig
                    // 这个EffectConfig已经配置为使用现有的EffectHandler（turnstart, turnend, mulligan等）
                    var effectConfig = effectRequest.EffectConfig;

                    // 创建GameContext

                    var gameContext = new GameContext(
                        gameState,
                        effectRequest.RequestingPlayerId,
                        _mapper,
                        _serviceProvider, // IServiceProvider
                        triggeringEvent,
                        sourceCard: sourceCardOfAbility,
                        currentAbility: currentAbility as Ability
                    );

                    // 【架构改进】将请求中携带的目标更新到GameContext
                    if (effectRequest.SelectedTargets != null && effectRequest.SelectedTargets.Any())
                    {
                        gameContext.ClearTemporaryState();
                        foreach (var target in effectRequest.SelectedTargets)
                        {
                            gameContext.AddSelectedTarget(target);
                        }
                        _logger.LogDebug("🔧 [效果队列架构] Updated GameContext with {TargetCount} targets from EffectRequest.", effectRequest.SelectedTargets.Count);
                    }

                    // 【架构重构】执行效果并获取完整结果，包括EffectRequests
                    if (effectConfig == null)
                    {
                        _logger.LogWarning("EffectConfig is null, cannot execute effect.");
                        continue;
                    }
                    var effectResult = await _effectExecutor.ExecuteEffectAsync(effectConfig, gameContext);

                    _logger.LogInformation("🔧 [效果队列架构] Effect execution completed for {EffectType}, generated {EventCount} events.",
                        effectRequest.EffectConfig?.Type ?? "unknown", effectResult.GeneratedEvents?.Count ?? 0);

                    // --- 核心修改：处理 ChainedAbilities ---
                    if (effectRequest.ChainedAbilities.Any())
                    {
                        _logger.LogInformation("🔧 [能力链] Effect {EffectType} completed and has {Count} chained abilities. Processing them...",
                                               effectRequest.EffectConfig?.Type ?? "unknown", effectRequest.ChainedAbilities.Count);

                        foreach (var chainedAbility in effectRequest.ChainedAbilities)
                        {
                            // The context passed here is the one from *after* the parent effect has executed,
                            // ensuring the chained ability operates on the most up-to-date game state.

                            // Delegate the entire execution process to the centralized service
                            var newEffects = await _abilityTriggerService.ProcessSingleAbilityAsync(chainedAbility, gameContext);

                            if (newEffects.Any())
                            {
                                _logger.LogDebug("🔧 [能力链] Enqueuing {Count} effects from chained ability {AbilityName}", newEffects.Count, chainedAbility.Name);
                                foreach (var newEffect in newEffects)
                                {
                                    await _effectQueueService.EnqueueEffectRequestAsync(gameId, newEffect);
                                }
                            }
                        }
                    }
                    // --- 修改结束 ---

                    // 【架构重构】立即处理生成的事件，而不是排队
                    if (effectResult.GeneratedEvents != null && effectResult.GeneratedEvents.Any())
                    {
                        await ProcessGeneratedEventsImmediatelyAsync(gameId, effectResult.GeneratedEvents, gameContext);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "🔧 [效果队列架构] Error processing effect from queue for game {GameId}", gameId);
                    break; // 避免无限循环
                }
            }
        }



        /// <summary>
        /// 【架构重构】立即处理生成的事件，支持事件驱动的效果链
        /// 替代传统的事件队列，实现立即事件广播和能力触发
        /// </summary>
        private async Task ProcessGeneratedEventsImmediatelyAsync(string gameId, List<IGameEvent> generatedEvents, GameContext gameContext)
        {
            _logger.LogDebug("🔧 [立即事件处理] Processing {EventCount} generated events immediately for game {GameId}",
                generatedEvents.Count, gameId);

            var eventProcessor = _eventProcessor;

            foreach (var gameEvent in generatedEvents)
            {
                try
                {
                    _logger.LogWarning("🔧 [攻击调试] Processing event: {EventType} (ID: {EventId})",
                        gameEvent.GetType().Name, gameEvent.EventId);

                    // 立即处理事件，包括能力触发和DTO转换
                    var processorResult = await eventProcessor.ProcessEventAsync(gameEvent, gameContext);

                    _logger.LogWarning("🔧 [攻击调试] EventProcessor result: ResultingDtos={DtoCount}, NewDomainEvents={NewEventCount}",
                        processorResult.ResultingDtos?.Count ?? 0, processorResult.NewDomainEvents?.Count ?? 0);

                    // 立即广播生成的DTO
                    if (processorResult.ResultingDtos != null && processorResult.ResultingDtos.Any())
                    {
                        _logger.LogWarning("🔧 [攻击调试] Broadcasting {DtoCount} DTOs for event {EventType}: {DtoTypes}",
                            processorResult.ResultingDtos.Count, gameEvent.GetType().Name,
                            string.Join(", ", processorResult.ResultingDtos.Select(d => d.Type)));

                        // 【伤害事件调试】特别记录DamageDealt事件的广播
                        var damageEvents = processorResult.ResultingDtos.Where(dto => dto.Type == "DamageDealt").ToList();
                        if (damageEvents.Any())
                        {
                            _logger.LogWarning("🔧 [伤害事件调试] Found {DamageEventCount} DamageDealt events to broadcast for {EventType}",
                                damageEvents.Count, gameEvent.GetType().Name);
                        }

                        await BroadcastEventsAsync(gameId, processorResult.ResultingDtos);

                        // 【修复】在TurnStartEvent广播后立即推送手牌可玩性信息
                        if (gameEvent.EventType == EventType.TurnStart)
                        {
                            var startingPlayerId = (gameEvent.Source?.Type == TargetType.Player) ? gameEvent.Source?.Id : null;
                            if (startingPlayerId.HasValue)
                            {
                                _logger.LogInformation("🎯 [TURN_START_PLAYABILITY] Pushing hand playability for Player {PlayerId} after turn start", startingPlayerId.Value);
                                try
                                {
                                    await UpdateAndBroadcastHandPlayabilityAsync(gameId, startingPlayerId.Value);

                                    // 【AI TURN TRIGGER】 Check if the player whose turn is starting is an AI
                                    var playerState = gameContext.CurrentGameState.GetPlayerState(startingPlayerId.Value);
                                    if (playerState != null && playerState.IsAI)
                                    {
                                        _logger.LogInformation("🤖 [AI TURN TRIGGER] Turn started for AI Player {PlayerId}. Triggering AI turn.", startingPlayerId.Value);
                                        // Do not await this. The AI will enqueue its own actions in the background.
                                        _ = _aiService.RequestAiActionAsync(gameId, startingPlayerId.Value);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "🎯 [TURN_START_PLAYABILITY] Error updating hand playability or triggering AI for Player {PlayerId} after turn start", startingPlayerId.Value);
                                }
                            }
                        }
                    }

                    // 【架构重构】处理生成的EffectRequests - 将它们入队到效果队列
                    if (processorResult.EffectRequests != null && processorResult.EffectRequests.Any())
                    {
                        _logger.LogInformation("🔧 [立即事件处理] Event {EventType} generated {EffectRequestCount} effect requests, enqueuing them",
                            gameEvent.GetType().Name, processorResult.EffectRequests.Count);

                        foreach (var effectRequest in processorResult.EffectRequests)
                        {
                            await _effectQueueService.EnqueueEffectRequestAsync(gameId, effectRequest);
                            _logger.LogDebug("🔧 [立即事件处理] Enqueued EffectRequest: {EffectType} for player {PlayerId}",
                                effectRequest.EffectConfig?.Type ?? "unknown", effectRequest.RequestingPlayerId);
                        }

                        // 【BUG-019修复】移除立即处理调用，避免重复事件处理和广播
                        // 效果请求将在主游戏循环中被正常处理，确保正确的事件流
                        _logger.LogInformation("🔧 [BUG-019修复] Enqueued {EffectRequestCount} effect requests for processing in main game loop",
                            processorResult.EffectRequests.Count);
                        // await ProcessEffectQueueForGameAsync(gameId); // 移除此调用以避免重复处理
                    }

                    // 递归处理新生成的事件（支持事件驱动的效果链）
                    if (processorResult.NewDomainEvents != null && processorResult.NewDomainEvents.Any())
                    {
                        _logger.LogDebug("🔧 [立即事件处理] Event {EventType} generated {NewEventCount} additional events, processing recursively",
                            gameEvent.GetType().Name, processorResult.NewDomainEvents.Count);

                        // 递归处理新事件，支持事件链
                        await ProcessGeneratedEventsImmediatelyAsync(gameId, processorResult.NewDomainEvents, gameContext);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "🔧 [立即事件处理] Error processing event {EventType} for game {GameId}",
                        gameEvent.GetType().Name, gameId);
                }
            }

            _logger.LogDebug("🔧 [立即事件处理] Completed immediate processing of all events for game {GameId}", gameId);
        }

        /// <summary>
        /// Executes the initial validation and event generation step for a player action dequeued from the action queue.
        /// This method performs basic checks and creates the initial domain event(s) to be processed by the event loop.
        /// It does NOT modify core game state directly (e.g., PP deduction); that is handled by the event processor.
        /// </summary>
        /// <param name="gameId">The game ID.</param>
        /// <param name="actionWrapper">The player action wrapper.</param>
        /// <param name="context">The game action context to add generated domain events to.</param>
        /// <exception cref="GameLogicException">Thrown if the action is invalid based on initial checks.</exception>
        private async Task<IGameEvent?> ExecutePlayerActionInitialStepAsync(string gameId, PlayerActionWrapper actionWrapper)
        {
            // --- 1. Basic Validation (Requires reading GameState) ---
            GameState? gameState = null;
            PlayerState? playerState = null;

            if (!_gameStates.TryGetValue(gameId, out gameState))
            {
                throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Game not found: {gameId}");
            }
            playerState = gameState.GetPlayerState(actionWrapper.PlayerId);
            if (playerState == null)
            {
                throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Player state not found for {actionWrapper.PlayerId} in game {gameId}.");
            }

            // Phase-specific validations
            if (actionWrapper.ActionType == "SubmitMulligan")
            {
                // Mulligan validation: Only check phase, not turn (mulligan is simultaneous for both players)
                if (gameState.Phase != GamePhase.Mulligan)
                {
                    throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Mulligan only allowed during Mulligan phase (Current: {gameState.Phase}).");
                }
                // No turn validation for mulligan - both players can submit simultaneously
                _logger.LogDebug("[InitialStep:{ActionType}] Mulligan validation passed for Player {PlayerId} during Mulligan phase.",
                    actionWrapper.ActionType, actionWrapper.PlayerId);
            }
            else
            {
                // Non-mulligan actions: Check both turn and phase
                if (gameState.CurrentPlayerId != actionWrapper.PlayerId)
                {
                    throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] It's not player {actionWrapper.PlayerId}'s turn (Current: {gameState.CurrentPlayerId}).");
                }
                // Allow other actions only during Main phase
                if (gameState.Phase != GamePhase.Main)
                {
                    throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Action only allowed during Main phase (Current: {gameState.Phase}).");
                }
            }

            // --- 2. Create Initial Domain Event ---
            IGameEvent initialEvent;
            switch (actionWrapper.ActionType) // Use the string property
            {
                // Corrected: Use string literals for case comparison
                case "PlayCard":
                    _logger.LogWarning("🔥 [PLAYCARD_DEBUG] ExecutePlayerActionInitialStepAsync: Processing PlayCard action for Player {PlayerId}", actionWrapper.PlayerId);
                    if (actionWrapper.ActionData is PlayCardActionDto playDto)
                    {
                        _logger.LogWarning("🔥 [PLAYCARD_DEBUG] ExecutePlayerActionInitialStepAsync: Creating PlayCardRequestedEvent - Card: {CardUid}, Target: {TargetUid}",
                            playDto.CardUid, playDto.TargetUid);
                        initialEvent = new PlayCardRequestedEvent(actionWrapper.PlayerId, playDto);
                    }
                    else
                    {
                        _logger.LogError("🔥 [PLAYCARD_DEBUG] ExecutePlayerActionInitialStepAsync: Invalid ActionData type for PlayCard: {ActualType}",
                            actionWrapper.ActionData?.GetType().Name ?? "null");
                        throw new ArgumentException($"Invalid ActionData type for {actionWrapper.ActionType}");
                    }
                    break;

                case "PerformAttack":
                    if (actionWrapper.ActionData is AttackActionDto attackDto)
                    {
                        initialEvent = new AttackRequestedEvent(actionWrapper.PlayerId, attackDto);
                    }
                    else throw new ArgumentException($"Invalid ActionData type for {actionWrapper.ActionType}");
                    break;

                case "EndTurn":
                    initialEvent = new EndTurnRequestedEvent(actionWrapper.PlayerId);
                    break;

                case "PerformEvolve":
                    if (actionWrapper.ActionData is EvolveActionDto evolveDto)
                    {
                        initialEvent = new EvolveRequestedEvent(actionWrapper.PlayerId, evolveDto);
                    }
                    else throw new ArgumentException($"Invalid ActionData type for {actionWrapper.ActionType}");
                    break;

                case "SubmitMulligan":
                    // --- Mulligan Debug Logging ---
                    var actionDataTypeName = actionWrapper.ActionData?.GetType().FullName ?? "null";
                    var actionDataJson = "N/A";
                    try
                    {
                        if (actionWrapper.ActionData != null)
                        {
                            actionDataJson = JsonSerializer.Serialize(actionWrapper.ActionData);
                        }
                    }
                    catch { actionDataJson = "Serialization failed."; }
                    _logger.LogWarning("[MULLIGAN_DEBUG] Received SubmitMulligan action. PlayerId: {PlayerId}, ActionData Type: {DataType}, ActionData JSON: {DataJson}",
                        actionWrapper.PlayerId, actionDataTypeName, actionDataJson);
                    // --- End Debug Logging ---

                    // Handle different ActionData formats for SubmitMulligan
                    if (actionWrapper.ActionData is List<Guid> cardUids)
                    {
                        // Direct List<Guid> format
                        initialEvent = new MulliganSubmittedEvent(actionWrapper.PlayerId, cardUids);
                    }
                    // Handle anonymous object with CardUidsToReplace property (from GameHub)
                    else if (actionWrapper.ActionData != null)
                    {
                        var actionDataType = actionWrapper.ActionData.GetType();
                        var cardUidsProperty = actionDataType.GetProperty("CardUidsToReplace");

                        if (cardUidsProperty != null && cardUidsProperty.GetValue(actionWrapper.ActionData) is List<Guid> extractedCardUids)
                        {
                            initialEvent = new MulliganSubmittedEvent(actionWrapper.PlayerId, extractedCardUids);
                        }
                        // Handle JSON element format
                        else if (actionWrapper.ActionData is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                        {
                            try
                            {
                                var deserializedUids = jsonElement.Deserialize<List<Guid>>();
                                if (deserializedUids == null) throw new ArgumentException("Deserialized card UIDs list is null.");
                                initialEvent = new MulliganSubmittedEvent(actionWrapper.PlayerId, deserializedUids);
                            }
                            catch (JsonException jex)
                            {
                                throw new ArgumentException($"Failed to deserialize ActionData for {actionWrapper.ActionType}: {jex.Message}", jex);
                            }
                        }
                        else
                        {
                            throw new ArgumentException($"Invalid ActionData format for {actionWrapper.ActionType}. Expected List<Guid>, object with CardUidsToReplace property, or JSON array. Received: {actionDataType.Name}");
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"ActionData is null for {actionWrapper.ActionType}. Expected List<Guid> or object with CardUidsToReplace property.");
                    }
                    break;

                default:
                    _logger.LogWarning("[ExecutePlayerActionInitialStepAsync] Unknown action type: {ActionType}", actionWrapper.ActionType);
                    throw new NotSupportedException($"Action type '{actionWrapper.ActionType}' is not supported.");
            }

            // --- 3. Return the Event ---
            _logger.LogDebug("[ExecutePlayerActionInitialStepAsync:{GameId}] Created initial domain event {EventType} for Action {ActionType}",
                gameId, initialEvent.GetType().Name, actionWrapper.ActionType);

            await Task.CompletedTask;
            return initialEvent;
        }

        /// <summary>
        /// Retrieves the current game state domain entity for the specified game ID.
        /// <summary>
        /// Retrieves the current game state domain entity for the specified game ID.
        /// Does not create a new game if the state does not exist.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <returns>The GameState domain entity if found, otherwise null.</returns>
        public GameState? GetGameState(string gameId)
        {
            _logger.LogDebug("[GameService] Attempting to get game state for Game {GameId}.", gameId);
            _gameStates.TryGetValue(gameId, out var existingState);
            return existingState;
        }
        // --- End Added GetGameStateAsync Implementation ---

        // --- Refactored to GetOrCreateGameStateAsync ---
        /// <summary>
        /// Gets the current game state domain entity for the given game ID, or creates a new one if it doesn't exist.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <param name="requestingPlayerId">The ID of the player requesting the game state (used for game creation).</param>
        /// <returns>The GameState domain entity for the game, or null if creation failed unexpectedly.</returns>
        public GameState? GetOrCreateGameState(string gameId, Guid? requestingPlayerId = null)
        {
            if (_gameStates.TryGetValue(gameId, out var existingState))
            {
                return existingState;
            }

            // Creation logic
            try
            {
                Guid player1Id = requestingPlayerId ?? Guid.NewGuid();
                Guid player2Id = Guid.NewGuid();

                var allCardDefinitions = _gameDataProvider.GetAllCardDefinitions();
                if (!allCardDefinitions.Any())
                {
                    _logger.LogError("No card definitions found. Cannot create game.");
                    return null;
                }

                bool player1IsFirst = new Random().Next(0, 2) == 0;
                var player1State = new PlayerState(player1Id) { IsFirst = player1IsFirst };
                var player2State = new PlayerState(player2Id) { IsFirst = !player1IsFirst, IsAI = true };

                var newGameState = new GameState(gameId, player1State, player2State, player1IsFirst ? player1Id : player2Id);

                foreach (var cardId in _fixedTestDeckCardIds)
                {
                    player1State.Deck.Add(_cardFactory.CreateAndRegisterNewCard(newGameState, cardId, player1Id, CardZone.Deck));
                    player2State.Deck.Add(_cardFactory.CreateAndRegisterNewCard(newGameState, cardId, player2Id, CardZone.Deck));
                }

                ShuffleDeck(player1State.Deck);
                ShuffleDeck(player2State.Deck);

                player1State.Hand.AddRange(DrawInitialHand(player1State.Deck, 3));
                player2State.Hand.AddRange(DrawInitialHand(player2State.Deck, 3));
                foreach (var card in player1State.Hand) card.CurrentZone = CardZone.Hand;
                foreach (var card in player2State.Hand) card.CurrentZone = CardZone.Hand;

                const int leaderCardId = GameConstants.Cards.LeaderCardId;
                player1State.Leader = _cardFactory.CreateAndRegisterNewCard(newGameState, leaderCardId, player1Id, CardZone.Leader);
                player2State.Leader = _cardFactory.CreateAndRegisterNewCard(newGameState, leaderCardId, player2Id, CardZone.Leader);

                if (_gameStates.TryAdd(gameId, newGameState))
                {
                    _ = Task.Run(() => TriggerAiMulliganIfNeededAsync(gameId, newGameState));
                    return newGameState;
                }

                // If TryAdd fails, it means another thread created the game state.
                // Return the state created by the other thread.
                return _gameStates.TryGetValue(gameId, out var racingState) ? racingState : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating game state for Game {GameId}", gameId);
                return null;
            }
        }

        /// <summary>
        /// Performs basic spell target validation (simplified version for immediate use)
        /// TODO: Replace with proper ability system integration
        /// </summary>
        /// <param name="targetEntity">The target entity to validate</param>
        /// <param name="gameState">Current game state</param>
        /// <returns>True if the target is valid, false otherwise</returns>
        private bool IsValidSpellTargetBasic(ICard targetEntity, GameState gameState)
        {
            try
            {
                // Basic validation: Check if target exists
                if (targetEntity == null)
                {
                    return false;
                }

                // Allow targeting players (leaders)
                if (targetEntity is PlayerState)
                {
                    return true;
                }

                // Allow targeting cards on the field
                if (targetEntity is CardInstance targetCard)
                {
                    return targetCard.CurrentZone == CardZone.Field;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Error in basic spell target validation", gameState.GameId);
                return false; // Fail safe
            }
        }

        // Removed the placeholder CreateDeckFromDefinitionAsync method as it's replaced by inline logic
        /// <summary&gt;
        /// Contains the core logic for checking if a card is playable, separated from the initial data fetching.
        /// This allows it to be called for proactive hand updates.
        /// </summary&gt;
        private async Task<PlayabilityResultDto> CheckPlayabilityCoreLogicAsync(
            GameState gameState,
            PlayerState playerState,
            CardInstance cardInHand,
            CardDefinition cardDefinition,
            string gameId, // For logging
            Guid playerId, // For logging
            Guid cardInstanceId // For logging
        )
        {
            var result = new PlayabilityResultDto();
            _logger.LogDebug("[CheckPlayabilityCore:{GameId}] Starting core logic check for Card {CardName} ({CardInstanceId}).", gameId, cardDefinition.Name, cardInstanceId);

            try
            {
                // --- Basic Checks --- (Moved from CheckCardPlayabilityAsync)
                // Cost Check
                if (playerState.CurrentPlayPoints < cardInHand.CurrentCost)
                {
                    result.IsPlayable = false;
                    result.FailureReason = $"费用不足 (需要 {cardInHand.CurrentCost}, 当前 {playerState.CurrentPlayPoints})。";
                    return result;
                }

                // Field Space Check (if Follower)
                if (cardDefinition.Type == CardType.Follower)
                {
                    if (playerState.Field.Count >= MaxBoardSize)
                    {
                        result.IsPlayable = false;
                        result.FailureReason = $"场地已满 (最大 {MaxBoardSize} 个)，无法召唤随从。";
                        return result;
                    }
                }

                // --- PrePlay Condition Checks --- (Moved from CheckCardPlayabilityAsync)
                _logger.LogDebug("[CheckPlayabilityCore:{GameId}] Starting PrePlay condition checks for Card {CardName} ({CardInstanceId}).", gameId, cardDefinition.Name, cardInstanceId);
                var abilityConfigs = _gameDataProvider.GetAbilityConfigs(cardDefinition.Id);
                var playTriggerConfigs = abilityConfigs
                    .Where(ac => ac.Trigger.EventTypes.Contains(EventType.PlayCard))
                    .ToList();

                var gameContext = new GameContext(gameState, playerId, _mapper, _serviceProvider, null, cardInHand); // IServiceProvider is not needed here

                foreach (var config in playTriggerConfigs)
                {
                    foreach (var condition in config.PreConditions)
                    {
                        if (!await _conditionEvaluator.EvaluateAsync(condition, gameContext))
                        {
                            result.IsPlayable = false;
                            result.FailureReason = $"打出前置条件未满足: {condition.Name ?? condition.Expression}";
                            _logger.LogInformation("[CheckPlayabilityCore:{GameId}] PrePlay condition failed for Card {CardInstanceId}: {ConditionExpression}", gameId, cardInstanceId, condition.Expression);
                            return result;
                        }
                    }
                }
                _logger.LogDebug("[CheckPlayabilityCore:{GameId}] PrePlay conditions met for Card {CardInstanceId}.", gameId, cardInstanceId);

                // --- Effect Target Info Check (for UI) --- (Moved from CheckCardPlayabilityAsync)
                _logger.LogDebug("[CheckPlayabilityCore:{GameId}] Starting Effect Target info checks for Card {CardInstanceId}.", gameId, cardInstanceId);
                result.RequiresTargetSelection = false;
                result.HasValidTargets = false;
                var validTargetIdSet = new HashSet<Guid>();

                if (playTriggerConfigs.Any()) // playTriggerConfigs is available here now
                {

                    foreach (var config in playTriggerConfigs)
                    {
                        foreach (var effect in config.Effects)
                        {
                            bool isTargetRequired = effect.Target?.PlayerChoiceOptions?.IsRequired == true;

                            if (isTargetRequired)
                            {
                                result.RequiresTargetSelection = true;
                                _logger.LogTrace("[CheckPlayabilityCore:{GameId}] Effect requires target (IsRequired=true). Checking valid targets for Card {CardInstanceId}.", gameId, cardInstanceId);

                                try
                                {
                                    if (effect.Target == null) continue;
                                    var validTargets = await _targetSelectionService.SelectTargetsAsync(effect.Target, gameContext);

                                    if (validTargets.Any())
                                    {
                                        result.HasValidTargets = true;
                                        validTargetIdSet = new HashSet<Guid>(validTargets.Select(t => t.Id));
                                        _logger.LogTrace("[CheckPlayabilityCore:{GameId}] Found {Count} valid targets for an effect of Card {CardInstanceId}.", gameId, validTargets.Count, cardInstanceId);
                                    }
                                    else
                                    {
                                        _logger.LogTrace("[CheckPlayabilityCore:{GameId}] No valid targets found for an effect of Card {CardInstanceId}.", gameId, cardInstanceId);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "[CheckPlayabilityCore:{GameId}] Error during SelectTargetsAsync for Card {CardInstanceId}.", gameId, cardInstanceId);
                                }
                            }
                        }
                    }
                }
                result.ValidTargetIds = validTargetIdSet.ToList();
                _logger.LogDebug("[CheckPlayabilityCore:{GameId}] Effect Target info checks complete for Card {CardInstanceId}. RequiresTarget: {Requires}, HasValid: {HasValid}, ValidCount: {Count}",
                    gameId, cardInstanceId, result.RequiresTargetSelection, result.HasValidTargets, result.ValidTargetIds.Count);

                // --- All Checks Passed ---
                // If we reached here without returning early, the card is playable based on cost, space, conditions, and target availability (if required)
                result.IsPlayable = true;
                _logger.LogInformation("[CheckPlayabilityCore:{GameId}] Card {CardInstanceId} ({CardName}) is playable for Player {PlayerId}.", gameId, cardInstanceId, cardDefinition.Name, playerId);

            }
            catch (Exception ex) // Catch specific exceptions if needed later
            {
                _logger.LogError(ex, "[CheckPlayabilityCore:{GameId}] Unexpected error during core logic check for Player {PlayerId}, Card {CardInstanceId}.", gameId, playerId, cardInstanceId);
                result.IsPlayable = false;
                result.FailureReason = "检查核心逻辑时发生意外错误。";
                // Consider re-throwing or specific handling based on exception type
            }

            return result;
        }
        // Made private - core logic to be extracted for proactive updates
        private async Task<PlayabilityResultDto> CheckCardPlayabilityAsync(string gameId, Guid playerId, Guid cardInstanceId)
        {
            var result = new PlayabilityResultDto();
            _logger.LogInformation("[GameService:{GameId}] Checking playability for Player {PlayerId}, CardInstanceId {CardInstanceId}.",
                gameId, playerId, cardInstanceId);

            try
            {
                // --- 1. Get Game State and Basic Entities ---
                // Use GetGameStateAsync which returns the domain entity
                var gameState = GetGameState(gameId);
                if (gameState == null)
                {
                    result.IsPlayable = false;
                    result.FailureReason = "游戏不存在。";
                    _logger.LogWarning("[CheckPlayability:{GameId}] Game not found.", gameId);
                    return result;
                }

                var playerState = gameState.GetPlayerState(playerId);
                if (playerState == null)
                {
                    result.IsPlayable = false;
                    result.FailureReason = "玩家状态未找到。";
                    _logger.LogWarning("[CheckPlayability:{GameId}] Player state not found for Player {PlayerId}.", gameId, playerId);
                    return result;
                }

                // Find card in hand using the int InstanceId
                // Assuming CardInstance.InstanceId is now int
                // Fix: Convert cardInstanceId (int) to string for comparison with InstanceId (string)
                var cardInstanceIdString = cardInstanceId;
                var card = playerState.Hand.FirstOrDefault(c => c.InstanceId == cardInstanceIdString);
                if (card == null)
                {
                    result.IsPlayable = false;
                    result.FailureReason = "手牌中找不到该卡牌。";
                    _logger.LogWarning("[CheckPlayability:{GameId}] CardInstanceId {CardInstanceId} not found in Player {PlayerId}'s hand.", gameId, cardInstanceId, playerId);
                    return result;
                }

                // Fetch Card Definition from cache
                var cardDefinition = _gameDataProvider.GetCardDefinition(card.DefinitionId);
                if (cardDefinition == null)
                {
                    result.IsPlayable = false;
                    result.FailureReason = "无法加载卡牌定义。";
                    _logger.LogWarning("[CheckPlayability:{GameId}] Card definition not found for DefinitionId {DefinitionId}.", gameId, card.DefinitionId);
                    return result;
                }

                // --- 2. Call Core Logic ---
                // Basic checks (Cost, Field Space) moved to CheckPlayabilityCoreLogicAsync
                result = await CheckPlayabilityCoreLogicAsync(gameState, playerState, card, cardDefinition, gameId, playerId, cardInstanceId);

                // If core logic determined not playable, return immediately
                if (!result.IsPlayable)
                {
                    return result;
                }

                // PrePlay and Target checks are now handled within CheckPlayabilityCoreLogicAsync

                // --- 5. All Checks Passed --- (This part is now handled in CoreLogic)
                // result.IsPlayable = true; // Set within CoreLogic
                // _logger.LogInformation("[CheckPlayability:{GameId}] Card {CardInstanceId} ({CardName}) is playable for Player {PlayerId}.", gameId, cardInstanceId, cardDefinition.Name, playerId);

            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning(ex, "[CheckPlayability:{GameId}] Game logic error checking playability for Player {PlayerId}, Card {CardInstanceId}.", gameId, playerId, cardInstanceId);
                result.IsPlayable = false;
                result.FailureReason = ex.Message; // Use exception message as reason
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CheckPlayability:{GameId}] Unexpected error checking playability for Player {PlayerId}, Card {CardInstanceId}.", gameId, playerId, cardInstanceId);
                result.IsPlayable = false;
                result.FailureReason = "检查可玩性时发生意外错误。";
            }

            return result;
        }

        /// &lt;summary&gt;
        /// Checks the playability of all cards in a player's hand and broadcasts the results
        /// to the specific player client after their action chain completes.
        /// &lt;/summary&gt;
        /// &lt;param name="gameId"&gt;The ID of the game.&lt;/param&gt;
        /// &lt;param name="playerId"&gt;The ID of the player whose hand needs updating.&lt;/param&gt;
        private async Task UpdateAndBroadcastHandPlayabilityAsync(string gameId, Guid playerId)
        {
            _logger.LogInformation("[GameService:{GameId}] Updating hand playability for Player {PlayerId} after action completion.", gameId, playerId);
            var playabilityResults = new Dictionary<Guid, PlayabilityResultDto>(); // Corrected HTML entities

            try
            {
                var gameState = GetGameState(gameId);
                if (gameState == null)
                {
                    _logger.LogWarning("[UpdateHandPlayability:{GameId}] Game not found.", gameId);
                    return; // Cannot proceed without game state
                }

                var playerState = gameState.GetPlayerState(playerId);
                if (playerState == null)
                {
                    _logger.LogWarning("[UpdateHandPlayability:{GameId}] Player state not found for Player {PlayerId}.", gameId, playerId);
                    return; // Cannot proceed without player state
                }

                // Iterate through each card in the player's hand
                foreach (var cardInHand in playerState.Hand.ToList()) // Use ToList() to avoid modification issues if needed
                {
                    var cardDefinition = _gameDataProvider.GetCardDefinition(cardInHand.DefinitionId);
                    if (cardDefinition == null)
                    {
                        _logger.LogWarning("[UpdateHandPlayability:{GameId}] Card definition {DefinitionId} not found for card {InstanceId} in Player {PlayerId}'s hand. Skipping.",
                            gameId, cardInHand.DefinitionId, cardInHand.InstanceId, playerId);
                        // Add a default non-playable result for this card?
                        playabilityResults[cardInHand.InstanceId] = new PlayabilityResultDto { IsPlayable = false, FailureReason = "卡牌定义丢失" };
                        continue;
                    }

                    // Call the core logic checker
                    var singleResult = await CheckPlayabilityCoreLogicAsync(gameState, playerState, cardInHand, cardDefinition, gameId, playerId, cardInHand.InstanceId);
                    playabilityResults[cardInHand.InstanceId] = singleResult;
                }

                // Create the DTO
                var updateDto = new HandPlayabilityUpdateDto(playerId) // Pass playerId to constructor
                {
                    PlayabilityResults = playabilityResults
                    // Timestamp and Type are handled by the base GameEventDto constructor
                };

                // Broadcast the update specifically to the player
                // Ensure _hubContext is injected and available
                if (_hubContext != null)
                {
                    // 【修复】使用Group而不是User，因为前端使用查询字符串连接，没有设置用户身份验证
                    // SignalR的Clients.User()需要用户身份验证，但我们使用的是查询字符串传递playerId
                    // 所以改用Clients.Group()向整个游戏组广播，然后前端根据PlayerId过滤
                    string groupName = $"Game_{gameId}";
                    var clientProxy = _hubContext.Clients.Group(groupName);
                    await clientProxy.SendAsync("ReceiveHandPlayabilityUpdate", updateDto);
                    _logger.LogInformation("[GameService:{GameId}] Sent hand playability update to Game Group {GroupName} for Player {PlayerId} with {Count} results.", gameId, groupName, playerId, playabilityResults.Count);
                }
                else
                {
                    _logger.LogError("[GameService:{GameId}] _hubContext is null. Cannot send hand playability update to Player {PlayerId}.", gameId, playerId);
                }

            }
            catch (Exception ex)
            {
                // Log the error, but typically don't re-throw as this shouldn't block the main game loop
                _logger.LogError(ex, "[GameService:{GameId}] Unexpected error during UpdateAndBroadcastHandPlayabilityAsync for Player {PlayerId}.", gameId, playerId);
                // Optionally send an error to the specific client?
                // await _hubContext.Clients.User(playerId).SendAsync(ClientMethodNames.ReceiveError, "Error updating hand playability.");
            }
        }
        // --- Existing Methods Below ---
        // --- Helper Methods for End Turn Logic ---

        /// <summary>
        /// Creates a new CardInstance domain entity from a CardDefinition.
        /// </summary>
        /// <param name="cardDef">The static card definition.</param>
        /// <param name="ownerPlayerId">The ID of the player who owns this instance.</param>
        /// <param name="initialZone">The initial zone for the card instance.</param>
        /// <returns>A new CardInstance object.</returns>
        [Obsolete("Use ICardFactory.CreateAndRegisterNewCardAsync instead for consistent registration.")]
        private CardInstance CreateCardInstanceFromDefinition(CardDefinition cardDef, Guid ownerPlayerId, CardZone initialZone)
        {
            if (cardDef == null)
            {
                // Log or throw, depending on how critical this is. Throwing for now.
                _logger.LogError("Attempted to create CardInstance from null CardDefinition for Player {OwnerPlayerId}.", ownerPlayerId);
                throw new ArgumentNullException(nameof(cardDef), "CardDefinition cannot be null when creating a CardInstance.");
            }

            var instance = new CardInstance(
                instanceId: Guid.NewGuid(), // Generate unique Guid ID
                definitionId: cardDef.Id,
                ownerPlayerId: ownerPlayerId
            )
            {
                CardType = cardDef.Type,
                CurrentZone = initialZone,
                BaseCost = cardDef.Cost,            // 【修复】设置基础费用
                CurrentCost = cardDef.Cost,
                BaseAttack = cardDef.Attack ?? 0,   // 【修复】设置基础攻击力
                CurrentAttack = cardDef.Attack ?? 0, // Initialize current stats from base
                BaseHealth = cardDef.Health ?? 0,   // 【修复】设置基础生命值
                CurrentHealth = cardDef.Health ?? 0,
                MaxHealth = cardDef.Health ?? 0,   // Initialize max health from base
                Name = cardDef.Name,                // 【修复】设置卡牌名称
                Description = cardDef.Description ?? "", // 【修复】设置卡牌描述
                Rarity = cardDef.Rarity ?? "",      // 【修复】设置稀有度
                Class = cardDef.Class ?? "",        // 【修复】设置职业
                Tribe = cardDef.Tribe,              // 【修复】设置种族
                IsEvolved = false,                  // Starts unevolved
                CanAttack = false,                  // Default CanAttack state (adjust based on zone/keywords if needed)
                                                    // Removed CanEvolve - evolution eligibility is determined by game rules
                AttacksThisTurn = 0,
                Countdown = null,                   // Initialize Countdown (relevant for amulets)
                ZoneIndex = null                // Initialize FieldPosition
                                                // AbilityState is initialized internally
            };

            // Set base keywords
            instance.SetBaseKeywords(cardDef.Keywords ?? Enumerable.Empty<string>());

            // Specific initialization based on type or zone could go here
            if (initialZone == CardZone.Field)
            {
                // Example: Followers entering field might check for Rush/Storm
                if (instance.IsFollower)
                {
                    instance.CanAttack = instance.HasKeyword(CardKeyword.Rush) || instance.HasKeyword(CardKeyword.Storm);
                }
            }
            else if (initialZone == CardZone.Leader)
            {
                instance.CanAttack = false; // Leaders generally don't attack this way
                                            // Ensure leader health is set correctly (often a fixed value like 20)
                instance.CurrentHealth = cardDef.Health ?? 20; // Use definition or default
                instance.MaxHealth = cardDef.Health ?? 20;
            }

            _logger.LogTrace("Created CardInstance: Id={InstanceId}, DefId={DefinitionId}, Name={Name}, Owner={OwnerPlayerId}, Zone={CurrentZone}",
                instance.InstanceId, instance.DefinitionId, cardDef.Name, instance.OwnerPlayerId, instance.CurrentZone);

            return instance;
        }

        private void ShuffleDeck(List<CardInstance> deck) // Changed parameter type to Domain Entity
        {
            _logger.LogWarning("[GameService] Placeholder ShuffleDeck called."); // Original line 725 content
                                                                                 // Placeholder: Simple Fisher-Yates shuffle
            /// <summary>
            /// Helper method to draw the initial hand from the deck at the start of the game (before mulligan).
            /// Modifies the input deck list directly by removing drawn cards.
            /// </summary>
            Random rng = new Random();
            int n = deck.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                CardInstance value = deck[k]; // Changed type to Domain Entity
                deck[k] = deck[n];
                deck[n] = value;
            }
        }

        private List<CardInstance> DrawInitialHand(List<CardInstance> deck, int handSize) // Changed types to Domain Entity
        {
            _logger.LogWarning("[GameService] Placeholder DrawInitialHand called.");
            // Placeholder: Draw cards from the top of the deck
            List<CardInstance> hand = new List<CardInstance>(); // Changed type to Domain Entity
            int drawCount = Math.Min(handSize, deck.Count);
            for (int i = 0; i < drawCount; i++)
            {
                if (deck.Count > 0) // Check if deck is not empty
                {
                    // Note: The original code directly added deck[0] to hand.
                    // We need to declare the variable first if we want to use it,
                    // but since the original didn't, let's keep it direct but with the correct type.
                    // CardInstanceDto drawnCard = deck[0]; // This would be needed if we used the variable
                    hand.Add(deck[0]); // Keep direct add, type compatibility handled by List<CardInstanceDto>
                    deck.RemoveAt(0);
                }
                else
                {
                    _logger.LogWarning("[GameService] Attempted to draw from empty deck in DrawInitialHand.");
                    break; // Stop drawing if deck is empty
                }
            }
            return hand;
        }


        // --- Helper Methods for Attack Logic ---

        /// <summary>
        /// 根据UID查找游戏实体，使用GameState实体而不是DTO。
        /// 支持通过PlayerId查找主战者，或通过InstanceId查找卡牌实例。
        /// </summary>
        /// <param name="gameState">游戏状态实体</param>
        /// <param name="uid">目标UID（可以是PlayerId或InstanceId）</param>
        /// <returns>找到的实体，如果未找到则返回null</returns>
        private ICard? FindEntityByUid(GameState gameState, Guid uid)
        {
            if (uid == Guid.Empty) // Changed to null check for GuidTypeMapping
            {
                _logger.LogWarning("[GameService:FindEntityByUid] Target UID is null or empty.");
                return null;
            }

            // 首先尝试通过PlayerId查找主战者
            // var targetPlayerById = gameState.GetPlayerState(uid);
            // if (targetPlayerById?.Leader != null)
            // {
            //     _logger.LogDebug("[GameService:FindEntityByUid] Found target entity by PlayerId: {PlayerId}", uid);
            //     return targetPlayerById.Leader; // 返回主战者CardInstance
            // }

            // 如果不是PlayerId，尝试解析为Guid格式的InstanceId
            // if (!Guid.TryParse(uid, out Guid targetGuid))
            // {
            //     _logger.LogWarning("[GameService:FindEntityByUid] Target UID '{TargetUid}' is not a valid Guid or PlayerId.", uid);
            //     return null;
            // }


            // 搜索所有玩家的所有区域
            foreach (var player in new[] { gameState.Player1, gameState.Player2 })
            {
                if (player == null) continue;

                // 检查主战者InstanceId
                if (player.Leader?.InstanceId == uid)
                {
                    _logger.LogDebug("[GameService:FindEntityByUid] Found target entity by Leader InstanceId: {InstanceId} for Player {PlayerId}", uid, player.PlayerId);
                    return player.Leader;
                }

                // 检查场上单位
                var unit = player.Field.FirstOrDefault(u => u.InstanceId == uid);
                if (unit != null)
                {
                    _logger.LogDebug("[GameService:FindEntityByUid] Found target entity by Field Unit InstanceId: {InstanceId} for Player {PlayerId}", uid, player.PlayerId);
                    return unit;
                }

                // 检查手牌（某些法术可能需要目标手牌）
                var handCard = player.Hand.FirstOrDefault(c => c.InstanceId == uid);
                if (handCard != null)
                {
                    _logger.LogDebug("[GameService:FindEntityByUid] Found target entity by Hand Card InstanceId: {InstanceId} for Player {PlayerId}", uid, player.PlayerId);
                    return handCard;
                }

                // 检查牌堆
                var deckCard = player.Deck.FirstOrDefault(c => c.InstanceId == uid);
                if (deckCard != null)
                {
                    _logger.LogDebug("[GameService:FindEntityByUid] Found target entity by Hand Card InstanceId: {InstanceId} for Player {PlayerId}", uid, player.PlayerId);
                    return deckCard;
                }

                // 检查墓地（某些法术可能需要目标墓地卡牌）
                var graveyardCard = player.Graveyard.FirstOrDefault(c => c.InstanceId == uid);
                if (graveyardCard != null)
                {
                    _logger.LogDebug("[GameService:FindEntityByUid] Found target entity by Graveyard Card InstanceId: {InstanceId} for Player {PlayerId}", uid, player.PlayerId);
                    return graveyardCard;
                }
            }

            _logger.LogWarning("[GameService:FindEntityByUid] Target entity with InstanceId/PlayerId '{TargetUid}' not found after checking all players and zones.", uid);
            return null;
        }


        // 已删除：CheckAndHandleDeath方法 - 死亡检查现在通过事件系统和EffectHandler处理
        // --- Helper Methods for HandlePlayCardAsync ---

        // Fetches CardDefinition and CardDto from hand synchronously using cache
        private Task<(CardDefinition? CardDefinition, CardInstance? CardInHand)> FetchCardDataAsync(string gameId, Guid playerId, Guid cardUid) // Return CardInstance?
        {
            _logger.LogDebug("[GameService:{GameId}:FetchCardData] Starting fetch for CardUID {CardUID}.", gameId, cardUid);
            if (cardUid == Guid.Empty)
                throw new GameLogicException("Card UID cannot be empty for fetching.");
            Guid cardInstanceId = cardUid; // cardUid is already a Guid

            int cardDefinitionId; // Use int for definition ID
            CardInstance? cardInHand; // Use CardInstance?
            object gameLock = _gameLocks.GetOrAdd(gameId, _ => new object());

            // Short lock just to get cardId from hand state (using Domain Entities)
            lock (gameLock)
            {
                // Use Domain Entity GameState
                if (!_gameStates.TryGetValue(gameId, out var gameState))
                    throw new GameLogicException($"Game not found: {gameId} (during card fetch)");

                // Use Domain Entity PlayerState
                var playerState = gameState.GetPlayerState(playerId);
                if (playerState == null)
                    throw new GameLogicException($"Player state error in game {gameId} (during card fetch).");

                // Find CardInstance in PlayerState.Hand (which should be List<CardInstance>)
                // Corrected: Compare Guid directly
                cardInHand = playerState.Hand.FirstOrDefault(c => c.InstanceId == cardInstanceId);
                if (cardInHand == null)
                    throw new GameLogicException($"Card with UID {cardUid} not found in player {playerId}'s hand (during card fetch).");

                cardDefinitionId = cardInHand.DefinitionId; // Get int DefinitionId from CardInstance
            }

            _logger.LogDebug("[GameService:{GameId}:FetchCardData] Need definition for CardId {CardDefinitionId}.", gameId, cardDefinitionId);

            // Fetch definition from cache instead of database
            var cardDefinition = _gameDataProvider.GetCardDefinition(cardDefinitionId);
            if (cardDefinition == null)
                throw new GameLogicException($"Card definition not found for CardId: {cardDefinitionId}");

            _logger.LogDebug("[GameService:{GameId}:FetchCardData] Fetched definition for {CardName} ({CardId}) from cache.", gameId, cardDefinition.Name, cardDefinition.Id);
            // Return CardInstance?
            return Task.FromResult<(CardDefinition? CardDefinition, CardInstance? CardInHand)>((cardDefinition, cardInHand));
        }

        // Validates preconditions for playing a card inside the main lock
        private void ValidatePlayCardPreconditions(GameState gameState, PlayerState playerState, CardDefinition cardDefinition, CardInstance? cardInHand, PlayCardActionDto actionDto, Guid playerId) // Changed DTOs to Domain Entities
        {
            _logger.LogDebug("[GameService:{GameId}:ValidatePlayCard] Validating preconditions for Player {PlayerId}, Card {CardName}.", gameState.GameId, playerId, cardDefinition.Name);
            if (gameState.IsGameOver) throw new GameLogicException("Game is already over.");
            if (gameState.CurrentPlayerId != playerId) throw new GameLogicException($"It's not player {playerId}'s turn.");
            if (gameState.Phase != GamePhase.Main) throw new GameLogicException($"Can only play cards during the Main phase (Current: {gameState.Phase}).");

            // Re-validate card still in hand inside lock
            // Now comparing CardInstance.InstanceId (string) with CardInstance?.InstanceId (string)
            if (!playerState.Hand.Any(c => c.InstanceId == cardInHand?.InstanceId))
                throw new GameLogicException($"Card {cardInHand?.InstanceId} disappeared from hand unexpectedly (inside lock validation).");

            // Null check for cardInHand before accessing Cost (though definition should exist if hand check passed)
            if (cardInHand == null) throw new GameLogicException("Internal error: Card instance became null after hand check.");
            // Use domain entity property: CurrentPlayPoints
            if (playerState.CurrentPlayPoints < cardDefinition.Cost) throw new GameLogicException($"Not enough PP to play {cardDefinition.Name}. Requires {cardDefinition.Cost}, has {playerState.CurrentPlayPoints}.");
            // Use centralized game constants
            const int MaxFieldSize = GameConstants.Field.MaxFieldSize;
            // Use domain entity property: Type (CardType enum)
            var cardType = cardDefinition.Type;

            if (cardType == CardType.Follower || cardType == CardType.Amulet) // Both take field space
            {
                // Use domain entity property: Field.Count
                if (playerState.Field.Count >= MaxFieldSize)
                {
                    throw new GameLogicException($"Cannot play {cardDefinition.Name}, field is full ({playerState.Field.Count}/{MaxFieldSize}).");
                }
            }
            _logger.LogDebug("[GameService:{GameId}:ValidatePlayCard] Preconditions validated successfully.", gameState.GameId);
        }

        // 已删除：ApplyPlayCardCostAndRemoveCard方法 - 已被PlayCardEffectHandler替代

        // Creates a new CardInstanceDto representing a follower entering the field
        private CardInstance CreateCardInstanceForBattle(CardDefinition cardDefinition, Guid ownerPlayerId) // Changed return type to CardInstance
        {
            _logger.LogDebug("[GameService:CreateCardInstanceForField] Creating CardInstance for {CardName} ({CardId}) for Player {PlayerId}.",
                cardDefinition.Name, cardDefinition.Id, ownerPlayerId);

            var instance = new CardInstance(
                instanceId: Guid.NewGuid(), // Generate Guid ID
                definitionId: cardDefinition.Id,
                ownerPlayerId: ownerPlayerId
            )
            {
                // Map properties from CardDefinition to CardInstance
                CardType = cardDefinition.Type,
                BaseCost = cardDefinition.Cost,         // 【修复】设置基础费用
                CurrentCost = cardDefinition.Cost,
                BaseAttack = cardDefinition.Attack ?? 0, // 【修复】设置基础攻击力
                CurrentAttack = cardDefinition.Attack ?? 0,
                BaseHealth = cardDefinition.Health ?? 0, // 【修复】设置基础生命值
                CurrentHealth = cardDefinition.Health ?? 0,
                MaxHealth = cardDefinition.Health ?? 0,
                Name = cardDefinition.Name,             // 【修复】设置卡牌名称
                Description = cardDefinition.Description ?? "", // 【修复】设置卡牌描述
                Rarity = cardDefinition.Rarity ?? "",   // 【修复】设置稀有度
                Class = cardDefinition.Class ?? "",     // 【修复】设置职业
                Tribe = cardDefinition.Tribe,           // 【修复】设置种族
                IsEvolved = false,
                Countdown = null, // Followers don't have countdown
                ZoneIndex = null, // Position is set when added to field list
                                  // Removed CanEvolve - evolution eligibility is determined by game rules
                AttacksThisTurn = 0,
                CurrentZone = CardZone.Field // Set initial zone
                                             // ActiveEffects and AbilityState are initialized by CardInstance constructor
            };

            // Set base keywords
            instance.SetBaseKeywords(cardDefinition.Keywords ?? new List<string>());

            // Set initial CanAttack based on keywords
            instance.CanAttack = instance.HasKeyword(CardKeyword.Rush) || instance.HasKeyword(CardKeyword.Storm);

            return instance; // Return the domain entity
        }
        public async Task BroadcastEventsAsync(string gameId, List<GameEventDto> events)
        {
            if (events == null || !events.Any())
            {
                _logger.LogDebug("[GameService:{GameId}] BroadcastEventsAsync called with null or empty event list. Skipping.", gameId);
                return;
            }

            _logger.LogInformation("[GameService:{GameId}] Broadcasting {EventCount} events.", gameId, events.Count);

            // It's generally safer to iterate over a copy if the original list might be modified elsewhere,
            // but in this context, it should be fine to iterate directly.
            foreach (var gameEvent in events)
            {
                try
                {
                    // Call the existing helper to broadcast each event individually
                    await BroadcastEventAsync(gameId, gameEvent);
                }
                catch (Exception ex)
                {
                    // Log error for individual event broadcast failure but continue with the rest
                    _logger.LogError(ex, "[GameService:{GameId}] Error broadcasting individual event of type {EventType} within BroadcastEventsAsync list.", gameId, gameEvent?.GetType().Name ?? "Unknown");
                }
            }
            _logger.LogInformation("[GameService:{GameId}] Finished broadcasting {EventCount} events.", gameId, events.Count);
        }
        // Helper to broadcast a single event to the game group
        private async Task BroadcastEventAsync(string gameId, GameEventDto gameEvent)
        {
            if (gameEvent == null) return;
            string groupName = $"Game_{gameId}";
            // <<< ADD DETAILED LOG BEFORE TRYING TO SEND >>>
            // Log the full event object if possible, otherwise key details. Using System.Text.Json for serialization.
            // --- Roo Add: Pre-Serialization Check ---
            string eventDetails = "N/A";
            try { eventDetails = System.Text.Json.JsonSerializer.Serialize(gameEvent, new System.Text.Json.JsonSerializerOptions { WriteIndented = false }); } // Use System.Text.Json
            catch (Exception jsonEx) { eventDetails = $"Error serializing: {jsonEx.Message}"; }
            _logger.LogInformation("[BROADCAST_ATTEMPT:{GameId}] Attempting to broadcast event of type {EventType} to group {GroupName}. Details: {EventDetails}",
                gameId, gameEvent.GetType().Name, groupName, eventDetails);
            // <<< HUB_TRACE LOGGING ADDED BY ROO >>>
            // <<< END ADDED LOG >>>
            _logger.LogDebug("[GameService:{GameId}] Broadcasting event {EventType} to group {GroupName}.", gameId, gameEvent.GetType().Name, groupName); // Keep original debug log too

            try
            {
                // Using IClientMethodMapper for dispatch - Reverted to direct call
                await _clientMethodMapper.BroadcastEventToGroupAllAsync(groupName, gameEvent);
                _logger.LogDebug("[GameService:{GameId}] Successfully invoked BroadcastEventToGroupAllAsync via mapper for event type {EventType}.", gameId, gameEvent.GetType().Name); // Updated log message
            }
            catch (Exception ex) // Catch potential errors during mapping or invocation
            {
                _logger.LogError(ex, "[GameService:{GameId}] Error mapping or invoking client method for event type {EventType}.", gameId, gameEvent.GetType().Name);
                // Do not re-throw here, as broadcasting failure shouldn't crash the main game logic flow.
            }
        } // <<< END BroadcastEventAsync method

        /// <summary>
        /// 自动触发 AI 玩家的 Mulligan 决策
        /// 在游戏创建后检测 AI 玩家并自动提交他们的 Mulligan 决策
        /// </summary>
        /// <param name="gameId">游戏ID</param>
        /// <param name="gameState">游戏状态</param>
        private async Task TriggerAiMulliganIfNeededAsync(string gameId, GameState gameState)
        {
            try
            {
                _logger.LogInformation("[GameService:{GameId}] Checking for AI players that need automatic mulligan...", gameId);

                // 检查游戏是否在 Mulligan 阶段
                if (gameState.Phase != GamePhase.Mulligan)
                {
                    _logger.LogDebug("[GameService:{GameId}] Game is not in Mulligan phase (Current: {Phase}). Skipping AI mulligan trigger.", gameId, gameState.Phase);
                    return;
                }

                // 检查 Player2 是否是 AI 且未完成 Mulligan
                if (gameState.Player2 != null && !gameState.Player2.HasCompletedMulligan)
                {
                    // 直接检查 PlayerState 的 IsAI 属性
                    if (gameState.Player2.IsAI)
                    {
                        _logger.LogInformation("[GameService:{GameId}] Triggering automatic mulligan for AI Player {PlayerId}...", gameId, gameState.Player2.PlayerId);

                        // 延迟一段时间模拟思考
                        await Task.Delay(2000);

                        // 获取当前游戏状态的 DTO
                        var gameStateDto = _mapper.Map<GameStateDto>(gameState);

                        // 调用 AI 服务决定 Mulligan
                        var cardsToReplace = await _aiService.DecideMulliganAsync(gameStateDto, gameState.Player2.PlayerId);

                        // 转换为 Guid 列表
                        var cardGuidsToReplace = cardsToReplace
                            .Select(cardId => Guid.TryParse(cardId, out var guid) ? guid : Guid.Empty)
                            .Where(guid => guid != Guid.Empty)
                            .ToList();

                        _logger.LogInformation("[GameService:{GameId}] AI Player {PlayerId} decided to replace {Count} cards: [{Cards}]",
                            gameId, gameState.Player2.PlayerId, cardGuidsToReplace.Count, string.Join(", ", cardGuidsToReplace));

                        // 创建 PlayerActionWrapper 并入队
                        var wrapper = new PlayerActionWrapper
                        {
                            ActionType = "SubmitMulligan",
                            PlayerId = gameState.Player2.PlayerId,
                            ActionData = new { CardUidsToReplace = cardGuidsToReplace }
                        };

                        // 提交 AI 的 Mulligan 决策
                        EnqueuePlayerAction(gameId, wrapper);

                        _logger.LogInformation("[GameService:{GameId}] AI Player {PlayerId} mulligan decision submitted successfully.", gameId, gameState.Player2.PlayerId);
                    }
                    else
                    {
                        _logger.LogDebug("[GameService:{GameId}] Player2 {PlayerId} is not an AI player. Skipping automatic mulligan.", gameId, gameState.Player2.PlayerId);
                    }
                }
                else
                {
                    _logger.LogDebug("[GameService:{GameId}] Player2 is null or has already completed mulligan. Skipping AI mulligan trigger.", gameId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Error occurred while triggering AI mulligan.", gameId);
                // 不重新抛出异常，避免影响游戏创建流程
            }
        }

    } // Closing brace for GameService class
} // Closing brace for ShadowverseBeyond.Server.Services namespace