using ShadowverseBeyond.Server.Domain.Abilities.Common;
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
using ShadowverseBeyond.Server.Dtos.SignalR.Actions; // Action DTOs incl. DebugCommandDto
// Updated Namespace
// using ShadowverseBeyond.Server.Models; // Already included above
using ShadowverseBeyond.Server.Services.Abstractions; // Added for IClientMethodMapper
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // Added for IConditionFactory
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // Added for IEffectExecutor
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Events; // Added for IEventProcessor and PlayCardEvent
using ShadowverseBeyond.Server.Domain.Factories; // Added for ICardFactory
using ShadowverseBeyond.Server.Domain.Buffs;

using ShadowverseBeyond.Server.Domain.Abilities; // Added for AbilityManager
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events.Services; // Added for keyword constants
using Microsoft.Extensions.DependencyInjection;

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

        private readonly IGameDataProvider _gameDataProvider; // Inject GameDataProvider for cached data access
        private readonly IPendingActionService _pendingActionService; // Add PendingActionService
        private readonly IMatchService _matchService;

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

        // Dictionary to manage DI scopes for each game session
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
        public Task EnqueuePlayerActionAsync(string gameId, PlayerActionWrapper actionWrapper)
        {
            if (string.IsNullOrEmpty(gameId) || actionWrapper == null)
            {
                _logger.LogWarning("[EnqueuePlayerActionAsync] Invalid arguments: gameId or actionWrapper is null/empty.");
                return Task.CompletedTask;
            }

            _logger.LogDebug("[EnqueuePlayerActionAsync:{GameId}] Received action {ActionType} from Player {PlayerId} with ActionId {ActionId}.", gameId, actionWrapper.ActionType, actionWrapper.PlayerId, actionWrapper.ActionId);

            var actionQueue = _playerActionQueues.GetOrAdd(gameId, _ => new ConcurrentQueue<PlayerActionWrapper>());
            actionQueue.Enqueue(actionWrapper);
            _logger.LogTrace("[EnqueuePlayerActionAsync:{GameId}] Action {ActionType} enqueued. Queue size: {QueueSize}", gameId, actionWrapper.ActionType, actionQueue.Count);

            // Fire-and-forget: The loop is started if not running, but the client does not wait for it.
            if (!_gameProcessingLoops.TryGetValue(gameId, out var existingLoop) || existingLoop.IsCompleted || existingLoop.IsFaulted || existingLoop.IsCanceled)
            {
                object loopLock = _gameLoopCreationLocks.GetOrAdd(gameId, _ => new object());
                lock (loopLock)
                {
                    if (!_gameProcessingLoops.TryGetValue(gameId, out existingLoop) || existingLoop.IsCompleted || existingLoop.IsFaulted || existingLoop.IsCanceled)
                    {
                        _logger.LogInformation("[EnqueuePlayerActionAsync:{GameId}] No active processing loop found. Starting a new one.", gameId);
                        var cts = new CancellationTokenSource();
                        _gameLoopCancellationTokens.AddOrUpdate(gameId, cts, (_, oldCts) => { oldCts.Dispose(); return cts; });
                        var processingTask = Task.Run(() => GameProcessingLoopAsync(gameId, cts.Token), cts.Token);
                        _gameProcessingLoops.AddOrUpdate(gameId, processingTask, (_, oldTask) => processingTask);
                        _logger.LogInformation("[EnqueuePlayerActionAsync:{GameId}] New processing loop started.", gameId);
                    }
                }
            }

            return Task.CompletedTask;
        }

        // Need a lock object dictionary for loop creation synchronization
        private readonly ConcurrentDictionary<string, object> _gameLoopCreationLocks = new();
        // Lock objects per game for general game state operations
        private readonly ConcurrentDictionary<string, object> _gameLocks = new();
        // Duplicate declaration removed by Roo.
        // Update constructor signature
        private readonly IClientMethodMapper _clientMethodMapper; // Added field declaration
        // Inject IClientMethodMapper, IBuffManager, and IMapper
        // Removed the duplicate field declaration added previously.
        // The original field declaration should exist elsewhere in the class.

        private readonly IServiceScopeFactory _serviceScopeFactory;
        public GameService(
            IHubContext<GameHub> hubContext,
            ILogger<GameService> logger,
            IMapper mapper,
            IClientMethodMapper clientMethodMapper,
            IGameDataProvider gameDataProvider,
            IMatchService matchService,
            IServiceScopeFactory serviceScopeFactory,
            IPendingActionService pendingActionService)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _clientMethodMapper = clientMethodMapper ?? throw new ArgumentNullException(nameof(clientMethodMapper));
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
            _matchService = matchService ?? throw new ArgumentNullException(nameof(matchService));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _pendingActionService = pendingActionService ?? throw new ArgumentNullException(nameof(pendingActionService));
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
            _logger.LogInformation("[GameProcessingLoop:{GameId}] Loop started.", gameId);
            var actionQueue = _playerActionQueues.GetOrAdd(gameId, _ => new ConcurrentQueue<PlayerActionWrapper>());

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    bool processedItem = false;

                    if (actionQueue.TryDequeue(out var actionWrapper))
                    {
                        processedItem = true;
                        cancellationToken.ThrowIfCancellationRequested();
                        _logger.LogDebug("[GameProcessingLoop:{GameId}] Dequeued action: {ActionType} for Player {PlayerId}", gameId, actionWrapper.ActionType, actionWrapper.PlayerId);

                        await using var scope = _serviceScopeFactory.CreateAsyncScope();
                        var sp = scope.ServiceProvider;
                        var gameContextAccessor = sp.GetRequiredService<IGameContextAccessor>();

                        try
                        {
                            var initialEvents = await ExecutePlayerActionInitialStepAsync(gameId, actionWrapper);
                            if (initialEvents.Count != 0)
                            {
                                _currentlyProcessingPlayerActionId.AddOrUpdate(gameId, actionWrapper.PlayerId.ToString(), (_, _) => actionWrapper.PlayerId.ToString());
                                _logger.LogInformation("[GameProcessingLoop:{GameId}] Started transaction for Player {PlayerId}.", gameId, actionWrapper.PlayerId);

                                if (_gameStates.TryGetValue(gameId, out var gameState) && gameState != null)
                                {
                                    var mapper = sp.GetRequiredService<IMapper>();

                                    _logger.LogWarning("[STATE_POLLUTION_TRACE] GameService: Creating GameContext for GameId: {GameId} with GameState HashCode: {HashCode}", gameId, gameState.GetHashCode());
                                    var gameContext = new GameContext(sp, mapper, gameContextAccessor, gameState, actionWrapper.PlayerId, cancellationToken, initialEvents[0]);
                                    var transactionService = sp.GetRequiredService<IGameActionTransactionService>();
                                    var transactionResult = await transactionService.ProcessTransactionAsync(gameContext, initialEvents);

                                    // --- NEW: Handle Paused Transaction ---
                                    // The transaction service will now handle its own pauses internally by awaiting player choices.
                                    // The GameService loop no longer needs to manage the pause/resume flow.
                                    // --- END: Handle Paused Transaction ---

                                    if (transactionResult.IsSuccess)
                                    {
                                        // 【功能补全】检查事务处理结果，如果包含回合开始事件，则触发AI
                                        var turnStartEvent = transactionResult.ProcessedEvents.OfType<TurnStartEvent>().FirstOrDefault();
                                        if (turnStartEvent?.Source?.Player != null && turnStartEvent.Source.Player.IsAI)
                                        {
                                            _logger.LogInformation("🤖 [AI TURN TRIGGER] Turn started for AI Player {PlayerId}. Triggering AI turn.", turnStartEvent.Source.Player.PlayerId);
                                            var aiService = sp.GetRequiredService<IAiService>();
                                            // 【BUG修复】为AI行动创建一个新的后台任务，并确保它拥有自己的DI作用域和GameContext
                                            _ = Task.Run(async () =>
                                            {
                                                // 为这个后台任务创建一个新的、独立的作用域
                                                await using var aiScope = _serviceScopeFactory.CreateAsyncScope();
                                                var aiSp = aiScope.ServiceProvider;
                                                var gameContextAccessor = aiSp.GetRequiredService<IGameContextAccessor>();
                                                var mapper = aiSp.GetRequiredService<IMapper>();

                                                // 获取最新的游戏状态
                                                if (_gameStates.TryGetValue(gameId, out var currentGameState))
                                                {
                                                    // 创建并设置GameContext
                                                    using (var aiGameContext = new GameContext(aiSp, mapper, gameContextAccessor, currentGameState, turnStartEvent.Source.Player.PlayerId, cancellationToken))
                                                    {
                                                        // 现在，在拥有正确上下文的情况下调用AI服务
                                                        var aiServiceFromScope = aiSp.GetRequiredService<IAiService>();
                                                        await aiServiceFromScope.RequestAiActionAsync(gameId, turnStartEvent.Source.Player.PlayerId);
                                                    }
                                                }
                                            }, cancellationToken);
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogError("Transaction failed for action {ActionType}: {Error}", actionWrapper.ActionType, transactionResult.ErrorMessage);
                                    }

                                    // --- BUG-FIX: Action Completion Logic ---
                                    // This logic MUST run here, after a transaction successfully completes.
                                    // It cannot run in a separate block that relies on the queue being empty,
                                    // as that block would incorrectly access a stale `actionWrapper` after a `continue`.
                                    _logger.LogInformation("[GameProcessingLoop:{GameId}] Action chain for Player {PlayerId} completed.", gameId, actionWrapper.PlayerId);

                                    // 1. Complete the task for the action that just finished.
                                    // The TaskCompletionSource logic has been removed to prevent deadlocks.

                                    // 2. Update playability for all relevant players.
                                    await UpdateAndBroadcastHandPlayabilityAsync(gameId, actionWrapper.PlayerId, cancellationToken);
                                    var opponentId = gameState.GetOpponentPlayerState(actionWrapper.PlayerId)?.PlayerId;
                                    if (opponentId.HasValue)
                                    {
                                        await UpdateAndBroadcastHandPlayabilityAsync(gameId, opponentId.Value, cancellationToken);
                                    }
                                    // --- End BUG-FIX ---
                                }
                            }
                            else
                            {
                                _logger.LogWarning("[GameProcessingLoop:{GameId}] Action {ActionType} did not generate initial events.", gameId, actionWrapper.ActionType);
                            }
                        }
                        catch (GameLogicException glex)
                        {
                            _logger.LogWarning(glex, "[GameProcessingLoop:{GameId}] Game logic error for action {ActionType} by Player {PlayerId}. Action rejected.", gameId, actionWrapper.ActionType, actionWrapper.PlayerId);
                            // TODO: Send error to player
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[GameProcessingLoop:{GameId}] Error processing action {ActionType} by Player {PlayerId}.", gameId, actionWrapper.ActionType, actionWrapper.PlayerId);
                        }
                        finally
                        {
                            // --- BUG-FIX: Clear the currently processing player ID here ---
                            // This ensures that even if the transaction fails or is paused, we are ready for the next action.
                            _currentlyProcessingPlayerActionId.TryUpdate(gameId, null, actionWrapper.PlayerId.ToString());
                            gameContextAccessor.GameContext = null;
                        }
                    }

                    if (!processedItem)
                    {
                        await Task.Delay(50, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[GameProcessingLoop:{GameId}] Loop canceled.", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "[GameProcessingLoop:{GameId}] Unhandled exception in processing loop. Terminating.", gameId);
            }
            finally
            {
                _logger.LogInformation("[GameProcessingLoop:{GameId}] Loop finished.", gameId);
                _gameProcessingLoops.TryRemove(gameId, out _);
                if (_gameLoopCancellationTokens.TryRemove(gameId, out var cts))
                {
                    cts.Dispose();
                }
                _currentlyProcessingPlayerActionId.TryRemove(gameId, out _);
            }
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
        private async Task<IReadOnlyList<IGameEvent>> ExecutePlayerActionInitialStepAsync(string gameId, PlayerActionWrapper actionWrapper)
        {
            // --- 1. Basic Validation (Requires reading GameState) ---
            GameState? gameState = null;
            PlayerState? playerState = null;

            if (!_gameStates.TryGetValue(gameId, out gameState))
            {
                throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Game not found: {gameId}");
            }

            if (gameState.IsGameOver && actionWrapper.ActionType != "DebugCommand")
            {
                throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Game is already over.");
            }

            playerState = gameState.GetPlayerState(actionWrapper.PlayerId);
            if (playerState == null)
            {
                throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Player state not found for {actionWrapper.PlayerId} in game {gameId}.");
            }

            // Phase-specific validations
            bool isDebugAction = actionWrapper.ActionType == "DebugCommand";
            bool isMulliganAction = actionWrapper.ActionType == "SubmitMulligan";
            bool isAlwaysAllowedAction = actionWrapper.ActionType is "Surrender" or "Disconnect";

            if (!isDebugAction)
            {
                if (isMulliganAction)
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
                else if (isAlwaysAllowedAction)
                {
                    _logger.LogDebug("[InitialStep:{ActionType}] Always-allowed action accepted for Player {PlayerId} (Phase: {Phase}).",
                        actionWrapper.ActionType, actionWrapper.PlayerId, gameState.Phase);
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
            }

            // --- 2. Create Initial Domain Event ---
            // 创建一个临时的服务作用域来获取所需的服务
            using var scope = _serviceScopeFactory.CreateScope();
            var serviceProvider = scope.ServiceProvider;
            var gameContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();

            // CancellationToken is not available here directly. For this temporary context,
            // it's acceptable to use CancellationToken.None as it's short-lived and doesn't perform awaitable operations.
            // However, the main context created in the loop WILL have the correct token.
            var gameContext = new GameContext(
                serviceProvider,
                _mapper,
                gameContextAccessor,
                gameState,
                actionWrapper.PlayerId,
                CancellationToken.None // This context is temporary and does not need cancellation.
            );

            var initialEvents = new List<IGameEvent>(capacity: 2);
            switch (actionWrapper.ActionType) // Use the string property
            {
                // Corrected: Use string literals for case comparison
                case "PlayCard":
                    _logger.LogWarning("🔥 [PLAYCARD_DEBUG] ExecutePlayerActionInitialStepAsync: Processing PlayCard action for Player {PlayerId}", actionWrapper.PlayerId);
                    if (actionWrapper.ActionData is PlayCardActionDto playDto)
                    {
                        _logger.LogWarning("🔥 [PLAYCARD_DEBUG] ExecutePlayerActionInitialStepAsync: Creating PlayCardRequestedEvent - Card: {CardUid}, Target: {TargetUid}",
                            playDto.CardUid, playDto.TargetUids?.FirstOrDefault());
                        initialEvents.Add(new PlayCardRequestedEvent(actionWrapper.PlayerId, playDto));
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
                        initialEvents.Add(new AttackRequestedEvent(actionWrapper.PlayerId, attackDto));
                    }
                    else throw new ArgumentException($"Invalid ActionData type for {actionWrapper.ActionType}");
                    break;

                // case "SubmitTargetChoice":
                //     if (actionWrapper.ActionData is SubmitTargetChoiceActionDto choiceDto)
                //     {
                //         initialEvent = new PlayerChoiceSubmittedEvent(actionWrapper.PlayerId, choiceDto);
                //     }
                //     else throw new ArgumentException($"Invalid ActionData type for {actionWrapper.ActionType}");
                //     break;

                case "EndTurn":
                    initialEvents.Add(new EndTurnRequestedEvent(actionWrapper.PlayerId));
                    break;

                case "PerformEvolve":
                    if (actionWrapper.ActionData is EvolveActionDto evolveDto)
                    {
                        initialEvents.Add(new EvolveRequestedEvent(actionWrapper.PlayerId, evolveDto));
                    }
                    else throw new ArgumentException($"Invalid ActionData type for {actionWrapper.ActionType}");
                    break;

                case "PerformSuperEvolve":
                    if (actionWrapper.ActionData is SuperEvolveActionDto superEvolveDto)
                    {
                        initialEvents.Add(new SuperEvolveRequestedEvent(actionWrapper.PlayerId, superEvolveDto));
                    }
                    else throw new ArgumentException($"Invalid ActionData type for {actionWrapper.ActionType}");
                    break;

                case "RequestAct":
                    if (actionWrapper.ActionData is ActActionDto actDto)
                    {
                        initialEvents.Add(new ActRequestedEvent(actionWrapper.PlayerId, actDto));
                    }
                    else throw new ArgumentException($"Invalid ActionData type for {actionWrapper.ActionType}");
                    break;

                case "Surrender":
                {
                    var opponent = gameState.GetOpponentPlayerState(actionWrapper.PlayerId);
                    if (opponent == null || opponent.PlayerId == Guid.Empty)
                    {
                        throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Opponent not found for player {actionWrapper.PlayerId}.");
                    }

                    initialEvents.Add(new GameEndEvent(opponent.PlayerId, actionWrapper.PlayerId, "Surrender"));
                    break;
                }

                case "Disconnect":
                {
                    var opponent = gameState.GetOpponentPlayerState(actionWrapper.PlayerId);
                    if (opponent == null || opponent.PlayerId == Guid.Empty)
                    {
                        throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Opponent not found for player {actionWrapper.PlayerId}.");
                    }

                    initialEvents.Add(new GameEndEvent(opponent.PlayerId, actionWrapper.PlayerId, "Disconnect"));
                    break;
                }

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
                        initialEvents.Add(new MulliganSubmittedEvent(actionWrapper.PlayerId, cardUids, gameContext));
                    }
                    // Handle anonymous object with CardUidsToReplace property (from GameHub)
                    else if (actionWrapper.ActionData != null)
                    {
                        var actionDataType = actionWrapper.ActionData.GetType();
                        var cardUidsProperty = actionDataType.GetProperty("CardUidsToReplace");

                        if (cardUidsProperty != null && cardUidsProperty.GetValue(actionWrapper.ActionData) is List<Guid> extractedCardUids)
                        {
                            initialEvents.Add(new MulliganSubmittedEvent(actionWrapper.PlayerId, extractedCardUids, gameContext));
                        }
                        // Handle JSON element format
                        else if (actionWrapper.ActionData is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                        {
                            try
                            {
                                var deserializedUids = jsonElement.Deserialize<List<Guid>>();
                                if (deserializedUids == null) throw new ArgumentException("Deserialized card UIDs list is null.");
                                initialEvents.Add(new MulliganSubmittedEvent(actionWrapper.PlayerId, deserializedUids, gameContext));
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

                case "DebugCommand":
                    if (actionWrapper.ActionData is DebugCommandDto debugDto)
                    {
                        var parser = serviceProvider.GetRequiredService<IDebugCommandParser>();
                        var parseResult = parser.Parse(gameState, actionWrapper.PlayerId, debugDto);
                        if (!parseResult.Success)
                        {
                            var message = parseResult.ErrorMessage ?? "debug-error: invalid command";
                            _logger.LogWarning("[DebugCommand:{GameId}] Parse failed for Player {PlayerId}: {Message}. Command={Command}",
                                gameId, actionWrapper.PlayerId, message, debugDto.Command);

                            await BroadcastEventToPlayerAsync(
                                gameId,
                                actionWrapper.PlayerId,
                                new LogEventDto(Guid.NewGuid(), "DebugCommand", $"debug-error: {message}", actionWrapper.PlayerId));

                            return Array.Empty<IGameEvent>();
                        }

                        initialEvents.AddRange(parseResult.GeneratedEvents);
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid ActionData type for {actionWrapper.ActionType}. Expected DebugCommandDto.");
                    }
                    break;

                default:
                    _logger.LogWarning("[ExecutePlayerActionInitialStepAsync] Unknown action type: {ActionType}", actionWrapper.ActionType);
                    throw new NotSupportedException($"Action type '{actionWrapper.ActionType}' is not supported.");
            }

            if (initialEvents.Count != 0)
            {
                _logger.LogDebug("[ExecutePlayerActionInitialStepAsync:{GameId}] Created {Count} initial domain event(s) for Action {ActionType}: {FirstEventType}",
                    gameId, initialEvents.Count, actionWrapper.ActionType, initialEvents[0].GetType().Name);
            }

            await Task.CompletedTask;
            return initialEvents;
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

        private async Task<List<int>?> ResolveDeckCardIdsAsync(
            MatchDeckSelection selection,
            IServiceProvider scopeServiceProvider,
            CancellationToken cancellationToken)
        {
            if (!selection.HasSelection)
            {
                return null;
            }

            var deckService = scopeServiceProvider.GetRequiredService<IDeckService>();
            var deckValidation = scopeServiceProvider.GetRequiredService<IDeckValidationService>();
            var deckCodeService = scopeServiceProvider.GetRequiredService<IDeckCodeService>();

            if (selection.DeckId.HasValue)
            {
                if (!TryBuildDeckOwner(selection, out var owner))
                {
                    return null;
                }

                var deck = await deckService.GetAsync(owner, selection.DeckId.Value, cancellationToken);
                if (deck == null)
                {
                    return null;
                }

                var validation = await deckValidation.ValidateAsync(deck.Class, deck.Cards, requireComplete: true, cancellationToken);
                if (!validation.Success || validation.Data == null)
                {
                    return null;
                }

                return ExpandDeckCardIds(validation.Data);
            }

            if (!string.IsNullOrWhiteSpace(selection.DeckCode))
            {
                if (!deckCodeService.TryDecode(selection.DeckCode, out var deckClass, out var cards, out _))
                {
                    return null;
                }

                var validation = await deckValidation.ValidateAsync(deckClass, cards, requireComplete: true, cancellationToken);
                if (!validation.Success || validation.Data == null)
                {
                    return null;
                }

                return ExpandDeckCardIds(validation.Data);
            }

            return null;
        }

        private static bool TryBuildDeckOwner(MatchDeckSelection selection, out DeckOwner owner)
        {
            if (selection.UserId.HasValue && selection.UserId.Value != Guid.Empty)
            {
                owner = new DeckOwner(selection.UserId, null);
                return true;
            }

            if (selection.GuestId.HasValue && selection.GuestId.Value != Guid.Empty)
            {
                owner = new DeckOwner(null, selection.GuestId);
                return true;
            }

            owner = default;
            return false;
        }

        private static List<int> ExpandDeckCardIds(IReadOnlyList<DeckCardEntryDto> cards)
        {
            var expanded = new List<int>(cards.Sum(card => card.Count));
            foreach (var entry in cards)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    expanded.Add(entry.CardDefinitionId);
                }
            }

            return expanded;
        }

        // --- Refactored to GetOrCreateGameStateAsync ---
        /// <summary>
        /// Gets the current game state domain entity for the given game ID, or creates a new one if it doesn't exist.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <param name="requestingPlayerId">The ID of the player requesting the game state (used for game creation).</param>
        /// <returns>The GameState domain entity for the game, or null if creation failed unexpectedly.</returns>
        public async Task<GameState?> GetOrCreateGameStateAsync(string gameId, Guid? hostPlayerId, Guid? guestPlayerId, bool useAiFallback, IServiceProvider scopeServiceProvider)
        {
            if (_gameStates.TryGetValue(gameId, out var existingState))
            {
                return existingState;
            }

            // Creation logic
            try
            {
                var cardFactory = scopeServiceProvider.GetRequiredService<ICardFactory>();

                Guid player1Id = hostPlayerId ?? Guid.NewGuid();
                Guid player2Id;

                bool createAi = false;
                if (guestPlayerId.HasValue)
                {
                    player2Id = guestPlayerId.Value;
                }
                else
                {
                    player2Id = Guid.NewGuid();
                    createAi = useAiFallback;
                }

                var allCardDefinitions = _gameDataProvider.GetAllCardDefinitions();
                if (!allCardDefinitions.Any())
                {
                    _logger.LogError("No card definitions found. Cannot create game.");
                    return null;
                }

                bool player1IsFirst = new Random().Next(0, 2) == 0;
                var player1State = new PlayerState(player1Id) { IsFirst = player1IsFirst };
                var player2State = new PlayerState(player2Id) { IsFirst = !player1IsFirst, IsAI = createAi };

                var newGameState = new GameState(gameId, player1State, player2State, player1IsFirst ? player1Id : player2Id);

                // --- Manual Manager Creation ---
                var loggerFactory = scopeServiceProvider.GetRequiredService<ILoggerFactory>();
                var abilityFactory = scopeServiceProvider.GetRequiredService<IAbilityFactory>();

                var systemAbilities = scopeServiceProvider.GetServices<IAbility>();
                var abilityManager = new AbilityManager(loggerFactory.CreateLogger<AbilityManager>(), abilityFactory, systemAbilities);
                var buffManager = new BuffManager(loggerFactory.CreateLogger<BuffManager>(), abilityManager);

                newGameState.AbilityManager = abilityManager;
                newGameState.BuffManager = buffManager;

                _logger.LogInformation("[GameService] Created and assigned AbilityManager for Game {GameId}.", gameId);
                // --- End Manual Manager Creation ---

                var roomResult = _matchService.Get(gameId);
                if (!roomResult.Success || roomResult.Room == null)
                {
                    _logger.LogWarning("[GameService] Match room not found while creating game state. GameId={GameId}", gameId);
                    return null;
                }

                var room = roomResult.Room;
                var hostDeckIds = await ResolveDeckCardIdsAsync(room.HostDeckSelection, scopeServiceProvider, CancellationToken.None);
                if (hostDeckIds == null)
                {
                    _logger.LogWarning("[GameService] Host deck selection invalid. GameId={GameId}", gameId);
                    return null;
                }

                var opponentSelection = room.GuestPlayerId.HasValue ? room.GuestDeckSelection : room.AiDeckSelection;
                var opponentDeckIds = await ResolveDeckCardIdsAsync(opponentSelection, scopeServiceProvider, CancellationToken.None);
                if (opponentDeckIds == null)
                {
                    _logger.LogWarning("[GameService] Opponent deck selection invalid. GameId={GameId}", gameId);
                    return null;
                }

                foreach (var cardId in hostDeckIds)
                {
                    cardFactory.CreateAndRegisterNewCard(newGameState, cardId, player1Id, CardZone.Deck);
                }

                foreach (var cardId in opponentDeckIds)
                {
                    cardFactory.CreateAndRegisterNewCard(newGameState, cardId, player2Id, CardZone.Deck);
                }

                ShuffleDeck(player1State.Deck);
                ShuffleDeck(player2State.Deck);

                // 【最终修复】根据领域规则，初始发牌不属于“抽牌”，不应触发抽牌事件。
                // 因此，回归到直接操作列表的方式。
                // 此操作现在是安全的，因为CardFactory的BUG已被修复，能确保Deck列表被正确填充。
                for (int i = 0; i < 3; i++)
                {
                    if (player1State.Deck.Any())
                    {
                        var card = player1State.Deck[0];
                        player1State.Deck.RemoveAt(0);
                        card.CurrentZone = CardZone.Hand;
                        player1State.Hand.Add(card);
                    }
                    if (player2State.Deck.Any())
                    {
                        var card = player2State.Deck[0];
                        player2State.Deck.RemoveAt(0);
                        card.CurrentZone = CardZone.Hand;
                        player2State.Hand.Add(card);
                    }
                }

                const int leaderCardId = GameConstants.Cards.LeaderCardId;
                player1State.Leader = cardFactory.CreateAndRegisterNewCard(newGameState, leaderCardId, player1Id, CardZone.Field);
                player2State.Leader = cardFactory.CreateAndRegisterNewCard(newGameState, leaderCardId, player2Id, CardZone.Field);

                if (_gameStates.TryAdd(gameId, newGameState))
                {
                    // Obsolete scope management and GameServiceProvider initialization removed.
                    // Scopes are now created on-demand in the processing loops.

                    // Pass the CancellationToken from the game loop if available, otherwise CancellationToken.None
                    var loopTokenSource = _gameLoopCancellationTokens.TryGetValue(gameId, out var cts) ? cts.Token : CancellationToken.None;
                    // 【FIX】Do not pass a scoped service provider to a background task.
                    // The task will create its own scope.
                    _ = Task.Run(() => TriggerAiMulliganIfNeededAsync(gameId, newGameState, loopTokenSource), loopTokenSource);
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
        /// <summary>
        /// Contains the core logic for checking if a card is playable, separated from the initial data fetching.
        /// This allows it to be called for proactive hand updates.
        /// </summary>
        /// TODO: 需要由单一的可玩性检测改成全面的可执行请求检测（攻击、进化、使用、启动）等等，并改为发送全面的GameStateUpdate

        private async Task<PlayabilityResultDto> CheckPlayabilityCoreLogicAsync(
            GameContext gameContext, // Pass the context directly
            PlayerState playerState,
            CardInstance cardInHand,
            CardDefinition cardDefinition
        )
        {
            var result = new PlayabilityResultDto();
            var gameId = gameContext.CurrentGameState.GameId;
            var playerId = playerState.PlayerId;
            var cardInstanceId = cardInHand.InstanceId;

            _logger.LogDebug("[CheckPlayabilityCore:{GameId}] Starting core logic check for Card {CardName} ({CardInstanceId}).", gameId, cardDefinition.Name, cardInstanceId);

            try
            {
                // --- Basic Checks ---
                var abilityConfigs = _gameDataProvider.GetAbilityConfigs(cardDefinition.Id);
                var modeResolution = PlayModeResolver.Resolve(cardInHand, playerState, abilityConfigs);
                result.PlayModes = modeResolution.Modes.ToList();
                result.SelectedPlayMode = modeResolution.SelectedMode;

                if (!modeResolution.HasAvailableMode)
                {
                    result.IsPlayable = false;
                    result.FailureReason = modeResolution.DefaultFailureReason ??
                        $"费用不足 (需要 {cardInHand.CurrentCost}, 当前 {playerState.CurrentPlayPoints})。";
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

                // --- PrePlay Condition Checks ---
                _logger.LogDebug("[CheckPlayabilityCore:{GameId}] Starting PrePlay condition checks for Card {CardName} ({CardInstanceId}).", gameId, cardDefinition.Name, cardInstanceId);
                var playTriggerConfigs = abilityConfigs
                    .Where(ac => ac.Trigger.EventTypes.Contains(EventType.PlayCard))
                    .ToList();

                // The gameContext is now passed in and is already set correctly in the accessor
                foreach (var config in playTriggerConfigs)
                {
                    var conditionEvaluator = gameContext.ServiceProvider.GetRequiredService<IConditionEvaluator>();
                    foreach (var condition in config.PreConditions)
                    {
                        if (!conditionEvaluator.Evaluate(condition, gameContext))
                        {
                            result.IsPlayable = false;
                            result.FailureReason = $"打出前置条件未满足: {condition.Expression}";
                            _logger.LogInformation("[CheckPlayabilityCore:{GameId}] PrePlay condition failed for Card {CardInstanceId}: {ConditionExpression}", gameId, cardInstanceId, condition.Expression);
                            return result;
                        }
                    }
                }
                _logger.LogDebug("[CheckPlayabilityCore:{GameId}] PrePlay conditions met for Card {CardInstanceId}.", gameId, cardInstanceId);

                // --- NEW: Target Availability Check (Non-Blocking) ---
                _logger.LogDebug("[CheckPlayabilityCore:{GameId}] Starting Target Availability checks for Card {CardInstanceId}.", gameId, cardInstanceId);

                var playCardAbilities = cardInHand.Abilities.Where(a => a.TriggerConfig.EventTypes.Contains(EventType.PlayCard));

                foreach (var ability in playCardAbilities)
                {
                    // Use the new non-blocking method to check if targets are available.
                    var availabilityResult = await ability.CheckTargetAvailabilityAsync(gameContext);

                    if (!availabilityResult.IsAvailable)
                    {
                        result.IsPlayable = false;
                        result.FailureReason = availabilityResult.FailureReason ?? "缺少有效的目标。";
                        _logger.LogInformation("[CheckPlayabilityCore:{GameId}] Card {CardInstanceId} is unplayable due to unavailable targets: {Reason}", gameId, cardInstanceId, result.FailureReason);
                        return result;
                    }

                    // Also, check if any target on this ability requires manual player choice.
                    // This is important for the UI to know whether to enable the 'Play' button or a 'Select Target' button.
                    if (ability.Targets.Any(t => t.Config.PlayerChoiceOptions?.IsRequired == true))
                    {
                        result.RequiresTargetSelection = true;
                    }
                }

                // --- All Checks Passed ---
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
        private async Task<PlayabilityResultDto> CheckCardPlayabilityAsync(string gameId, Guid playerId, Guid cardInstanceId, CancellationToken cancellationToken)
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
                var cardDefinition = _gameDataProvider.GetCardDefinition(card.CurrentDefinitionId);
                if (cardDefinition == null)
                {
                    result.IsPlayable = false;
                    result.FailureReason = "无法加载卡牌定义。";
                    _logger.LogWarning("[CheckPlayability:{GameId}] Card definition not found for DefinitionId {DefinitionId}.", gameId, card.CurrentDefinitionId);
                    return result;
                }

                // --- 2. Call Core Logic ---
                // Basic checks (Cost, Field Space) moved to CheckPlayabilityCoreLogicAsync
                // --- Create a temporary scope and context for this check ---
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var sp = scope.ServiceProvider;
                // By creating the GameContext within a 'using' block, we ensure that the ambient context
                // in GameServiceProvider is set upon creation and cleared upon disposal.
                var gameContextAccessor = sp.GetRequiredService<IGameContextAccessor>();
                using (var gameContext = new GameContext(sp, _mapper, gameContextAccessor, gameState, playerId, cancellationToken, null, card))
                {
                    result = await CheckPlayabilityCoreLogicAsync(gameContext, playerState, card, cardDefinition);
                }

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

        /// <summary>
        /// Checks the playability of all cards in a player's hand and broadcasts the results
        /// to the specific player client after their action chain completes.
        /// </summary>
        /// <param name="gameId">The ID of the game.</param>
        /// <param name="playerId">The ID of the player whose hand needs updating.</param>
        private async Task UpdateAndBroadcastHandPlayabilityAsync(string gameId, Guid playerId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[GameService:{GameId}] Updating hand playability for Player {PlayerId} after action completion.", gameId, playerId);
            var playabilityResults = new Dictionary<Guid, PlayabilityResultDto>();

            // --- Create a dedicated scope and context for this operation ---
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            var gameContextAccessor = sp.GetRequiredService<IGameContextAccessor>();

            try
            {
                var gameState = GetGameState(gameId);
                if (gameState == null)
                {
                    _logger.LogWarning("[UpdateHandPlayability:{GameId}] Game not found.", gameId);
                    return;
                }

                var playerState = gameState.GetPlayerState(playerId);
                if (playerState == null)
                {
                    _logger.LogWarning("[UpdateHandPlayability:{GameId}] Player state not found for Player {PlayerId}.", gameId, playerId);
                    return;
                }

                // Iterate through each card in the player's hand
                foreach (var cardInHand in playerState.Hand.ToList())
                {
                    var cardDefinition = _gameDataProvider.GetCardDefinition(cardInHand.CurrentDefinitionId);
                    if (cardDefinition == null)
                    {
                        _logger.LogWarning("[UpdateHandPlayability:{GameId}] Card definition {DefinitionId} not found for card {InstanceId} in Player {PlayerId}'s hand. Skipping.",
                            gameId, cardInHand.CurrentDefinitionId, cardInHand.InstanceId, playerId);
                        playabilityResults[cardInHand.InstanceId] = new PlayabilityResultDto { IsPlayable = false, FailureReason = "卡牌定义丢失" };
                        continue;
                    }

                    // For each card, create a specific GameContext with that card as the SourceCard.
                    // This is crucial for abilities that reference 'self' or 'owner' during playability checks.
                    using (var gameContext = new GameContext(sp, _mapper, gameContextAccessor, gameState, playerId, cancellationToken, null, cardInHand))
                    {
                        var singleResult = await CheckPlayabilityCoreLogicAsync(gameContext, playerState, cardInHand, cardDefinition);
                        playabilityResults[cardInHand.InstanceId] = singleResult;
                    }
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
                    // 【临时调试日志】序列化DTO以检查其确切内容
                    var dtoAsJson = "Serialization failed";
                    try { dtoAsJson = System.Text.Json.JsonSerializer.Serialize(updateDto); } catch { }
                    _logger.LogInformation("[DEBUG_PLAYABILITY] Sending HandPlayabilityUpdate to Group {GroupName} for Player {PlayerId}. DTO JSON: {DtoJson}", groupName, playerId, dtoAsJson);

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
        /// Broadcasts a single event to a specific player in a game group.
        /// </summary>
        private async Task BroadcastEventToPlayerAsync(string gameId, Guid playerId, GameEventDto gameEvent)
        {
            if (gameEvent == null) return;
            // Note: This relies on the client-side connection having its ConnectionId associated with the PlayerId group.
            // This is a common pattern but requires correct group management in the Hub.
            string playerGroupName = $"Player_{playerId}";
            _logger.LogInformation("[BROADCAST_PLAYER:{GameId}] Attempting to broadcast event of type {EventType} to player group {PlayerGroupName}.",
                gameId, gameEvent.GetType().Name, playerGroupName);

            try
            {
                await _clientMethodMapper.BroadcastEventToGroupAsync(playerGroupName, gameEvent);
                _logger.LogDebug("[GameService:{GameId}] Successfully invoked SendAsync for event type {EventType} to player group {PlayerGroupName}.",
                    gameId, gameEvent.GetType().Name, playerGroupName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Error invoking client method for event type {EventType} to player group {PlayerGroupName}.",
                    gameId, gameEvent.GetType().Name, playerGroupName);
            }
        }

        /// <summary>
        /// 自动触发 AI 玩家的 Mulligan 决策
        /// 在游戏创建后检测 AI 玩家并自动提交他们的 Mulligan 决策
        /// </summary>
        /// <param name="gameId">游戏ID</param>
        /// <param name="gameState">游戏状态</param>
        private async Task TriggerAiMulliganIfNeededAsync(string gameId, GameState gameState, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("[GameService:{GameId}] Checking for AI players that need automatic mulligan...", gameId);

                if (gameState.Phase != GamePhase.Mulligan)
                {
                    _logger.LogDebug("[GameService:{GameId}] Game is not in Mulligan phase (Current: {Phase}). Skipping AI mulligan trigger.", gameId, gameState.Phase);
                    return;
                }

                if (gameState.Player2 != null && !gameState.Player2.HasCompletedMulligan && gameState.Player2.IsAI)
                {
                    _logger.LogInformation("[GameService:{GameId}] Triggering automatic mulligan for AI Player {PlayerId}...", gameId, gameState.Player2.PlayerId);

                    await Task.Delay(2000, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    // 【FIX】Create a new scope for this background task to resolve scoped services correctly.
                    await using var scope = _serviceScopeFactory.CreateAsyncScope();
                    var sp = scope.ServiceProvider;
                    var aiService = sp.GetRequiredService<IAiService>();
                    var mapper = sp.GetRequiredService<IMapper>();
                    var gameContextAccessor = sp.GetRequiredService<IGameContextAccessor>();

                    List<Guid> cardGuidsToReplace;

                    // Create a temporary context for the AI's decision-making process.
                    using (var tempContext = new GameContext(sp, mapper, gameContextAccessor, gameState, gameState.Player2.PlayerId, cancellationToken))
                    {
                        var gameStateDto = mapper.Map<GameStateDto>(gameState);
                        var cardsToReplace = await aiService.DecideMulliganAsync(gameStateDto, gameState.Player2.PlayerId);
                        cardGuidsToReplace = cardsToReplace
                            .Select(cardId => Guid.TryParse(cardId, out var guid) ? guid : Guid.Empty)
                            .Where(guid => guid != Guid.Empty)
                            .ToList();
                    }

                    _logger.LogInformation("[GameService:{GameId}] AI Player {PlayerId} decided to replace {Count} cards: [{Cards}]",
                        gameId, gameState.Player2.PlayerId, cardGuidsToReplace.Count, string.Join(", ", cardGuidsToReplace));

                    var wrapper = new PlayerActionWrapper
                    {
                        ActionType = "SubmitMulligan",
                        PlayerId = gameState.Player2.PlayerId,
                        ActionData = new { CardUidsToReplace = cardGuidsToReplace }
                    };

                    _ = EnqueuePlayerActionAsync(gameId, wrapper);

                    _logger.LogInformation("[GameService:{GameId}] AI Player {PlayerId} mulligan decision submitted successfully.", gameId, gameState.Player2.PlayerId);
                }
                else
                {
                    _logger.LogDebug("[GameService:{GameId}] Player2 is null, has already completed mulligan, or is not an AI. Skipping AI mulligan trigger.", gameId);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[GameService:{GameId}] AI mulligan trigger was canceled.", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Error occurred while triggering AI mulligan.", gameId);
            }
        }

        /// <summary>
        /// Handles the submission of a player's target choice, bypassing the main action queue.
        /// This is an "out-of-band" request that resumes a paused action.
        /// </summary>
        public Task SubmitTargetChoiceAsync(string gameId, Guid playerId, SubmitTargetChoiceActionDto dto)
        {
            _logger.LogInformation("[SubmitTargetChoiceAsync:{GameId}] Received target choice from Player {PlayerId} for ability {AbilityId}",
                gameId, playerId, dto.SourceAbilityRuntimeId);

            // This method now only completes the pending choice.
            // The actual processing of the choice happens inside the game loop.
            _pendingActionService.CompletePlayerChoice(dto.PendingChoiceId, dto);

            return Task.CompletedTask;
        }

    } // Closing brace for GameService class
} // Closing brace for ShadowverseBeyond.Server.Services namespace
