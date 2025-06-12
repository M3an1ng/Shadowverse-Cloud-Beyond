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
using System.Text.Json; // Added for JsonElement, JsonException
// Removed duplicate: using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Exceptions; // GameLogicException
using Shadowverse.Domain.Interfaces; // Corrected namespace for ICard (and potentially others)
using ShadowverseBeyond.Server.Domain.Interfaces; // Added back for ICardRepository
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
using ShadowverseBeyond.Server.Domain.Abilities; // Added for ability system
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // Added for IConditionFactory
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // Added for IEffectExecutor
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters; // Added for effect parameters
using ShadowverseBeyond.Server.Domain.Abilities.Common; // Added for ValueConfig
using ShadowverseBeyond.Server.Domain.Events; // Added for IEventProcessor and PlayCardEvent

using ShadowverseBeyond.Server.Domain.Extensions; // Added for keyword extensions
using ShadowverseBeyond.Server.Domain.Constants; // Added for keyword constants

using ShadowverseBeyond.Server.Infrastructure.Data;         // For ApplicationDbContext
using Microsoft.Extensions.DependencyInjection;             // For GetRequiredService

using System.Threading;
using Microsoft.EntityFrameworkCore.Storage; // Added for CancellationTokenSource
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

        // Remove Scoped ICardRepository injection from Singleton GameService constructor
        // private readonly ICardRepository _cardRepository;
        private readonly IServiceScopeFactory _serviceScopeFactory; // Inject IServiceScopeFactory instead
        private readonly IAiService _aiService; // Added for AI logic
        private readonly IServiceProvider _serviceProvider; // Inject IServiceProvider to resolve Scoped dependencies
        private readonly Domain.Buffs.IBuffManager _buffManager; // Inject BuffManager
        // Removed _cardRepository field - use IServiceScopeFactory instead
        private readonly Domain.Abilities.DSL.Functions.IFunctionRegistry _functionRegistry; // Inject Function Registry
        private readonly IEventProcessor _eventProcessor; // Added for event processing
        private readonly Abstractions.IGameDataProvider _gameDataProvider; // Inject GameDataProvider for cached data access

        // Central Game State Storage - Stores GameState domain objects
        private readonly ConcurrentDictionary<string, GameState> _gameStates = new(); // Use Domain Entity
        // Lock objects per game to prevent concurrent modification of a single game's state
        private readonly ConcurrentDictionary<string, object> _gameLocks = new();
        // --- Queue and Processing Loop Management ---
        private readonly ConcurrentDictionary<string, ConcurrentQueue<PlayerActionWrapper>> _playerActionQueues = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<IGameEvent>> _eventQueues = new();
        private readonly ConcurrentDictionary<string, string?> _currentlyProcessingPlayerActionId = new(); // Tracks the PlayerId whose action chain is being processed
        private readonly ConcurrentDictionary<string, Task> _gameProcessingLoops = new(); // Stores the running Task for each game's loop
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _gameLoopCancellationTokens = new(); // Stores cancellation tokens for each loop
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
                return Task.CompletedTask; // Or throw ArgumentNullException
            }

            _logger.LogDebug("[EnqueuePlayerActionAsync:{GameId}] Received action {ActionType} from Player {PlayerId}.", gameId, actionWrapper.ActionType, actionWrapper.PlayerId);

            // 1. Get or add the action queue for the game
            var actionQueue = _playerActionQueues.GetOrAdd(gameId, _ => new ConcurrentQueue<PlayerActionWrapper>());

            // 2. Enqueue the action
            actionQueue.Enqueue(actionWrapper);
            _logger.LogTrace("[EnqueuePlayerActionAsync:{GameId}] Action {ActionType} enqueued. Queue size: {QueueSize}", gameId, actionWrapper.ActionType, actionQueue.Count);


            // 3. Ensure the processing loop is running for this game
            // Check if a loop task exists and if it's still active
            if (!_gameProcessingLoops.TryGetValue(gameId, out var existingLoop) || existingLoop.IsCompleted || existingLoop.IsFaulted || existingLoop.IsCanceled)
            {
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

            return Task.CompletedTask; // Enqueuing itself is fast, loop runs in background
        }

        // Need a lock object dictionary for loop creation synchronization
        private readonly ConcurrentDictionary<string, object> _gameLoopCreationLocks = new();
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

        public GameService(
            IHubContext<GameHub> hubContext, // Type already changed, ensuring consistency
            ILogger<GameService> logger,
            IMapper mapper, // Inject IMapper
            IServiceScopeFactory serviceScopeFactory,
            IAiService aiService,
            IServiceProvider serviceProvider,
            IClientMethodMapper clientMethodMapper,
            Domain.Buffs.IBuffManager buffManager, // Add IBuffManager
                                                   // Removed ICardRepository - use IServiceScopeFactory instead
            Domain.Abilities.DSL.Functions.IFunctionRegistry functionRegistry, // Add IFunctionRegistry
            Abstractions.IGameDataProvider gameDataProvider, // Add IGameDataProvider
            IEventProcessor eventProcessor) // Add IEventProcessor
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper)); // Store IMapper
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _clientMethodMapper = clientMethodMapper ?? throw new ArgumentNullException(nameof(clientMethodMapper)); // Assign clientMethodMapper
            _buffManager = buffManager ?? throw new ArgumentNullException(nameof(buffManager)); // Store BuffManager
            // Removed _cardRepository assignment - use IServiceScopeFactory instead
            _functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry)); // Store registry
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider)); // Store GameDataProvider
            _eventProcessor = eventProcessor ?? throw new ArgumentNullException(nameof(eventProcessor)); // Store IEventProcessor

            // Removed _eventHandlers dictionary initialization
        }

        /// <summary>
        /// 创建通用的EffectHandler执行辅助方法，减少重复代码
        /// </summary>
        private async Task<List<IGameEvent>> ExecuteEffectHandlerAsync(
            string gameId,
            Guid playerId,
            string effectType,
            Dictionary<string, ValueConfig> parameters,
            GameActionContext context)
        {
            // 1. 基础验证和数据获取
            if (!_gameStates.TryGetValue(gameId, out var gameState))
            {
                throw new GameLogicException($"Game not found: {gameId}");
            }

            var playerState = gameState.GetPlayerState(playerId);
            if (playerState == null)
            {
                throw new GameLogicException($"Player state not found for {playerId} in game {gameId}");
            }

            // 2. 创建GameContext和EffectExecutor
            using var scope = _serviceScopeFactory.CreateScope();
            var cardRepository = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.ICardRepository>();
            var functionRegistry = scope.ServiceProvider.GetRequiredService<Domain.Abilities.DSL.Functions.IFunctionRegistry>();

            var gameContext = new GameContext(
                gameState,
                playerId,
                _mapper,
                triggeringEvent: null,
                sourceCard: null,
                cardRepository: cardRepository,
                functionRegistry: functionRegistry
            );

            var effectExecutor = scope.ServiceProvider.GetRequiredService<IEffectExecutor>();
            var effectConfig = new EffectConfig
            {
                Type = effectType,
                Parameters = parameters
            };

            var events = await effectExecutor.ExecuteEffectAsync(effectConfig, gameContext);

            // 3. 将生成的事件添加到上下文
            foreach (var gameEvent in events)
            {
                context.DomainEvents.Add(gameEvent);
            }

            return events;
        }

        /// <summary>
        /// 重构后的出牌方法 - 使用PlayCardEffectHandler
        /// 保持GameService只负责验证、调度和队列管理职责
        /// </summary>
        private async Task<bool> PlayCardAsync(string gameId, Guid playerId, PlayCardActionDto actionDto, GameActionContext context)
        {
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} attempting PlayCard: CardUID={CardUID}, TargetUID={TargetUID}.",
                gameId, playerId, actionDto.CardUid, actionDto.TargetUid.ToString() ?? "None");

            try
            {
                var parameters = new Dictionary<string, ValueConfig>
                {
                    { "cardInstanceId", ValueConfig.Static(actionDto.CardUid) },
                    { "targetId", ValueConfig.Static(actionDto.TargetUid) },
                    { "forcePlay", ValueConfig.Static(false) },
                    { "playReason", ValueConfig.Static("PlayerAction") }
                };

                var events = await ExecuteEffectHandlerAsync(gameId, playerId, "playcard", parameters, context);

                _logger.LogInformation("[GameService:{GameId}] PlayCard completed successfully for Player {PlayerId}. Generated {EventCount} events.",
                    gameId, playerId, events.Count);

                return true;
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning(ex, "[GameService:{GameId}] Game logic error during PlayCard for Player {PlayerId}, Card {CardUID}.",
                    gameId, playerId, actionDto.CardUid);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Unexpected error during PlayCard for Player {PlayerId}, Card {CardUID}.",
                    gameId, playerId, actionDto.CardUid);
                throw new GameLogicException("An unexpected error occurred while playing the card.", ex);
            }
        }


        // Removed duplicate EnqueuePlayerActionAsync method definition.

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

        // Placeholder for the actual loop implementation
        private async Task GameProcessingLoopAsync(string gameId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[GameProcessingLoop:{GameId}] Loop started.", gameId);
            var actionQueue = _playerActionQueues.GetOrAdd(gameId, _ => new ConcurrentQueue<PlayerActionWrapper>());
            var eventQueue = _eventQueues.GetOrAdd(gameId, _ => new ConcurrentQueue<IGameEvent>());

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    bool processedItem = false; // Flag to check if any work was done in this iteration

                    // --- 1. Prioritize Event Queue ---
                    while (eventQueue.TryDequeue(out var currentDomainEvent))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _logger.LogDebug("[GameProcessingLoop:{GameId}] Dequeued Domain Event: {EventType}", gameId, currentDomainEvent.GetType().Name);

                        try
                        {
                            // TODO: Replace placeholder with actual IEventProcessor call
                            // The IEventProcessor needs access to GameState, AbilityManager, etc.
                            // This might involve resolving it within the loop or passing a context.
                            // ProcessEventAsync should return the DTO for broadcasting and any new domain events.
                            // var (eventDto, newDomainEvents) = await _eventProcessor.ProcessEventAsync(currentDomainEvent, gameId /* or context */);

                            GameState? gameState = null;
                            object gameLock = _gameLocks.GetOrAdd(gameId, _ => new object());
                            List<GameEventDto> resultingEventDtos = new List<GameEventDto>(); // Initialize outside lock
                            List<IGameEvent> newDomainEvents = new List<IGameEvent>(); // Initialize outside lock (Placeholder for now)

                            // Create a scope to resolve scoped services like IEventProcessor
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var eventProcessor = scope.ServiceProvider.GetRequiredService<IEventProcessor>();
                                var cardRepository = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.ICardRepository>(); // Needed for GameContext
                                var functionRegistry = scope.ServiceProvider.GetRequiredService<Domain.Abilities.DSL.Functions.IFunctionRegistry>(); // Needed for GameContext

                                GameContext? gameContext = null; // Declare context outside lock
                                lock (gameLock) // Lock before accessing GameState
                                {
                                    if (!_gameStates.TryGetValue(gameId, out gameState) || gameState == null)
                                    {
                                        _logger.LogError("[GameProcessingLoop:{GameId}] GameState not found for event processing. Skipping event {EventType}.", gameId, currentDomainEvent.GetType().Name);
                                        continue; // Skip this event if state is missing
                                    }

                                    // Determine requestingPlayerId (Needs refinement)
                                    Guid? requestingPlayerId = currentDomainEvent.SourcePlayer?.PlayerId;
                                    if (requestingPlayerId == null || requestingPlayerId == Guid.Empty)
                                    {
                                        requestingPlayerId = gameState.CurrentPlayerId;
                                        _logger.LogTrace("[GameProcessingLoop:{GameId}] Could not determine RequestingPlayerId from event {EventType}. Using CurrentPlayerId ({PlayerId}) as fallback.",
                                            gameId, currentDomainEvent.GetType().Name, requestingPlayerId);
                                    }
                                    if (requestingPlayerId == null || requestingPlayerId == Guid.Empty)
                                    {
                                        _logger.LogError("[GameProcessingLoop:{GameId}] Critical: Could not determine RequestingPlayerId for event {EventType} even after fallback. Skipping event.", gameId, currentDomainEvent.GetType().Name);
                                        continue;
                                    }

                                    // Create GameContext
                                    gameContext = new GameContext(
                                        gameState,
                                        requestingPlayerId ?? Guid.Empty,
                                        _mapper,
                                        triggeringEvent: currentDomainEvent,
                                        sourceCard: (currentDomainEvent as dynamic)?.SourceCard,
                                        cardRepository: cardRepository,
                                        functionRegistry: functionRegistry
                                    );

                                    // GameContext is created inside the lock
                                    _logger.LogDebug("[GameProcessingLoop:{GameId}] GameContext created for event {EventType}.", gameId, currentDomainEvent.GetType().Name);
                                    // IMPORTANT: Assign context to a variable declared outside the lock scope
                                    // so we can use it after releasing the lock.
                                    // We'll rename the outer variable later if needed for clarity.

                                } // Release game lock HERE before awaiting

                                // Check if context was successfully created before proceeding outside the lock
                                if (gameContext != null)
                                {
                                    // Call the processor OUTSIDE the lock
                                    _logger.LogDebug("[GameProcessingLoop:{GameId}] Calling IEventProcessor.ProcessEventAsync for {EventType} (outside lock)...", gameId, currentDomainEvent.GetType().Name);
                                    var processorResult = await eventProcessor.ProcessEventAsync(currentDomainEvent, gameContext);
                                    newDomainEvents = processorResult.NewDomainEvents; // Assign to loop-scoped variable
                                    resultingEventDtos = processorResult.ResultingDtos; // Assign to loop-scoped variable
                                    _logger.LogDebug("[GameProcessingLoop:{GameId}] IEventProcessor.ProcessEventAsync completed for {EventType}. Returned {EventCount} new domain events and {DtoCount} DTOs.",
                                        gameId, currentDomainEvent.GetType().Name, newDomainEvents?.Count ?? 0, resultingEventDtos?.Count ?? 0);
                                }
                                else
                                {
                                    // Log error if context couldn't be created (e.g., GameState missing inside lock)
                                    _logger.LogError("[GameProcessingLoop:{GameId}] Failed to create GameContext for event {EventType}. Skipping processing.", gameId, currentDomainEvent.GetType().Name);
                                    // Ensure lists are empty if processing is skipped
                                    newDomainEvents = new List<IGameEvent>();
                                    resultingEventDtos = new List<GameEventDto>();
                                }
                            } // Scope disposed here

                            // --- Broadcast and Enqueue New Events (Outside Lock) ---

                            // 1. Broadcast the resulting DTOs from the event processing
                            if (resultingEventDtos != null && resultingEventDtos.Any())
                            {
                                _logger.LogDebug("[GameProcessingLoop:{GameId}] Broadcasting {DtoCount} DTOs resulting from event {EventType}.", gameId, resultingEventDtos.Count, currentDomainEvent.GetType().Name);
                                await BroadcastEventsAsync(gameId, resultingEventDtos);
                            }
                            else
                            {
                                _logger.LogTrace("[GameProcessingLoop:{GameId}] No DTOs returned by processor for event {EventType}, skipping broadcast.", gameId, currentDomainEvent.GetType().Name);
                            }

                            // 2. Enqueue any NEW domain events returned by the processor
                            if (newDomainEvents != null && newDomainEvents.Any())
                            {
                                _logger.LogDebug("[GameProcessingLoop:{GameId}] Enqueuing {EventCount} new domain events resulting from event {EventType}.", gameId, newDomainEvents.Count, currentDomainEvent.GetType().Name);
                                foreach (var newEvent in newDomainEvents)
                                {
                                    eventQueue.Enqueue(newEvent);
                                    _logger.LogTrace("[GameProcessingLoop:{GameId}] Enqueued new Domain Event: {NewEventType}", gameId, newEvent.GetType().Name);
                                }
                            }

                            processedItem = true; // Mark item as processed successfully

                        }
                        catch (OperationCanceledException) { throw; } // Re-throw cancellation
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[GameProcessingLoop:{GameId}] Error processing domain event {EventType}.", gameId, currentDomainEvent?.GetType().Name ?? "Unknown");
                            // Consider strategy for event processing errors (e.g., skip, retry, halt loop?)
                        }
                    } // End of event queue processing loop

                    // --- 2. Process Action Queue if Event Queue is Empty ---
                    if (eventQueue.IsEmpty && actionQueue.TryDequeue(out var actionWrapper))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _logger.LogDebug("[GameProcessingLoop:{GameId}] Dequeued Player Action: {ActionType} for Player {PlayerId}", gameId, actionWrapper.ActionType, actionWrapper.PlayerId);

                        try
                        {
                            // Execute the initial step of the player action
                            var actionContext = new GameActionContext(); // Create context for this action
                            await ExecutePlayerActionInitialStepAsync(gameId, actionWrapper, actionContext); // Call the implemented method

                            // Enqueue the initial domain events generated by the action
                            if (actionContext.DomainEvents.Any())
                            {
                                foreach (var initialEvent in actionContext.DomainEvents)
                                {
                                    eventQueue.Enqueue(initialEvent);
                                    _logger.LogDebug("[GameProcessingLoop:{GameId}] Enqueued initial Domain Event: {EventType} from Action {ActionType}", gameId, initialEvent.GetType().Name, actionWrapper.ActionType);
                                }
                                // Set the currently processing player ID *after* successfully enqueuing initial events
                                // Use AddOrUpdate for thread safety, ensuring we don't overwrite if already processing another action (shouldn't happen with this logic, but safer)
                                _currentlyProcessingPlayerActionId.AddOrUpdate(gameId, actionWrapper.PlayerId.ToString(), (_, existingPlayerId) => actionWrapper.PlayerId.ToString());
                                _logger.LogInformation("[GameProcessingLoop:{GameId}] Started processing action chain for Player {PlayerId}.", gameId, actionWrapper.PlayerId);
                            }
                            else
                            {
                                _logger.LogWarning("[GameProcessingLoop:{GameId}] Action {ActionType} for Player {PlayerId} did not generate any initial domain events.", gameId, actionWrapper.ActionType, actionWrapper.PlayerId);
                                // If no events, the action effectively completes immediately. Clear the processing ID if it was somehow set.
                                // Check if the value is the one we might have just set before removing.
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
                    // Check if event queue is empty, action queue is empty, AND we were processing an action chain
                    if (eventQueue.IsEmpty && actionQueue.IsEmpty && _currentlyProcessingPlayerActionId.TryGetValue(gameId, out var completedPlayerId) && completedPlayerId != null)
                    {
                        _logger.LogInformation("[GameProcessingLoop:{GameId}] Action chain for Player {PlayerId} completed (Event & Action queues empty).", gameId, completedPlayerId);
                        // Clear the processing flag *before* starting the update
                        // Use TryUpdate with comparison for atomicity
                        if (_currentlyProcessingPlayerActionId.TryUpdate(gameId, null, completedPlayerId))
                        {
                            try
                            {
                                // Call the actual method to update and broadcast hand playability
                                await UpdateAndBroadcastHandPlayabilityAsync(gameId, Guid.Parse(completedPlayerId));
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
                // Consider cleanup for action/event queues if needed (e.g., log remaining items)
                if (actionQueue != null && !actionQueue.IsEmpty)
                {
                    _logger.LogWarning("[GameProcessingLoop:{GameId}] Loop terminated with {Count} items remaining in action queue.", gameId, actionQueue.Count);
                }
                if (eventQueue != null && !eventQueue.IsEmpty)
                {
                    _logger.LogWarning("[GameProcessingLoop:{GameId}] Loop terminated with {Count} items remaining in event queue.", gameId, eventQueue.Count);
                }
            }
        } // End of GameProcessingLoopAsync
          // Removed incorrectly inserted code block from previous apply_diff attempt.

        /// <summary>
        /// Executes the initial validation and event generation step for a player action dequeued from the action queue.
        /// This method performs basic checks and creates the initial domain event(s) to be processed by the event loop.
        /// It does NOT modify core game state directly (e.g., PP deduction); that is handled by the event processor.
        /// </summary>
        /// <param name="gameId">The game ID.</param>
        /// <param name="actionWrapper">The player action wrapper.</param>
        /// <param name="context">The game action context to add generated domain events to.</param>
        /// <exception cref="GameLogicException">Thrown if the action is invalid based on initial checks.</exception>
        private async Task ExecutePlayerActionInitialStepAsync(string gameId, PlayerActionWrapper actionWrapper, GameActionContext context)
        {
            // --- 1. Basic Validation (Requires reading GameState) ---
            GameState? gameState = null;
            PlayerState? playerState = null;
            bool isValid = false;

            object gameLock = _gameLocks.GetOrAdd(gameId, _ => new object());
            lock (gameLock) // Lock needed to safely read current player and phase
            {
                if (!_gameStates.TryGetValue(gameId, out gameState))
                {
                    throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Game not found: {gameId}");
                }
                playerState = gameState.GetPlayerState(actionWrapper.PlayerId);
                if (playerState == null)
                {
                    throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Player state not found for {actionWrapper.PlayerId} in game {gameId}.");
                }

                // Common validations: Is it the player's turn? Is the game in the right phase?
                if (gameState.CurrentPlayerId != actionWrapper.PlayerId)
                {
                    throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] It's not player {actionWrapper.PlayerId}'s turn (Current: {gameState.CurrentPlayerId}).");
                }
                // Allow Mulligan submission only during Mulligan phase
                // Corrected: Compare ActionType string with string literal
                if (actionWrapper.ActionType == "SubmitMulligan" && gameState.Phase != GamePhase.Mulligan)
                {
                    throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Action only allowed during Mulligan phase (Current: {gameState.Phase}).");
                }
                // Allow other actions only during Main phase
                // Corrected: Compare ActionType string with string literal
                if (actionWrapper.ActionType != "SubmitMulligan" && gameState.Phase != GamePhase.Main)
                {
                    throw new GameLogicException($"[InitialStep:{actionWrapper.ActionType}] Action only allowed during Main phase (Current: {gameState.Phase}).");
                }

                // TODO: Add more specific initial validations if needed (e.g., basic PP check for PlayCard?)

                isValid = true; // If no exception thrown
            } // Release lock

            if (!isValid) return; // Should have thrown if invalid

            // --- 2. Create Initial Domain Event ---
            IGameEvent initialEvent;
            switch (actionWrapper.ActionType) // Use the string property
            {
                // Corrected: Use string literals for case comparison
                case "PlayCard":
                    if (actionWrapper.ActionData is PlayCardActionDto playDto)
                    {
                        initialEvent = new PlayCardRequestedEvent(actionWrapper.PlayerId, playDto);
                    }
                    else throw new ArgumentException($"Invalid ActionData type for {actionWrapper.ActionType}");
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

            // --- 3. Add Event to Context ---
            if (initialEvent != null) // Should always be non-null if no exception
            {
                context.DomainEvents.Add(initialEvent);
                _logger.LogDebug("[ExecutePlayerActionInitialStepAsync:{GameId}] Added initial domain event {EventType} for Action {ActionType}",
                    gameId, initialEvent.GetType().Name, actionWrapper.ActionType);
            }

            // Add await Task.CompletedTask; if the method doesn't have any other awaits
            await Task.CompletedTask;
        }

        /// <summary>
        /// 重构后的结束回合方法 - 使用TurnEndEffectHandler
        /// 保持GameService只负责验证、调度和队列管理职责
        /// </summary>
        private async Task EndTurnAsync(string gameId, Guid playerId, GameActionContext context)
        {
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} attempting EndTurn.", gameId, playerId);

            try
            {
                // 获取玩家回合数用于参数
                int turnNumber = 1;
                if (_gameStates.TryGetValue(gameId, out var gameState))
                {
                    var playerState = gameState.GetPlayerState(playerId);
                    if (playerState != null)
                    {
                        turnNumber = playerState.TurnNumber;
                    }
                }

                var parameters = new Dictionary<string, ValueConfig>
                {
                    { "endingPlayerId", ValueConfig.Static(playerId) },
                    { "turnNumber", ValueConfig.Static(turnNumber) },
                    { "forceEndTurn", ValueConfig.Static(false) },
                    { "endTurnReason", ValueConfig.Static("PlayerAction") }
                };

                var events = await ExecuteEffectHandlerAsync(gameId, playerId, "turnend", parameters, context);

                _logger.LogInformation("[GameService:{GameId}] EndTurn completed successfully for Player {PlayerId}. Generated {EventCount} events.",
                    gameId, playerId, events.Count);

                await Task.CompletedTask;
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning(ex, "[GameService:{GameId}] Game Logic Error during EndTurn for Player {PlayerId}", gameId, playerId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Unexpected error during EndTurn for Player {PlayerId}", gameId, playerId);
                throw new GameLogicException("An unexpected error occurred during turn end.", ex);
            }
        }







        /// <summary>
        /// Processes the start-of-turn logic for the specified player.
        /// This includes incrementing turn count, updating PP/EP, drawing cards,
        /// resetting unit states, and generating necessary start-of-turn events.
        /// This method is called after mulligan completion for the first player,
        /// and by EndTurnAsync (for human players) or AiTurnExecutor (for AI players)
        /// for subsequent turns.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="playerId">The ID of the player whose turn is starting.</param>
        /// <returns>A list of game events generated during start-of-turn processing.</returns>
        /// <exception cref="GameLogicException">Thrown if the game or player state is invalid.</exception>
        // V2 Refactor: Signature changed to return tuple (Events, IsAiTurn) and use startingPlayerId parameter.
        // M7 V1.1 Change: Signature updated to remove context and return Task<TurnStartedEventDto>
        // Made private as per queue design (or potentially removed if logic fully moves)
        private async Task<TurnStartedEventDto> StartPlayerTurnAsync(string gameId, Guid startingPlayerId)
        {
            _logger.LogInformation("[GameService:{GameId}] Executing StartPlayerTurnAsync for Player {StartingPlayerId}.", gameId, startingPlayerId);
            // M8 Change: Removed local events list creation. Using context.

            // Acquire lock for this specific game
            bool isAiTurn = false; // M8 Change: Declare isAiTurn here to be accessible for return value
            object gameLock = _gameLocks.GetOrAdd(gameId, _ => new object()); // Fixed lambda syntax
            GameState? gameState = null; // Declare gameState here to be accessible after the lock (Changed Type to Domain Entity)

            // --- Pre-fetch card definitions outside the lock ---
            Dictionary<int, CardDefinition> unitDefinitions = new Dictionary<int, CardDefinition>();
            // Temporarily get game state to check for units
            if (_gameStates.TryGetValue(gameId, out var tempGameState))
            {
                var tempPlayerState = tempGameState.GetPlayerState(startingPlayerId);
                if (tempPlayerState != null && tempPlayerState.Field.Any())
                {
                    _logger.LogDebug("[GameService:{GameId}] Pre-fetching unit definitions for Player {PlayerId} from cache.", gameId, startingPlayerId);
                    try
                    {
                        // Use GameDataProvider cache instead of database access
                        var definitionIds = tempPlayerState.Field.Select(u => u.DefinitionId).Distinct().ToList();

                        // Fetch definitions from cache (no async needed)
                        foreach (var defId in definitionIds)
                        {
                            var def = _gameDataProvider.GetCardDefinition(defId);
                            if (def != null)
                            {
                                unitDefinitions[defId] = def;
                            }
                            else
                            {
                                _logger.LogWarning("[GameService:{GameId}] Pre-fetch: Card definition not found for ID {DefinitionId}.", gameId, defId);
                            }
                        }
                        _logger.LogDebug("[GameService:{GameId}] Pre-fetched {Count} unit definitions from cache.", gameId, unitDefinitions.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[GameService:{GameId}] Error during pre-fetching card definitions for Player {PlayerId}.", gameId, startingPlayerId);
                        // Continue without pre-fetched data, the lock block might handle it or fail later.
                    }
                }
                else
                {
                    _logger.LogDebug("[GameService:{GameId}] No units on field for Player {PlayerId}, skipping definition pre-fetch.", gameId, startingPlayerId);
                }
            }
            else
            {
                _logger.LogWarning("[GameService:{GameId}] Game state not found before lock, cannot pre-fetch unit definitions.", gameId);
                // The lock block below will handle the missing state error properly.
            }
            // --- End Pre-fetch ---
            lock (gameLock)
            {
                // 1. Validate gameId and retrieve GameState.
                // bool isAiTurnResult = false; // Declaration moved outside lock
                // M8 Change: Retrieve gameState inside lock, assign to pre-declared variable
                if (!_gameStates.TryGetValue(gameId, out gameState)) // Removed 'var'
                {
                    throw new GameLogicException($"Game not found: {gameId} (in StartPlayerTurnAsync)");
                }

                // 2. Validate Player State
                // Use helper method to get player state
                var startingPlayerState = gameState.GetPlayerState(startingPlayerId);
                if (startingPlayerState == null)
                {
                    throw new GameLogicException($"Player state not found for {startingPlayerId} in game {gameId} (in StartPlayerTurnAsync).");
                }

                // --- Start of logic adapted from ProcessStartOfTurn ---

                // --- Increment Player's Individual Turn Count ---
                startingPlayerState.TurnNumber++;

                // Existing code continues below...
                // V2 Change: Determine AI status inside lock
                isAiTurn = startingPlayerState.IsAI; // M8 Change: Assign value inside lock where startingPlayerState is valid
                int currentPlayerTurn = startingPlayerState.TurnNumber; // Use this for player-specific logic
                _logger.LogInformation("[GameService:{GameId}] Incrementing TurnNumber for {PlayerId} to {PlayerTurn}.",
                    gameState.GameId, startingPlayerState.PlayerId, currentPlayerTurn);

                _logger.LogWarning("[GS_DEBUG_SOT:{GameId}] Processing StartOfTurn for PlayerId={PlayerId}", gameState.GameId, startingPlayerState.PlayerId); // DEBUG LOG (Fixed format string)

                // --- M7 Design Doc Step 5.1: Update Game State ---
                Guid? oldPlayerId = gameState.CurrentPlayerId; // Store old ID before changing
                gameState.CurrentPlayerId = startingPlayerState.PlayerId;
                gameState.Phase = GamePhase.Main; // Transition to Main phase
                _logger.LogWarning("[GS_DEBUG_SOT:{GameId}] Setting gameState.CurrentPlayerId from '{OldPlayer}' to '{NewPlayer}'", gameState.GameId, oldPlayerId, gameState.CurrentPlayerId); // DEBUG LOG
                gameState.GameMessage = $"Turn {currentPlayerTurn} - {startingPlayerState.PlayerId}'s turn."; // Use individual turn number for message
                _logger.LogInformation("[GameService:{GameId}] StartOfTurn processing state updated for Player={CurrentPlayerId}, PlayerTurn={PlayerTurn}, Phase={Phase}.", // Original log
                   gameState.GameId, gameState.CurrentPlayerId, currentPlayerTurn, gameState.Phase);

                // --- M7 Design Doc Step 5.2: Update Player PP ---
                // 使用PlayerState领域方法处理PP恢复
                var ppEvents = startingPlayerState.RestorePlayPoints(currentPlayerTurn);
                // TODO: 在未来版本中，这些事件应该被添加到事件队列中进行处理
                // 目前暂时跳过事件处理，直接使用领域方法的状态修改结果

                _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} PP updated: {CurrentPP}/{MaxPP}.",
                    gameState.GameId, startingPlayerState.PlayerId, startingPlayerState.CurrentPlayPoints, startingPlayerState.MaxPlayPoints);

                // --- M7 Design Doc Step 5.3: Update Player EP (Fixed for Shadowverse Rules) ---
                int requiredEvoTurn = startingPlayerState.IsFirst ? 5 : 4; // P1 needs Turn 5, P2 needs Turn 4
                                                                           // Use PlayerState domain property names
                bool shouldUnlockEvolution = currentPlayerTurn >= requiredEvoTurn && startingPlayerState.MaxEvolutionPoints == 0;
                int previousEP = startingPlayerState.CurrentEvolutionPoints; // Store previous EP for logging/event

                // Evolution unlock: Set EP limit and grant full EP on first unlock
                if (shouldUnlockEvolution)
                {
                    int maxEpForPlayer = startingPlayerState.IsFirst ? 2 : 3; // P1 gets 2 EP, P2 gets 3 EP
                                                                              // Use PlayerState domain property names
                    startingPlayerState.MaxEvolutionPoints = maxEpForPlayer;
                    startingPlayerState.CurrentEvolutionPoints = maxEpForPlayer; // Grant full EP immediately on unlock
                    _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} evolution UNLOCKED (Turn {PlayerTurn}). Granted {MaxEP} EP.",
                       gameState.GameId, startingPlayerState.PlayerId, currentPlayerTurn, maxEpForPlayer);
                }
                // Note: EP does NOT auto-recover after unlock - only consumed through evolution

                // Use PlayerState domain property names
                if (startingPlayerState.CurrentEvolutionPoints != previousEP)
                {
                    // M7 V1.1 Change: Removed context.AddEvent for EpUpdateEventDto
                    _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} EP changed. New EP: {CurrentEP}/{MaxEP}.", // Removed "Event added."
                      gameState.GameId, startingPlayerState.PlayerId, startingPlayerState.CurrentEvolutionPoints, startingPlayerState.MaxEvolutionPoints);
                } // <<< Closing brace for the if block

                // --- M7 Design Doc Step 5.3.1: Evolution Eligibility Check ---
                // Player can evolve if they have unlocked evolution and have EP available
                // Evolution eligibility is now determined by game rules, not stored in PlayerState
                bool canEvolveThisTurn = CanPlayerEvolve(startingPlayerState);
                _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} can evolve this turn: {CanEvolve} (MaxEP: {MaxEP}, CurrentEP: {CurrentEP}).",
                    gameState.GameId, startingPlayerState.PlayerId, canEvolveThisTurn, startingPlayerState.MaxEvolutionPoints, startingPlayerState.CurrentEvolutionPoints);

                // --- M7 Design Doc Step 5.4: Draw Card(s) at Start of Turn ---
                // Determine number of cards to draw (1 normally, 2 for P2T1)
                // --- M7 Design Doc Step 5.4: Draw Card(s) ---
                int cardsToDraw = 1;
                // Check if it's the second player's *very first* turn (TurnNumber will be 1 after increment)
                if (!startingPlayerState.IsFirst && currentPlayerTurn == 1)
                {
                    cardsToDraw = 2; // Second player draws 2 on their first turn
                    _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} is second player on their first turn. Drawing {DrawCount} cards.",
                        gameState.GameId, startingPlayerState.PlayerId, cardsToDraw);
                }
                else
                {
                    _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} drawing {DrawCount} card(s) for turn {PlayerTurn}.",
                        gameState.GameId, startingPlayerState.PlayerId, cardsToDraw, currentPlayerTurn);
                }
                // --- Roo Debug: Log before DrawCards ---
                _logger.LogWarning("[DEBUG_DRAW:{GameId}] Before calling DrawCards for Player {PlayerId}. Requesting {DrawCount} cards. Current Deck Count: {DeckCount}",
                    gameState.GameId, startingPlayerState.PlayerId, cardsToDraw, startingPlayerState.Deck.Count);
                // --- End Roo Debug ---
                // 使用PlayerState领域方法处理抽卡
                var drawnCards = startingPlayerState.DrawCards(cardsToDraw);
                // TODO: 在未来版本中，这些事件应该被添加到事件队列中进行处理
                // 目前暂时跳过事件处理，直接使用领域方法的状态修改结果

                // --- Roo Debug: Log after DrawCards ---
                _logger.LogWarning("[DEBUG_DRAW:{GameId}] After calling DrawCards for Player {PlayerId}. Drawn cards count: {DrawnCount}. New Deck Count: {DeckCount}",
                    gameState.GameId, startingPlayerState.PlayerId, drawnCards.Count, startingPlayerState.DeckCount);
                // Generate events related to the card draw:
                // - HandUpdateEventDto: Notifies the owner about the new hand state, including the drawn card(s).
                // - DeckUpdateEventDto: Notifies both players about the change in deck size.
                // - CardDrawnEventDto: Notifies both players about each specific card drawn (for animations/triggers).
                // --- End Roo Debug ---
                // Generate events for drawn cards (HandUpdate, DeckUpdate, CardDrawn)
                if (drawnCards.Any())
                {
                    // M7 V1.1 Change: Removed context.AddEvent for HandUpdate, DeckUpdate, CardDrawn events
                    _logger.LogDebug("[GameService:{GameId}] Hand/Deck updated after drawing {DrawnCount} cards.", // Simplified log
                        gameState.GameId, drawnCards.Count);
                }

                // --- M7 Design Doc Step 5.5: Reset Unit States (Updated based on BUG-017 analysis/design doc) ---
                _logger.LogDebug("[GameService:{GameId}] Resetting unit states for Player {PlayerId} at start of turn.", gameState.GameId, startingPlayerState.PlayerId);

                foreach (var unit in startingPlayerState.Field)
                {
                    // Use simple unit name since we removed pre-fetching
                    string unitName = $"Unit (ID: {unit.DefinitionId})";

                    // Units already on the field at the start of the turn should be able to attack
                    // (unless affected by a status effect later). Reset flags explicitly here.
                    // Summoning sickness is handled by units starting with CanAttack=false when played (unless Rush/Storm).
                    unit.CanAttack = true;          // Reset attack permission
                    unit.AttacksThisTurn = 0; // Reset attack count for the new turn
                    _logger.LogTrace("[GameService:{GameId}] Reset state for unit {UnitName} ({UnitInstanceId}): CanAttack=True, AttacksMade=0",
                        gameState.GameId, unitName, unit.InstanceId);
                }
                _logger.LogDebug("[GameService:{GameId}] Finished resetting CanAttack and AttacksThisTurn flags for Player {PlayerId}'s {UnitCount} units.",
                    gameState.GameId, startingPlayerState.PlayerId, startingPlayerState.Field.Count); // Updated log message

                // M7 V1.1 Change: Removed context.AddEvent for TurnStartedEvent
                _logger.LogInformation("[GameService:{GameId}] StartOfTurn state updates complete for Player {PlayerId}, PlayerTurn {PlayerTurn}, Phase {Phase}.", // Simplified log
                    gameState.GameId, startingPlayerState.PlayerId, currentPlayerTurn, gameState.Phase);

                // --- End of logic adapted from ProcessStartOfTurn ---
            } // <<< Closing brace for the lock block
              // M8 Change: Method returns Task<bool>. Re-calculate IsAiTurn. Events collected in context.
              // M8 Change: isAiTurn is now assigned within the lock block. Remove recalculation here.
              // M7 V1.1 Change: Create and return the full game state DTO (Log message adjusted)
            _logger.LogInformation("[GS_SOT:{GameId}] Turn start processed for player {PlayerId}. Preparing full state DTO.", gameId, startingPlayerId);

            // M8 Change: Add null check for gameState declared outside lock and use it directly
            if (gameState == null) // Null check for the variable declared outside lock
            {
                _logger.LogError("[GS_SOT:{GameId}] Game state became null unexpectedly after lock for player {PlayerId}", gameId, startingPlayerId);
                // Throw exception as this indicates an internal error, the DTO cannot represent this failure.
                throw new InvalidOperationException($"Internal server error: Game state for game {gameId} became null unexpectedly after turn processing for player {startingPlayerId}.");
            }
            // Convert GameState entity to DTO for the event using AutoMapper
            var gameStateDto = _mapper.Map<GameStateDto>(gameState);
            var updateEvent = new TurnStartedEventDto(gameStateDto, startingPlayerId); // Create event with DTO

            _logger.LogDebug("[GameService:{GameId}] Returning TurnStartedEventDto for player {PlayerId}.",
                gameState.GameId, startingPlayerId);

            return await Task.FromResult(updateEvent); // Use Task.FromResult for async Task<T>
        } // <<< Closing brace for StartPlayerTurnAsync method
        /// <summary>
        /// 重构后的换牌方法 - 使用MulliganEffectHandler
        /// 保持GameService只负责验证、调度和队列管理职责
        /// </summary>
        private async Task SubmitMulliganAsync(string gameId, Guid playerId, List<Guid> cardUidsToReplace, GameActionContext context)
        {
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} attempting SubmitMulligan with {CardCount} cards.",
                gameId, playerId, cardUidsToReplace?.Count ?? 0);

            try
            {
                var parameters = new Dictionary<string, ValueConfig>
                {
                    { "playerId", ValueConfig.Static(playerId) },
                    { "cardUidsToReplace", ValueConfig.Static(cardUidsToReplace ?? new List<Guid>()) },
                    { "forceMulligan", ValueConfig.Static(false) },
                    { "mulliganReason", ValueConfig.Static("PlayerChoice") }
                };

                var events = await ExecuteEffectHandlerAsync(gameId, playerId, "mulligan", parameters, context);

                _logger.LogInformation("[GameService:{GameId}] Mulligan completed successfully for Player {PlayerId}. Generated {EventCount} events.",
                    gameId, playerId, events.Count);

                await Task.CompletedTask;
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning(ex, "[GameService:{GameId}] Game Logic Error during Mulligan for Player {PlayerId}", gameId, playerId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Unexpected error during Mulligan for Player {PlayerId}", gameId, playerId);
                throw new GameLogicException("An unexpected error occurred during mulligan submission.", ex);
            }
        }

        /// <summary>
        /// Retrieves the current game state domain entity for the specified game ID.
        /// <summary>
        /// Retrieves the current game state domain entity for the specified game ID.
        /// Does not create a new game if the state does not exist.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <returns>The GameState domain entity if found, otherwise null.</returns>
        public async Task<GameState?> GetGameStateAsync(string gameId)
        {
            _logger.LogDebug("[GameService] Attempting to get game state for Game {GameId}.", gameId);

            // Use Domain Entity type in TryGetValue
            if (_gameStates.TryGetValue(gameId, out GameState? existingState))
            {
                _logger.LogDebug("[GameService] Found existing game state for Game {GameId}.", gameId);
                // Wrap synchronous result in a completed task.
                return await Task.FromResult(existingState); // Return existing domain entity
            }
            else
            {
                _logger.LogDebug("[GameService] No existing game state found for Game {GameId}.", gameId);
                // Return a completed task with a null result.
                return await Task.FromResult<GameState?>(null); // Return nullable Domain Entity
            }
        }
        // --- End Added GetGameStateAsync Implementation ---

        // --- Refactored to GetOrCreateGameStateAsync ---
        /// <summary>
        /// Gets the current game state domain entity for the given game ID, or creates a new one if it doesn't exist.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <param name="requestingPlayerId">The ID of the player requesting the game state (used for game creation).</param>
        /// <returns>The GameState domain entity for the game, or null if creation failed unexpectedly.</returns>
        public async Task<GameState?> GetOrCreateGameStateAsync(string gameId, Guid? requestingPlayerId = null) // Return Domain Entity
        {
            _logger.LogInformation("[GameService] Attempting to get or create game state for Game {GameId}.", gameId);

            // Use Domain Entity type in TryGetValue
            if (_gameStates.TryGetValue(gameId, out GameState? existingState)) // Use Domain Entity
            {
                _logger.LogInformation("[GameService] Found existing game state for Game {GameId}.", gameId);
                return existingState;
            }

            _logger.LogInformation("[GameService] No existing game state found for Game {GameId}. Creating new state.", gameId);

            // --- New Game State Creation Logic ---
            try
            {
                // Use the requesting player ID if provided, otherwise generate new GUIDs
                Guid player1Id = requestingPlayerId ?? Guid.NewGuid(); // Use requesting player as Player 1
                Guid player2Id = Guid.NewGuid(); // AI player (Player 2)

                // Use GameDataProvider cache instead of database access
                var allCardDefinitions = _gameDataProvider.GetAllCardDefinitions().Values.ToList();
                _logger.LogInformation("[GameService:{GameId}] Fetched {Count} total card definitions from cache.", gameId, allCardDefinitions.Count);

                // Check if any definitions were found
                if (allCardDefinitions == null || !allCardDefinitions.Any())
                {
                    _logger.LogError("[GameService:{GameId}] No card definitions found in the repository. Cannot create game state.", gameId);
                    return null; // Cannot proceed without cards
                }

                // --- Fixed Test Deck Creation Logic ---
                int targetDeckSize = 20; // Set target deck size to 20 for testing
                List<CardInstance> player1Deck = new List<CardInstance>(targetDeckSize); // Changed type to Domain Entity
                List<CardInstance> player2Deck = new List<CardInstance>(targetDeckSize); // Changed type to Domain Entity

                // Create a lookup dictionary for efficient access to definitions
                var definitionLookup = allCardDefinitions.ToDictionary(def => def.Id);

                // --- Roo Add: Fetch Leader Definition (Using actual Leader ID 0) ---
                const int leaderCardId = GameConstants.Cards.LeaderCardId;
                if (!definitionLookup.TryGetValue(leaderCardId, out var leaderCardDef))
                {
                    _logger.LogError("[GameService:{GameId}] Leader card definition (ID: {LeaderId}) not found. Cannot create game state.", gameId, leaderCardId);
                    return null; // Cannot proceed without leader definition
                }
                _logger.LogInformation("[GameService:{GameId}] Found placeholder leader definition: {LeaderName} ({LeaderId})", gameId, leaderCardDef.Name, leaderCardDef.Id);
                // --- End Roo Add ---

                // Validate if all required definitions were fetched (using integer IDs)
                var missingDefs = _fixedTestDeckCardIds.Distinct().Where(id => !definitionLookup.ContainsKey(id)).ToList();
                if (missingDefs.Any())
                {
                    _logger.LogError("[GameService:{GameId}] Missing required card definitions for test deck (Integers): {MissingIds}. Cannot create game state.", gameId, string.Join(", ", missingDefs));
                    return null; // Cannot proceed
                }

                // Build decks using the fixed list of integer card IDs
                foreach (var cardIdInt in _fixedTestDeckCardIds) // Use integer ID directly
                {
                    // Use integer ID directly for lookup
                    if (definitionLookup.TryGetValue(cardIdInt, out var cardDef))
                    {
                        // Create unique CardInstanceDto instances for each player's deck
                        // Create CardInstance domain entity for player 1
                        var instance1 = new CardInstance(Guid.NewGuid(), cardDef.Id, player1Id)
                        {
                            CardType = cardDef.Type ?? string.Empty, // Use CardType property
                            CurrentZone = CardZone.Deck,
                            BaseCost = cardDef.Cost,
                            CurrentCost = cardDef.Cost,
                            BaseAttack = cardDef.Attack ?? 0,
                            CurrentAttack = cardDef.Attack ?? 0,
                            BaseHealth = cardDef.Health ?? 0,
                            CurrentHealth = cardDef.Health ?? 0,
                            MaxHealth = cardDef.Health ?? 0,
                            IsEvolved = false,
                            CanAttack = false, // Cannot attack from deck
                                               // Removed CanEvolve - evolution eligibility is determined by game rules
                            AttacksThisTurn = 0, // Initialize attack count
                            Countdown = null, // Initialize countdown
                            FieldPosition = null // Initialize field position
                        };
                        instance1.SetBaseKeywords(cardDef.Keywords ?? Enumerable.Empty<string>()); // Use helper to set keywords
                        player1Deck.Add(instance1);

                        // Create CardInstance domain entity for player 2
                        var instance2 = new CardInstance(Guid.NewGuid(), cardDef.Id, player2Id)
                        {
                            CardType = cardDef.Type ?? string.Empty, // Use CardType property
                            CurrentZone = CardZone.Deck,
                            BaseCost = cardDef.Cost,
                            CurrentCost = cardDef.Cost,
                            BaseAttack = cardDef.Attack ?? 0,
                            CurrentAttack = cardDef.Attack ?? 0,
                            BaseHealth = cardDef.Health ?? 0,
                            CurrentHealth = cardDef.Health ?? 0,
                            MaxHealth = cardDef.Health ?? 0,
                            IsEvolved = false,
                            CanAttack = false, // Cannot attack from deck
                                               // Removed CanEvolve - evolution eligibility is determined by game rules
                            AttacksThisTurn = 0, // Initialize attack count
                            Countdown = null, // Initialize countdown
                            FieldPosition = null // Initialize field position
                        };
                        instance2.SetBaseKeywords(cardDef.Keywords ?? Enumerable.Empty<string>()); // Use helper to set keywords
                        player2Deck.Add(instance2);
                    }
                    // else: Error already logged if definition was missing
                }

                _logger.LogInformation("[GameService:{GameId}] Created fixed test decks (Size: {DeckSize}) for Player 1 and Player 2 using predefined list.",
                    gameId, player1Deck.Count); // Use Count which should be 20

                // Shuffle the newly created decks
                ShuffleDeck(player1Deck);
                ShuffleDeck(player2Deck);

                // Draw initial hands from the shuffled decks
                int initialHandSize = 3;
                List<CardInstance> player1Hand = DrawInitialHand(player1Deck, initialHandSize); // Changed type to Domain Entity
                List<CardInstance> player2Hand = DrawInitialHand(player2Deck, initialHandSize); // Changed type to Domain Entity
                                                                                                // Deck count in PlayerStateDto will be updated later when assigning the deck

                // Decide first player randomly
                bool player1IsFirst = new Random().Next(0, 2) == 0;
                Guid firstPlayerId = player1IsFirst ? player1Id : player2Id;
                Guid secondPlayerId = player1IsFirst ? player2Id : player1Id;

                // --- Roo Add: Create Leader Card Instances ---
                // Create CardInstance domain entity for player 1's leader
                var player1LeaderInstance = new CardInstance(Guid.NewGuid(), leaderCardDef.Id, player1Id)
                {
                    CardType = leaderCardDef.Type ?? string.Empty, // Use CardType property
                    CurrentZone = CardZone.Leader,
                    BaseCost = leaderCardDef.Cost,
                    CurrentCost = leaderCardDef.Cost,
                    BaseAttack = leaderCardDef.Attack ?? 0,
                    CurrentAttack = leaderCardDef.Attack ?? 0,
                    BaseHealth = leaderCardDef.Health ?? 0,
                    CurrentHealth = leaderCardDef.Health ?? 0, // Leader health tracked separately in PlayerState
                    MaxHealth = leaderCardDef.Health ?? 0,
                    IsEvolved = false,
                    CanAttack = false, // Leaders don't attack
                                       // Removed CanEvolve - evolution eligibility is determined by game rules
                    AttacksThisTurn = 0,
                    Countdown = null,
                    FieldPosition = null
                };
                player1LeaderInstance.SetBaseKeywords(leaderCardDef.Keywords ?? Enumerable.Empty<string>());

                // Create CardInstance domain entity for player 2's leader
                var player2LeaderInstance = new CardInstance(Guid.NewGuid(), leaderCardDef.Id, player2Id)
                {
                    CardType = leaderCardDef.Type ?? string.Empty, // Use CardType property
                    CurrentZone = CardZone.Leader,
                    BaseCost = leaderCardDef.Cost,
                    CurrentCost = leaderCardDef.Cost,
                    BaseAttack = leaderCardDef.Attack ?? 0,
                    CurrentAttack = leaderCardDef.Attack ?? 0,
                    BaseHealth = leaderCardDef.Health ?? 0,
                    CurrentHealth = leaderCardDef.Health ?? 0, // Leader health tracked separately in PlayerState
                    MaxHealth = leaderCardDef.Health ?? 0,
                    IsEvolved = false,
                    CanAttack = false, // Leaders don't attack
                                       // Removed CanEvolve - evolution eligibility is determined by game rules
                    AttacksThisTurn = 0,
                    Countdown = null,
                    FieldPosition = null
                };
                player2LeaderInstance.SetBaseKeywords(leaderCardDef.Keywords ?? Enumerable.Empty<string>());
                // --- End Roo Add ---

                // Create PlayerState domain entities
                // Create PlayerState domain entities using the correct constructor and properties
                var player1State = new PlayerState(player1Id)
                {
                    IsAI = false, // Assuming player 1 is human
                    MaxPlayPoints = 0,
                    CurrentPlayPoints = 0,
                    MaxEvolutionPoints = player1IsFirst ? 2 : 3,
                    CurrentEvolutionPoints = 0,
                    // Removed CanEvolve - evolution eligibility is determined by game rules
                    Hand = player1Hand, // Assign List<CardInstance>
                    Deck = player1Deck, // Assign List<CardInstance>
                                        // Field, Graveyard, Extra are initialized by constructor
                    IsFirst = player1IsFirst,
                    PlayedCardCountThisTurn = 0,
                    TurnNumber = 0,
                    Leader = player1LeaderInstance, // Assign CardInstance
                    HasCompletedMulligan = false,
                    HasTakenFirstTurn = false
                };

                var player2State = new PlayerState(player2Id)
                {
                    IsAI = true, // Assuming player 2 is AI
                    MaxPlayPoints = 0,
                    CurrentPlayPoints = 0,
                    MaxEvolutionPoints = player1IsFirst ? 3 : 2,
                    CurrentEvolutionPoints = 0,
                    // Removed CanEvolve - evolution eligibility is determined by game rules
                    Hand = player2Hand, // Assign List<CardInstance>
                    Deck = player2Deck, // Assign List<CardInstance>
                                        // Field, Graveyard, Extra are initialized by constructor
                    IsFirst = !player1IsFirst,
                    PlayedCardCountThisTurn = 0,
                    TurnNumber = 0,
                    Leader = player2LeaderInstance, // Assign CardInstance
                    HasCompletedMulligan = false,
                    HasTakenFirstTurn = false
                };

                // Player decks are now stored directly in PlayerStateDto.Deck

                // Create the GameState domain entity using the correct constructor
                var newGameState = new GameState(gameId, player1State, player2State, firstPlayerId)
                {
                    // Constructor handles initial setup for GameId, Player1, Player2, FirstPlayerId, CurrentPlayerId, Status, Phase, TurnNumber
                    // We might set an initial message if needed
                    GameMessage = "Game created. Waiting for players."
                    // Status and Phase are set by the constructor
                    // WinnerId starts as null implicitly
                };

                // _logger.LogInformation("[GameService:{GameId}] >>> Attempting to add newly created game state to dictionary...", gameId); // REMOVED DIAGNOSTIC LOG
                // ADDED: Log initial hand details for player 1 before adding state
                try // Added try-catch for safety during logging complex objects
                {
                    // Log only InstanceId and DefinitionId available directly on CardInstance to avoid repo lookup in loop
                    string player1HandDetails = string.Join(", ", player1State.Hand.Select(c => $"{{InstanceId='{c.InstanceId}', DefId='{c.DefinitionId}'}}"));
                    _logger.LogInformation("[GameService:{GameId}:GetOrCreate] Player 1 Initial Hand prepared for state: [{Player1HandDetails}]", gameId, player1HandDetails);
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "[GameService:{GameId}:GetOrCreate] Error occurred while logging player 1 hand details.", gameId);
                }
                // END ADDED logging block
                bool addedSuccessfully = _gameStates.TryAdd(gameId, newGameState);
                // _logger.LogInformation("[GameService:{GameId}] <<< Add attempt finished. Success: {WasSuccess}", gameId, addedSuccessfully); // REMOVED DIAGNOSTIC LOG
                if (addedSuccessfully) // Use helper variable
                {
                    _logger.LogInformation("[GameService] Successfully created and stored new game state for Game {GameId}.", gameId);
                    // TODO: Add logic to associate player connections with this gameId/group
                    // await _hubContext.Groups.AddToGroupAsync(player1ConnectionId, $"Game_{gameId}");
                    // await _hubContext.Groups.AddToGroupAsync(player2ConnectionId, $"Game_{gameId}");
                    return newGameState;
                }
                else
                {
                    _logger.LogWarning("[GameService] Failed to add newly created game state for Game {GameId} to dictionary. Race condition?", gameId);
                    // Attempt to retrieve again in case another thread added it between TryGetValue and TryAdd
                    if (_gameStates.TryGetValue(gameId, out var racingState))
                    {
                        _logger.LogInformation("[GameService] Retrieved state added by concurrent operation for Game {GameId}.", gameId);
                        return racingState;
                    }
                    else
                    {
                        _logger.LogError("[GameService] Failed to store or retrieve game state for Game {GameId} after creation attempt.", gameId);
                        return null; // Indicate failure
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Unexpected error during game state creation for Game {GameId}. Exception Details: {ExceptionString}", gameId, ex.ToString()); // <-- 使用 ex.ToString()
                                                                                                                                                                                  // Return null to indicate failure.
                return null;
            }
        } // End of GetOrCreateGameStateAsync method

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

        // Static keyword arrays for performance optimization
        private static readonly string[] TargetingKeywords = {
            "deal", "damage", "destroy", "target", "enemy", "ally", "follower",
            "banish", "transform", "give", "restore", "heal"
        };

        private static readonly string[] GlobalEffectKeywords = {
            "all", "each", "random", "draw", "summon", "gain", "your", "both"
        };

        /// <summary>
        /// Determines if a spell card requires a target based on its definition
        /// TODO: Replace with proper ability system integration
        /// </summary>
        /// <param name="cardDefinition">The spell card definition to check</param>
        /// <returns>True if the spell requires a target, false otherwise</returns>
        private bool RequiresTarget(CardDefinition cardDefinition)
        {
            try
            {
                // Check if the card has targeting keywords in its description or abilities
                var description = cardDefinition.Description?.ToLowerInvariant() ?? "";
                var name = cardDefinition.Name?.ToLowerInvariant() ?? "";

                // Check if any targeting keywords are present
                bool hasTargetingKeywords = TargetingKeywords.Any(keyword =>
                    description.Contains(keyword) || name.Contains(keyword));

                // Check for global/area effect keywords
                bool hasGlobalEffects = GlobalEffectKeywords.Any(keyword =>
                    description.Contains(keyword) || name.Contains(keyword));

                // If it has targeting keywords but no global effect keywords, it likely requires a target
                bool requiresTarget = hasTargetingKeywords && !hasGlobalEffects;

                _logger.LogDebug("[GameService] Spell {SpellName} target requirement analysis: HasTargeting={HasTargeting}, HasGlobal={HasGlobal}, RequiresTarget={RequiresTarget}",
                    cardDefinition.Name, hasTargetingKeywords, hasGlobalEffects, requiresTarget);

                return requiresTarget;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Error determining target requirement for spell {SpellName}",
                    cardDefinition.Name);
                // Fail safe - assume no target required to avoid blocking spell play
                return false;
            }
        }




        /// <summary>
        /// Executes spell effects through the event-driven ability system
        /// </summary>
        /// <param name="gameState">Current game state</param>
        /// <param name="cardDefinition">The spell card definition</param>
        /// <param name="targetEntity">The target entity (can be null for non-targeted spells)</param>
        /// <param name="casterPlayerState">The player casting the spell</param>
        /// <param name="context">Game action context for event generation</param>
        private async Task ExecuteSpellEffectsAsync(GameState gameState, CardDefinition cardDefinition, ICard? targetEntity, PlayerState casterPlayerState, GameActionContext context)
        {
            try
            {
                _logger.LogDebug("[GameService:{GameId}] Executing spell effects for {SpellName} ({SpellId}) through event system",
                    gameState.GameId, cardDefinition.Name, cardDefinition.Id);

                // Create scope for dependency injection
                using var scope = _serviceScopeFactory.CreateScope();
                var eventProcessor = scope.ServiceProvider.GetRequiredService<IEventProcessor>();

                // Create a spell cast event that will trigger abilities
                var spellCardInstance = new CardInstance(
                    instanceId: Guid.NewGuid(),
                    definitionId: cardDefinition.Id,
                    ownerPlayerId: casterPlayerState.PlayerId
                );

                // Populate spell card properties
                spellCardInstance.CardType = cardDefinition.Type ?? "Spell";
                // Note: CardInstance doesn't have Name/Description properties
                // These are stored in the CardDefinition and accessed via DefinitionId

                // Create spell cast event
                var playerStateDto = _mapper.Map<PlayerStateDto>(casterPlayerState);
                var spellCastEvent = new PlayCardEvent(playerStateDto, spellCardInstance);

                // Create game context for ability processing
                var gameContext = new GameContext(gameState, casterPlayerState.PlayerId, _mapper, spellCastEvent, spellCardInstance);

                // Process the event through the event processor to trigger abilities
                var processorResult = await eventProcessor.ProcessEventAsync(spellCastEvent, gameContext);

                // Add generated events to context
                if (processorResult.ResultingDtos != null)
                {
                    context.Events.AddRange(processorResult.ResultingDtos);
                }

                _logger.LogDebug("[GameService:{GameId}] Spell effects processed through event system. Generated {EventCount} events",
                    gameState.GameId, processorResult.ResultingDtos?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Error executing spell effects for {SpellName}",
                    gameState.GameId, cardDefinition.Name);
                // Don't rethrow - spell effects failure shouldn't break spell play
            }
        }

        /// <summary>
        /// Processes clash effects when two units engage in combat through the event system
        /// </summary>
        /// <param name="gameState">Current game state</param>
        /// <param name="attacker">The attacking unit</param>
        /// <param name="defender">The defending unit</param>
        /// <param name="context">Game action context for event generation</param>
        private async Task ProcessClashEffectsAsync(GameState gameState, CardInstance attacker, CardInstance defender, GameActionContext context)
        {
            try
            {
                _logger.LogDebug("[GameService:{GameId}] Processing clash effects between {AttackerInstanceId} and {DefenderInstanceId}",
                    gameState.GameId, attacker.InstanceId, defender.InstanceId);

                // Create scope for dependency injection
                using var scope = _serviceScopeFactory.CreateScope();
                var eventProcessor = scope.ServiceProvider.GetRequiredService<IEventProcessor>();

                // Create proper ClashEvent for ability processing
                var clashEvent = new ClashEvent(attacker, defender);

                // Create game context for ability processing
                var gameContext = new GameContext(gameState, attacker.OwnerPlayerId, _mapper, clashEvent);

                // Process the clash event through the event processor to trigger abilities
                var processorResult = await eventProcessor.ProcessEventAsync(clashEvent, gameContext);

                // Add generated events to context
                if (processorResult.ResultingDtos != null)
                {
                    context.Events.AddRange(processorResult.ResultingDtos);
                }

                _logger.LogDebug("[GameService:{GameId}] ClashEvent processed. Generated {EventCount} events",
                    gameState.GameId, processorResult.ResultingDtos?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Error processing clash effects between {AttackerInstanceId} and {DefenderInstanceId}",
                    gameState.GameId, attacker.InstanceId, defender.InstanceId);
                // Don't rethrow - clash effects failure shouldn't break combat
            }
        }



        /// <summary>
        /// Prepares hand update information for later broadcasting (synchronous version for use within locks)
        /// </summary>
        /// <param name="gameId">Game identifier</param>
        /// <param name="playerId">Player identifier</param>
        /// <param name="hand">Player's current hand</param>
        /// <param name="context">Game action context to add events to</param>
        private void PrepareHandUpdateEvent(string gameId, Guid playerId, List<CardInstance> hand, GameActionContext context)
        {
            try
            {
                _logger.LogDebug("[GameService:{GameId}] Preparing hand update for Player {PlayerId} with {CardCount} cards",
                    gameId, playerId, hand.Count);

                // Convert hand to DTOs for client communication
                var handDtos = hand.Select(card => _mapper.Map<CardInstanceDto>(card)).ToList();

                // Create deck update event (since HandUpdate is deprecated, we use DeckUpdate for hand count changes)
                // Note: This is a workaround since HandUpdate is deprecated
                var deckUpdateEvent = new DeckUpdateEventDto(playerId, hand.Count);

                // Add the event to context for later broadcasting outside the lock
                context.Events.Add(deckUpdateEvent);

                _logger.LogDebug("[GameService:{GameId}] Hand update event prepared for Player {PlayerId}",
                    gameId, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Error preparing hand update for Player {PlayerId}",
                    gameId, playerId);
                // Don't rethrow - hand update failure shouldn't break the game flow
            }
        }

        /// <summary>
        /// Triggers end-of-turn effects for the specified player (synchronous version for use within locks)
        /// </summary>
        /// <param name="gameState">Current game state</param>
        /// <param name="endingPlayerState">Player whose turn is ending</param>
        /// <param name="context">Game action context for event generation</param>
        private void TriggerEndOfTurnEffects(GameState gameState, PlayerState endingPlayerState, GameActionContext context)
        {
            _logger.LogDebug("[GameService:{GameId}] Triggering end-of-turn effects for Player {PlayerId}",
                gameState.GameId, endingPlayerState.PlayerId);

            try
            {
                // Create end-of-turn event using the correct class from CoreGameEvents
                var playerStateDto = _mapper.Map<PlayerStateDto>(endingPlayerState);
                var endOfTurnEvent = new TurnEndEvent(playerStateDto);

                // Add the event to context for processing outside the lock
                // Note: Complex async processing should be handled by the caller outside the lock
                context.Events.Add(new TurnEndedEventDto(endingPlayerState.PlayerId,
                    gameState.GetOpponentPlayerState(endingPlayerState.PlayerId)?.PlayerId ?? Guid.Empty));

                _logger.LogDebug("[GameService:{GameId}] End-of-turn effects processed synchronously",
                    gameState.GameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Error triggering end-of-turn effects for Player {PlayerId}",
                    gameState.GameId, endingPlayerState.PlayerId);
                // Don't rethrow - end-of-turn effects failure shouldn't break the turn transition
            }
        }

        /// <summary>
        /// Triggers end-of-turn effects for the specified player (async version for use outside locks)
        /// </summary>
        /// <param name="gameState">Current game state</param>
        /// <param name="endingPlayerState">Player whose turn is ending</param>
        /// <param name="context">Game action context for event generation</param>
        private async Task TriggerEndOfTurnEffectsAsync(GameState gameState, PlayerState endingPlayerState, GameActionContext context)
        {
            _logger.LogDebug("[GameService:{GameId}] Triggering end-of-turn effects for Player {PlayerId}",
                gameState.GameId, endingPlayerState.PlayerId);

            try
            {
                // Create scope for dependency injection
                using var scope = _serviceScopeFactory.CreateScope();
                var abilityManager = scope.ServiceProvider.GetRequiredService<AbilityManager>();
                var eventProcessor = scope.ServiceProvider.GetRequiredService<IEventProcessor>();

                // Create end-of-turn event using the correct class from CoreGameEvents
                var playerStateDto = _mapper.Map<PlayerStateDto>(endingPlayerState);
                var endOfTurnEvent = new TurnEndEvent(playerStateDto);

                // Create game context for ability processing
                var gameContext = new GameContext(gameState, endingPlayerState.PlayerId, _mapper, endOfTurnEvent);

                // Process the event through the event processor to trigger abilities
                var processorResult = await eventProcessor.ProcessEventAsync(endOfTurnEvent, gameContext);

                // Add generated events to context
                if (processorResult.ResultingDtos != null)
                {
                    context.Events.AddRange(processorResult.ResultingDtos);
                }

                _logger.LogDebug("[GameService:{GameId}] End-of-turn effects processed. Generated {EventCount} events",
                    gameState.GameId, processorResult.ResultingDtos?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Error triggering end-of-turn effects for Player {PlayerId}",
                    gameState.GameId, endingPlayerState.PlayerId);
                // Don't rethrow - end-of-turn effects failure shouldn't break the turn transition
            }
        }

        /// <summary>
        /// 重构后的攻击方法 - 使用AttackEffectHandler
        /// 保持GameService只负责验证、调度和队列管理职责
        /// </summary>
        private async Task<bool> AttackAsync(string gameId, Guid playerId, AttackActionDto actionDto, GameActionContext context)
        {
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} executing Attack: Attacker={AttackerUid}, Target={TargetUid}.",
                gameId, playerId, actionDto.AttackerUid, actionDto.TargetUid);



            try
            {
                // 确定攻击目标类型
                var targetType = AttackTargetType.Unit; // 默认攻击单位
                if (_gameStates.TryGetValue(gameId, out var gameState))
                {
                    var opponentPlayer = gameState.GetOpponentPlayerState(playerId);
                    if (opponentPlayer != null && actionDto.TargetUid == opponentPlayer.PlayerId)
                    {
                        targetType = AttackTargetType.Player;
                    }
                }

                var parameters = new Dictionary<string, ValueConfig>
                {
                    { "attackerInstanceId", ValueConfig.Static(actionDto.AttackerUid) },
                    { "targetId", ValueConfig.Static(actionDto.TargetUid) },
                    { "targetType", ValueConfig.Static(targetType) },
                    { "forceAttack", ValueConfig.Static(false) },
                    { "attackReason", ValueConfig.Static("PlayerAction") }
                };

                var events = await ExecuteEffectHandlerAsync(gameId, playerId, "attack", parameters, context);

                _logger.LogInformation("[GameService:{GameId}] Attack completed successfully for Player {PlayerId}. Generated {EventCount} events.",
                    gameId, playerId, events.Count);

                return true;
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning(ex, "[GameService:{GameId}] Game Logic Error during Attack for Player {PlayerId}", gameId, playerId);
                throw; // Let the Hub handle sending error to client
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Unexpected error during Attack for Player {PlayerId}", gameId, playerId);
                throw new GameLogicException("An unexpected error occurred during the attack.", ex); // Wrap for Hub
            }
        } // End of AttackAsync method

        /// <summary>
        /// Determines if a player can evolve units this turn based on game rules.
        /// </summary>
        /// <param name="playerState">The player state to check</param>
        /// <returns>True if the player can evolve units this turn</returns>
        private bool CanPlayerEvolve(PlayerState playerState)
        {
            // Player can evolve if they have unlocked evolution and have EP available
            return playerState.MaxEvolutionPoints > 0 && playerState.CurrentEvolutionPoints > 0;
        }
        /// <summary>
        /// 重构后的进化方法 - 使用EvolveEffectHandler
        /// 保持GameService只负责验证、调度和队列管理职责
        /// </summary>
        private async Task EvolveAsync(string gameId, Guid playerId, EvolveActionDto actionDto, GameActionContext context)
        {
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} attempting Evolve: Unit={UnitUID}",
                gameId, playerId, actionDto.UnitUid);

            try
            {
                var parameters = new Dictionary<string, ValueConfig>
                {
                    { "unitInstanceId", ValueConfig.Static(actionDto.UnitUid) },
                    { "forceEvolve", ValueConfig.Static(false) },
                    { "evolveReason", ValueConfig.Static("PlayerAction") }
                };

                var events = await ExecuteEffectHandlerAsync(gameId, playerId, "evolve", parameters, context);

                _logger.LogInformation("[GameService:{GameId}] Evolve completed successfully for Player {PlayerId}. Generated {EventCount} events.",
                    gameId, playerId, events.Count);

                await Task.CompletedTask;
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning(ex, "[GameService:{GameId}] Game Logic Error during Evolve for Player {PlayerId}", gameId, playerId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Unexpected error during Evolve for Player {PlayerId}", gameId, playerId);
                throw new GameLogicException("An unexpected error occurred during unit evolution.", ex);
            }
        }
        /// <summary>
        /// Checks if a card is currently playable by a player, considering cost, field space, and pre-play conditions.
        /// Also provides information about potential effect targets for UI purposes.
        /// </summary>
        /// <param name="gameId">The ID of the game.</param>
        /// <param name="playerId">The ID of the player attempting to play the card.</param>
        /// <param name="cardInstanceId">The InstanceId of the card in the player's hand.</param>
        /// <returns>A DTO containing playability status and target information.</returns>

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
                if (Enum.TryParse<CardType>(cardDefinition.Type, true, out var cardType) && cardType == CardType.Follower)
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
                    .Where(ac => ac.Trigger.EventTypes.Contains(GameEventType.CardPlayed))
                    .ToList();

                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var conditionEvaluator = scope.ServiceProvider.GetRequiredService<IConditionEvaluator>();
                    var gameContext = new GameContext(gameState, playerId, _mapper, null, cardInHand); // TriggeringEvent is null for checks

                    foreach (var config in playTriggerConfigs)
                    {
                        foreach (var condition in config.PreConditions)
                        {
                            if (!await conditionEvaluator.EvaluateAsync(condition, gameContext))
                            {
                                result.IsPlayable = false;
                                result.FailureReason = $"打出前置条件未满足: {condition.Name ?? condition.Expression}";
                                _logger.LogInformation("[CheckPlayabilityCore:{GameId}] PrePlay condition failed for Card {CardInstanceId}: {ConditionExpression}", gameId, cardInstanceId, condition.Expression);
                                return result;
                            }
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
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var targetSelectionService = scope.ServiceProvider.GetRequiredService<TargetSelectionService>();
                        var gameContext = new GameContext(gameState, playerId, _mapper, null, cardInHand);

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
                                        var validTargets = await targetSelectionService.SelectTargetsAsync(effect.Target, gameContext);

                                        if (validTargets.Any())
                                        {
                                            result.HasValidTargets = true;
                                            validTargetIdSet = validTargets.Select(t => t.Id).ToHashSet();
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
                var gameState = await GetGameStateAsync(gameId);
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
                var gameState = await GetGameStateAsync(gameId);
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
                    // Use the client method name defined in IGameClient (e.g., ReceiveHandPlayabilityUpdate)
                    // Using string literal as ClientMethodNames class/enum was not found
                    // Get the IClientProxy for the specific user and call the SendAsync extension method
                    var clientProxy = _hubContext.Clients.User(playerId.ToString());
                    await clientProxy.SendAsync("ReceiveHandPlayabilityUpdate", updateDto);
                    _logger.LogInformation("[GameService:{GameId}] Sent hand playability update to Player {PlayerId} with {Count} results.", gameId, playerId, playabilityResults.Count);
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

        /// <param name="drawCount">The number of cards to draw.</param>
        /// <param name="context">The game action context for generating events.</param>
        /// <returns>A list of cards drawn.</returns>
        private List<CardInstance> DrawCards(PlayerState playerState, int drawCount, GameActionContext? context = null)
        {
            _logger.LogDebug("[GameService] Player {PlayerId} attempting to draw {DrawCount} cards. Deck size: {DeckSize}",
                playerState.PlayerId, drawCount, playerState.Deck.Count);

            List<CardInstance> drawnCards = new();
            for (int i = 0; i < drawCount; i++)
            {
                if (playerState.Deck.Count > 0)
                {
                    CardInstance drawnCard = playerState.Deck[0];
                    drawnCards.Add(drawnCard);
                    playerState.Deck.RemoveAt(0);

                    // Generate CardDrawn event if context is provided
                    if (context != null)
                    {
                        // Use correct property name: DefinitionId
                        context.Events.Add(new CardDrawnEventDto(playerState.PlayerId, drawnCard.InstanceId.ToString(), drawnCard.DefinitionId.ToString()));
                        _logger.LogDebug("[GameService] Generated CardDrawnEvent for CardInstance ({CardInstanceId})",
                            drawnCard.InstanceId);
                    }
                }
                else
                {
                    _logger.LogWarning("[GameService] Attempted to draw from empty deck for Player {PlayerId}.", playerState.PlayerId);
                    break;
                }
            }

            // DeckCount is read-only, no need to set it manually
            playerState.Hand.AddRange(drawnCards);

            // Generate DeckUpdate event if context is provided and cards were drawn
            if (context != null && drawnCards.Count > 0)
            {
                context.Events.Add(new DeckUpdateEventDto(playerState.PlayerId, playerState.DeckCount));
                _logger.LogDebug("[GameService] Generated DeckUpdateEvent for Player {PlayerId}. New deck size: {DeckSize}",
                    playerState.PlayerId, playerState.DeckCount);
            }

            _logger.LogDebug("[GameService] Player {PlayerId} drew {ActualDrawCount} cards. New hand size: {HandSize}, New deck size: {DeckSize}",
                 playerState.PlayerId, drawnCards.Count, playerState.Hand.Count, playerState.DeckCount);
            return drawnCards;
        }


        /// <summary>
        /// Processes end-of-turn effects for the current player.
        /// Placeholder: Currently only sets HasTakenFirstTurn.
        /// </summary>
        /// <param name="currentPlayerState">The state of the player whose turn is ending.</param>
        /// <param name="gameState">The current game state.</param>
        /// <returns>A list of game events generated during end-of-turn processing.</returns>
        /// <summary>
        /// Processes end-of-turn effects for the current player.
        /// Based on M7 Design Doc section 3.1, Step 4.
        /// </summary>
        /// <param name="endingPlayerState">The state of the player whose turn is ending.</param>
        /// <param name="gameState">The current game state.</param>
        /// <returns>A list of events generated during end-of-turn processing.</returns>
        // Changed DTOs to Domain Entities
        private void ProcessEndOfTurn(PlayerState endingPlayerState, GameState gameState, GameActionContext context)
        {
            // Removed TurnNumber from log
            _logger.LogInformation("[GameService:{GameId}] Processing EndOfTurn for Player {PlayerId}.",
                gameState.GameId, endingPlayerState.PlayerId);


            // M7 Design Doc - Step 4.1: Update HasTakenFirstTurn
            if (!endingPlayerState.HasTakenFirstTurn)
            {
                endingPlayerState.HasTakenFirstTurn = true;
                _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} marked as having taken their first turn.", gameState.GameId, endingPlayerState.PlayerId);
            }

            // M7 Design Doc - Step 4.2: Trigger "End of your turn" effects
            // Note: End-of-turn effects are processed synchronously to avoid lock issues
            // Complex async effects should be handled outside the lock context
            TriggerEndOfTurnEffects(gameState, endingPlayerState, context);

            // M7 Design Doc - Step 4.3: Reset Attack Counts/States for player's units
            foreach (var unit in endingPlayerState.Field) // Use domain entity Field
            {
                // Use correct property name: AttacksThisTurn
                unit.AttacksThisTurn = 0; // Reset attack count on domain entity
                                          // Reset CanAttack unless the unit has Storm (Storm allows attacking the turn it's played AND subsequent turns)
                                          // Rush only allows attack on the turn it's played. Non-Rush/Storm units regain CanAttack at start of their turn.
                                          // For simplicity now, we only reset the count. CanAttack logic handled in ProcessStartOfTurn.
                                          // unit.CanAttack = unit.Keywords.Contains("Storm", StringComparer.OrdinalIgnoreCase); // Reset CanAttack based on Storm status? Let's handle CanAttack reset in StartOfTurn.
            }
            _logger.LogDebug("[GameService:{GameId}] Reset attack counts for Player {PlayerId}'s units.", gameState.GameId, endingPlayerState.PlayerId);


            // M7 Design Doc - Step 4.4: Generate TurnEndedEvent
            // Use helper method to get opponent state from domain GameState
            var opponentPlayerState = gameState.GetOpponentPlayerState(endingPlayerState.PlayerId);
            if (opponentPlayerState == null)
            {
                _logger.LogError("[GameService:{GameId}] Critical: Could not find opponent for Player {PlayerId} during ProcessEndOfTurn.", gameState.GameId, endingPlayerState.PlayerId);
                // In a real scenario, might need more robust error handling, but for now, let the caller handle potential issues.
                // Returning empty list might mask the problem. Throwing might be better if state is inconsistent.
                throw new GameLogicException($"Opponent state inconsistent for player {endingPlayerState.PlayerId} in game {gameState.GameId}.");
            }
            _logger.LogWarning("[GS_DEBUG_EOT:{GameId}] Preparing TurnEndedEvent. endingPlayerId={EndingPlayer}, nextPlayerId={NextPlayer}", gameState.GameId, endingPlayerState.PlayerId, opponentPlayerState.PlayerId); // DEBUG LOG

            // Global turn number removed. TurnEndedEvent now only needs player IDs.
            // Removed: int endingTurnNumber = gameState.TurnNumber;
            // Use PlayerId from domain entities for the DTO
            context.Events.Add(new TurnEndedEventDto(endingPlayerState.PlayerId, opponentPlayerState.PlayerId)); // M8 Change: Use context
                                                                                                                 // Removed endingTurnNumber and {TurnNumber} from log as it no longer exists
            _logger.LogInformation("[GameService:{GameId}] Generated TurnEndedEvent. Next Player: {NextPlayerId}.",
                gameState.GameId, opponentPlayerState.PlayerId);


            _logger.LogInformation("[GameService:{GameId}] EndOfTurn processing complete for Player {PlayerId}.", gameState.GameId, endingPlayerState.PlayerId); // M8 Change: Removed event count and return
                                                                                                                                                                 // M8 Change: Removed return events;
        } // End of ProcessEndOfTurn (Replacement)

        /// <summary>
        /// Processes start-of-turn effects for the player whose turn is beginning.
        /// Handles PP update, card draw (including P2 first turn bonus), EP update, game state updates,
        /// resetting flags/unit states, and other start-of-turn triggers based on M7 Design Doc section 3.1, Step 5.
        /// </summary>
        /// <param name="startingPlayerState">The state of the player whose turn is starting.</param>
        /// <param name="gameState">The current game state. This method will modify it.</param>
        /// <param name="isSecondPlayerFirstTurn">Indicates if this is the second player's very first turn (for draw 2 rule).</param>
        /// <returns>A list of game events generated during start-of-turn processing.</returns>

        /// <summary>
        /// Creates a new CardInstance domain entity from a CardDefinition.
        /// </summary>
        /// <param name="cardDef">The static card definition.</param>
        /// <param name="ownerPlayerId">The ID of the player who owns this instance.</param>
        /// <param name="initialZone">The initial zone for the card instance.</param>
        /// <returns>A new CardInstance object.</returns>
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
                CardType = cardDef.Type ?? string.Empty,
                CurrentZone = initialZone,
                CurrentCost = cardDef.Cost,
                CurrentAttack = cardDef.Attack ?? 0, // Initialize current stats from base
                CurrentHealth = cardDef.Health ?? 0,
                MaxHealth = cardDef.Health ?? 0,   // Initialize max health from base
                IsEvolved = false,                  // Starts unevolved
                CanAttack = false,                  // Default CanAttack state (adjust based on zone/keywords if needed)
                                                    // Removed CanEvolve - evolution eligibility is determined by game rules
                AttacksThisTurn = 0,
                Countdown = null,                   // Initialize Countdown (relevant for amulets)
                FieldPosition = null                // Initialize FieldPosition
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

        /// <summary>
        /// Reassigns field positions for all units on a player's field.
        /// This should be called after units are removed from the field to maintain correct positions.
        /// </summary>
        /// <param name="playerState">The player whose field positions need to be updated</param>
        /// <param name="gameId">Game ID for logging</param>
        private void ReassignFieldPositions(PlayerState playerState, string gameId)
        {
            _logger.LogDebug("[GameService:{GameId}] Reassigning field positions for Player {PlayerId}. Field size: {FieldSize}",
                gameId, playerState.PlayerId, playerState.Field.Count);

            for (int i = 0; i < playerState.Field.Count; i++)
            {
                var unit = playerState.Field[i];
                var oldPosition = unit.FieldPosition;
                unit.FieldPosition = i;

                if (oldPosition != i)
                {
                    _logger.LogTrace("[GameService:{GameId}] Updated field position for unit {InstanceId}: {OldPosition} -> {NewPosition}",
                        gameId, unit.InstanceId, oldPosition, i);
                }
            }

            _logger.LogDebug("[GameService:{GameId}] Field position reassignment completed for Player {PlayerId}",
                gameId, playerState.PlayerId);
        }

        /// <summary>
        /// Assigns field position to a newly added unit.
        /// </summary>
        /// <param name="cardInstance">The card instance to assign position to</param>
        /// <param name="playerState">The player state containing the field</param>
        /// <param name="gameId">Game ID for logging</param>
        private void AssignFieldPosition(CardInstance cardInstance, PlayerState playerState, string gameId)
        {
            // Assign position based on current index in field
            var position = playerState.Field.IndexOf(cardInstance);
            if (position >= 0)
            {
                cardInstance.FieldPosition = position;
                _logger.LogDebug("[GameService:{GameId}] Assigned field position {FieldPosition} to unit {InstanceId} for Player {PlayerId}",
                    gameId, position, cardInstance.InstanceId, playerState.PlayerId);
            }
            else
            {
                _logger.LogWarning("[GameService:{GameId}] Could not find unit {InstanceId} in Player {PlayerId}'s field to assign position",
                    gameId, cardInstance.InstanceId, playerState.PlayerId);
            }
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

        // M8 Change: Signature updated to use GameActionContext
        private void ApplyDamage(ICard targetEntity, int damage, GameActionContext context, Guid sourcePlayerId, Guid targetPlayerId, Guid? sourceUid, string damageType)
        {
            // Check for null or non-positive damage first
            if (targetEntity == null || damage <= 0) return; // Corrected <=

            // Determine target UID for logging/events and get original health
            Guid eventTargetUid;
            int originalHealth;
            int newHealth;
            int changeAmount;

            if (targetEntity is CardInstanceDto cardInstance)
            {
                eventTargetUid = cardInstance.InstanceId;
                originalHealth = cardInstance.CurrentHealth; // Use CurrentHealth for CardInstanceDto
                _logger.LogTrace("[GameService:ApplyDamage] Target is CardInstanceDto. Using InstanceId '{InstanceId}'. Original Health: {OriginalHealth}", eventTargetUid, originalHealth);

                // Apply damage to CardInstanceDto
                cardInstance.CurrentHealth -= damage;
                newHealth = cardInstance.CurrentHealth;
            }
            else if (targetEntity is PlayerStateDto playerState && playerState.LeaderCard != null)
            {
                // Target is PlayerStateDto, use LeaderCard's InstanceId and Health
                eventTargetUid = playerState.LeaderCard.InstanceId;
                originalHealth = playerState.LeaderCard.CurrentHealth; // Use LeaderCard.CurrentHealth
                _logger.LogTrace("[GameService:ApplyDamage] Target is PlayerStateDto. Using LeaderCard InstanceId '{InstanceId}'. Original Health: {OriginalHealth}", eventTargetUid, originalHealth);

                // Apply damage to LeaderCard
                playerState.LeaderCard.CurrentHealth -= damage;
                newHealth = playerState.LeaderCard.CurrentHealth;
            }
            else
            {
                // Fallback/Error case: Log warning and do not apply damage if type is unexpected or LeaderCard is null
                eventTargetUid = targetEntity.Uid; // Use original Uid as fallback for logging
                originalHealth = targetEntity.Health; // Use original Health as fallback for logging
                _logger.LogWarning("[GameService:ApplyDamage] Target entity type is unexpected ({EntityType}) or LeaderCard is null for PlayerStateDto. Damage not applied. Fallback UID: '{FallbackUid}'. Original Health: {OriginalHealth}", targetEntity.GetType().Name, eventTargetUid, originalHealth);
                return; // Do not proceed with damage application or event generation for unexpected types
            }

            changeAmount = newHealth - originalHealth;

            // Log damage application (using eventTargetUid)
            _logger.LogDebug("[GameService] Applying {Damage} damage to Entity {TargetUid} (Owned by {TargetPlayerId}) from Source {SourceUid} (Player {SourcePlayerId}). Type: {DamageType}. Original Health: {OriginalHealth}, New Health: {NewHealth}",
                damage, eventTargetUid, targetPlayerId, sourceUid.ToString() ?? "Unknown", sourcePlayerId, damageType, originalHealth, newHealth);

            // Generate events (using eventTargetUid and correct newHealth/changeAmount)
            context.Events.Add(new DamageDealtEventDto(sourcePlayerId, eventTargetUid, damage, targetPlayerId, sourceUid, damageType));
            context.Events.Add(new HealthChangedEventDto(targetPlayerId, eventTargetUid, newHealth, changeAmount));

            _logger.LogDebug("[GameService] Entity {TargetUid} health changed to {NewHealth}. Change: {ChangeAmount}.",
               eventTargetUid, newHealth, changeAmount);
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
            // Use domain entity property: Type (string)
            if (!Enum.TryParse<CardType>(cardDefinition.Type, true, out var cardType))
            {
                throw new GameLogicException($"Unknown card type: {cardDefinition.Type}");
            }

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
                CurrentCost = cardDefinition.Cost,
                CurrentAttack = cardDefinition.Attack ?? 0,
                CurrentHealth = cardDefinition.Health ?? 0,
                MaxHealth = cardDefinition.Health ?? 0,
                IsEvolved = false,
                Countdown = null, // Followers don't have countdown
                FieldPosition = null, // Position is set when added to field list
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

        // 已删除：ExecuteFollowerPlay方法 - 已被PlayCardEffectHandler替代

        /// <summary>
        /// Public method to explicitly trigger the execution of an AI player's turn.
        /// Called by the Hub. Wraps the internal execution logic.
        /// </summary>
        // Made private as per queue design (AI turn triggered differently now)
        private async Task TriggerAiTurnAsync(string gameId, Guid aiPlayerId)
        {
            _logger.LogInformation("[GameService:{GameId}] TriggerAiTurnAsync called for AI Player {PlayerId}.", gameId, aiPlayerId);
            // We await the internal method to handle potential exceptions during setup/execution,
            // even though the caller (Hub) might use fire-and-forget for this trigger method.
            await ExecuteAiActionPhaseAsync(gameId, aiPlayerId);
            _logger.LogDebug("[GameService:{GameId}] TriggerAiTurnAsync finished for AI Player {PlayerId}.", gameId, aiPlayerId); // Added Debug log
        }
        // --- AI Turn Execution Logic ---

        /// <summary>
        /// Executes the AI's turn asynchronously. Triggered by EndTurnAsync when it's the AI's turn.
        /// This method runs fire-and-forget and should handle its own scope for dependencies.
        /// </summary>
        /// <param name="gameId">The ID of the game.</param>
        /// <param name="aiPlayerId">The ID of the AI player whose turn it is.</param>
        /// <param name="previousPlayerId">The ID of the player whose turn just ended.</param> // Roo Add: Parameter doc
        // --- START AI TURN EXECUTION LOGIC (REPLACEMENT TARGET) ---
        // Roo Modify: Make previousPlayerId optional
        private async Task ExecuteAiActionPhaseAsync(string gameId, Guid aiPlayerId, string? previousPlayerId = null)
        {
            // Log which trigger context we are in (if previousPlayerId is provided, it's likely from EndTurn, otherwise StartTurn)
            string triggerContext = previousPlayerId == null ? "ProcessStartOfTurn" : "EndTurnAsync (Opponent)";
            _logger.LogInformation("[AI_TURN:{GameId}:{PlayerId}] Executing turn via AiTurnExecutor (Triggered by: {Context}). Previous Player: {PrevPlayer}",
                gameId, aiPlayerId, triggerContext, previousPlayerId ?? "N/A");
            // Create a scope to resolve the Scoped AiTurnExecutor
            using var scope = _serviceProvider.CreateScope();
            var aiTurnExecutorInstance = scope.ServiceProvider.GetRequiredService<AiTurnExecutor>();
            _logger.LogDebug("[AI_TURN:{GameId}:{PlayerId}] Resolved AiTurnExecutor instance within scope.", gameId, aiPlayerId);

            // --- AI Start-of-Turn Logic REMOVED ---
            // StartPlayerTurnAsync is now called externally before this method.
            // Call the AI execution logic using the resolved instance
            _logger.LogInformation("[AI_TURN:{GameId}:{PlayerId}] Executing AI's main turn actions.", gameId, aiPlayerId);
            // M8 Step 6: Create context, pass it, and broadcast after
            var context = new GameActionContext();
            _logger.LogDebug("[AI_TURN:{GameId}:{PlayerId}] Created GameActionContext for AI turn.", gameId, aiPlayerId);
            await aiTurnExecutorInstance.ExecuteTurnAsync(gameId, aiPlayerId, context); // Pass context
            _logger.LogInformation("[AI_TURN:{GameId}:{PlayerId}] AiTurnExecutor (resolved via scope) completed successfully. Broadcasting {EventCount} collected events.", gameId, aiPlayerId, context.Events.Count);

            // --- BUG-011 DEBUG LOG 2/3 REMOVED (Contained reference to HandUpdateEventDto) ---

            await BroadcastEventsAsync(gameId, context.Events); // Broadcast collected events

            // --- START: Logic to start next human player's turn ---
            _logger.LogInformation("[AI_TURN:{GameId}:{PlayerId}] AI turn execution complete. Checking if next player's turn should start.", gameId, aiPlayerId);
            try
            {
                var gameState = await GetGameStateAsync(gameId); // Use existing helper
                if (gameState == null)
                {
                    _logger.LogWarning("[AI_TURN:{GameId}:{PlayerId}] Could not retrieve game state after AI turn. Cannot start next turn.", gameId, aiPlayerId);
                    return; // Exit if game state is gone
                }

                if (gameState.IsGameOver)
                {
                    _logger.LogInformation("[AI_TURN:{GameId}:{PlayerId}] Game is over after AI turn. Not starting next turn.", gameId, aiPlayerId);
                    return; // Exit if game ended during AI turn
                }

                // Use helper method
                var nextPlayer = gameState.GetOpponentPlayerState(aiPlayerId);

                if (nextPlayer != null && !nextPlayer.IsAI)
                {
                    // --- BUG-010 DIAGNOSTIC LOGGING START ---
                    _logger.LogWarning("[BUG-010_DIAG:{GameId}] Condition met: Next player ({NextPlayerId}) is Human (IsAI={IsAI}). Preparing to call StartPlayerTurnAsync.",
                        gameId, nextPlayer.PlayerId, nextPlayer.IsAI);
                    // Log relevant game state details before calling StartPlayerTurnAsync
                    string gameStateDetails = $"GameOver={gameState?.IsGameOver}, CurrentPlayer={gameState?.CurrentPlayerId}"; // Add null checks
                    _logger.LogWarning("[BUG-010_DIAG:{GameId}] GameState before StartPlayerTurnAsync call: {GameStateDetails}", gameId, gameStateDetails);
                    // --- END BUG-010 DIAGNOSTIC LOGGING ---

                    _logger.LogInformation("[AI_TURN:{GameId}:{PlayerId}] Next player ({NextPlayerId}) is human. Starting their turn.", gameId, aiPlayerId, nextPlayer.PlayerId); // Keep original log
                                                                                                                                                                                  // Start the human player's turn
                                                                                                                                                                                  // M8 Change: Create context, pass it, remove tuple deconstruction, capture bool result
                                                                                                                                                                                  // M7 V1.1 Change: Removed context creation
                    _logger.LogDebug("[AI_TURN:{GameId}:{PlayerId}] Preparing to start human player {NextPlayerId}'s turn.", gameId, aiPlayerId, nextPlayer.PlayerId);
                    // M7 V1.1 Change: Call new signature, get DTO
                    TurnStartedEventDto humanTurnStateEvent = await StartPlayerTurnAsync(gameId, nextPlayer.PlayerId);
                    // Determine AI status from DTO if needed for logging/logic
                    bool isNextTurnAiResult = humanTurnStateEvent.GameState.Players.FirstOrDefault(p => p.PlayerId == humanTurnStateEvent.GameState.CurrentPlayerId)?.IsAI ?? false;

                    // --- BUG-010 DIAGNOSTIC LOGGING START ---
                    // M7 V1.1 Change: Update log to use derived boolean result and indicate DTO return
                    _logger.LogWarning("[BUG-010_DIAG:{GameId}] StartPlayerTurnAsync call completed. Returned IsAI={IsAiTurn}, Returned TurnStartedEventDto",
                        gameId, isNextTurnAiResult);
                    // --- END BUG-010 DIAGNOSTIC LOGGING ---


                    // Broadcast the TurnStartedEventDto containing the full state for the human player's turn
                    if (humanTurnStateEvent != null) // Add null check for safety
                    {
                        _logger.LogInformation("[AI_TURN:{GameId}:{PlayerId}] Broadcasting TurnStartedEventDto for start of human player {NextPlayerId}'s turn.", gameId, aiPlayerId, nextPlayer.PlayerId);
                        await BroadcastEventAsync(gameId, humanTurnStateEvent); // Broadcast the specific DTO containing the full state
                    }
                    else // Added else block for clarity if DTO was null
                    {
                        _logger.LogWarning("[AI_TURN:{GameId}:{PlayerId}] humanTurnStateEvent was null after StartPlayerTurnAsync. Cannot broadcast turn start state.", gameId, aiPlayerId);
                    }
                } // <<< Corrected: This brace closes the 'if (nextPlayer != null && !nextPlayer.IsAI)' block
                else if (nextPlayer == null) // <<< Corrected: 'else if' now correctly follows the 'if'
                {
                    _logger.LogWarning("[AI_TURN:{GameId}:{PlayerId}] Could not find opponent player after AI turn. Cannot start next turn.", gameId, aiPlayerId);
                }
                else // nextPlayer is AI // <<< Corrected: 'else' now correctly follows the 'else if'
                {
                    _logger.LogInformation("[AI_TURN:{GameId}:{PlayerId}] Next player ({NextPlayerId}) is AI. Not starting their turn here (should be handled by their own trigger).", gameId, aiPlayerId, nextPlayer.PlayerId);
                }
            } // <<< Corrected: This brace now correctly closes the 'try' block starting at line 2129
            catch (Exception postAiEx)
            {
                _logger.LogError(postAiEx, "[AI_TURN:{GameId}:{PlayerId}] Error during post-AI turn processing (starting next human turn).", gameId, aiPlayerId);
                // Decide if this error should affect game state or just be logged. Logging for now.
            }
            // --- END: Logic to start next human player's turn ---
        } // <<< END ExecuteAiTurnInternalAsync method - Catch block removed as inner try-catch handles post-AI errors

        /// <summary>
        /// Broadcasts a list of game events to all clients connected to the specified game's group.
        /// Loops through the list and calls the single-event broadcast helper.
        /// </summary>
        /// <param name="gameId">The ID of the game instance.</param>
        /// <param name="events">The list of game events to broadcast.</param>
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

            // --- BUG-011 DETAILED BROADCAST PREP LOG REMOVED (Contained reference to HandUpdateEventDto) ---


            // --- BUG-011 DEBUG LOG 3/3 ---
            if (gameEvent is PpUpdateEventDto ppEvt)
            {
                {
                    // Format string manually to bypass potential compiler issue with structured logging access
                    string logMessage = $"[BUG-011 DEBUG 3/3:{gameId}] Broadcasting PpUpdateEvent for Player {ppEvt.SourcePlayerId}. PP: {ppEvt.CurrentPp}/{ppEvt.MaxPp}.";
                    _logger.LogWarning(logMessage);
                }
            }
            // Removed HandUpdateEventDto check from BUG-011 DEBUG 3/3
            // --- END BUG-011 DEBUG LOG ---

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

    } // Closing brace for GameService class
} // Closing brace for ShadowverseBeyond.Server.Services namespace
