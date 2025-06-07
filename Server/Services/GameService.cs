// Server/Services/GameService.cs
//
// 相关文档:
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 回合管理系统: 文档/05-游戏系统/03-回合管理系统.md
// - 战斗系统: 文档/05-游戏系统/02-战斗系统.md
// - 卡牌出牌系统: 文档/05-游戏系统/04-卡牌出牌系统.md
//
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Hubs; // GameHub
using ShadowverseBeyond.Server.Hubs.ClientInterfaces; // IGameClient
using ShadowverseBeyond.Server.Dtos.SignalR;        // DTOs (Restored for GameEventDto base class)
// Removed: using ShadowverseBeyond.Server.Dtos.SignalR;        // DTOs (Caused Ambiguity)
using ShadowverseBeyond.Server.Dtos.Enums; // Add using for CardType enum
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
using ShadowverseBeyond.Server.Models; // Added for GameActionContext
using ShadowverseBeyond.Server.Services.Abstractions; // Added for IClientMethodMapper
using ShadowverseBeyond.Server.Domain.Abilities; // Added for ability system
using ShadowverseBeyond.Server.Domain.Abilities.Common; // Added for AbilityType enum
using ShadowverseBeyond.Server.Domain.Events; // Added for event system
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // For ConditionConfig
using ShadowverseBeyond.Server.Domain.Abilities.Targets;    // For TargetConfig
using ShadowverseBeyond.Server.Domain.Abilities.Effects;    // For EffectConfig
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;   // For TriggerConfig

namespace ShadowverseBeyond.Server.Services
{
    public class GameService : IGameService
    {
        private readonly IHubContext<GameHub, IGameClient> _hubContext;
        private readonly ILogger<GameService> _logger;

        // Removed EventBroadcastHandler delegate and _eventHandlers dictionary

        // Remove Scoped ICardRepository injection from Singleton GameService constructor
        // private readonly ICardRepository _cardRepository;
        private readonly IServiceScopeFactory _serviceScopeFactory; // Inject IServiceScopeFactory instead
        private readonly IAiService _aiService; // Added for AI logic
        private readonly IServiceProvider _serviceProvider; // Inject IServiceProvider to resolve Scoped dependencies

        // Central Game State Storage
        private readonly ConcurrentDictionary<string, GameStateDto> _gameStates = new();
        // Lock objects per game to prevent concurrent modification of a single game's state
        private readonly ConcurrentDictionary<string, object> _gameLocks = new();
        // Ability managers per game for handling card abilities
        private readonly ConcurrentDictionary<string, AbilityManager> _abilityManagers = new();
        // Flag to prevent re-entrant EndTurn processing for the same game - REMOVED by Roo
        // private readonly ConcurrentDictionary<string, bool> _isProcessingEndTurn = new();
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
        // Inject IClientMethodMapper
        public GameService(IHubContext<GameHub, IGameClient> hubContext, ILogger<GameService> logger, IServiceScopeFactory serviceScopeFactory, IAiService aiService, IServiceProvider serviceProvider, IClientMethodMapper clientMethodMapper)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _clientMethodMapper = clientMethodMapper ?? throw new ArgumentNullException(nameof(clientMethodMapper)); // Assign mapper

            // Removed _eventHandlers dictionary initialization
        }

        // Updated PlayCardAsync implementation
        // M8 Change: Signature updated to use GameActionContext and return Task
        public async Task<bool> PlayCardAsync(string gameId, string playerId, PlayCardActionDto actionDto, GameActionContext context)
        { // Start of PlayCardAsync body (replacement for line 64 onwards)
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} attempting PlayCard: CardUID={CardUID}, TargetUID={TargetUID}.",
                gameId, playerId, actionDto.CardUid, actionDto.TargetUid ?? "None");

            // M8 Change: Removed local events list creation

            // Step 1: Fetch card data asynchronously before acquiring the main lock
            CardDefinition? cardDefinition = null;
            CardInstanceDto? cardInHand = null; // Changed type from CardDto?
            try
            {
                _logger.LogWarning("--- PlayCardAsync TRYING to await FetchCardDataAsync ---"); // Roo Added Log
                (cardDefinition, cardInHand) = await FetchCardDataAsync(gameId, playerId, actionDto.CardUid);
                _logger.LogWarning("--- PlayCardAsync SUCCESSFULLY awaited FetchCardDataAsync ---"); // Roo Added Log
                // FetchCardDataAsync throws if card/definition not found
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning(ex, "[GameService:{GameId}:PlayCard] Validation error during async data fetch for Player {PlayerId}, CardUID {CardUID}.", gameId, playerId, actionDto.CardUid);
                throw; // Re-throw validation errors
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}:PlayCard] Unexpected error during async data fetch for Player {PlayerId}, CardUID {CardUID}.", gameId, playerId, actionDto.CardUid);
                throw new GameLogicException("An unexpected error occurred while preparing to play the card.", ex); // Wrap unexpected errors
            }

            // Ensure cardDefinition and cardInHand are not null after fetch attempt (should have thrown if failed)
            if (cardDefinition == null || cardInHand == null)
            {
                _logger.LogError("[GameService:{GameId}:PlayCard] Critical: CardDefinition or CardInHand is null after successful fetch block for Player {PlayerId}, CardUID {CardUID}. This should not happen.", gameId, playerId, actionDto.CardUid);
                throw new GameLogicException("Internal error retrieving card data.");
            }


            // Step 2: Acquire lock and perform synchronous game state modifications
            object gameLock = _gameLocks.GetOrAdd(gameId, _ => new object());
            lock (gameLock)
            {
                try
                {
                    // a. Retrieve current game and player state inside lock
                    if (!_gameStates.TryGetValue(gameId, out var gameState))
                    {
                        throw new GameLogicException($"Game not found: {gameId} (inside lock)");
                    }
                    var playerState = gameState.Players.FirstOrDefault(p => p.PlayerId == playerId);
                    if (playerState == null)
                    {
                        throw new GameLogicException($"Player state error in game {gameId} (inside lock).");
                    }

                    // b. Validate preconditions (turn, phase, PP, field space, card still in hand)
                    ValidatePlayCardPreconditions(gameState, playerState, cardDefinition, cardInHand, actionDto, playerId);

                    // c. Apply cost and remove card from hand
                    // M8 Change: Pass context instead of events
                    ApplyPlayCardCostAndRemoveCard(context, gameId, playerState, cardInHand, cardDefinition); // Pass gameId

                    // d. Execute card type specific logic
                    if (!Enum.TryParse<CardType>(cardDefinition.Type, true, out var cardType))
                    {
                        throw new GameLogicException($"Unknown card type: {cardDefinition.Type}");
                    }

                    // Process card abilities and events (will be handled after the lock)
                    // Store card information for ability processing outside the lock

                    switch (cardType)
                    {
                        case CardType.Follower:
                            // Refactored: Call new method, pass playerId
                            var cardInstance = CreateCardInstanceForBattle(cardDefinition, playerState.PlayerId);
                            // M8 Change: Pass context instead of events
                            // Refactored: Pass cardInstance instead of unitState
                            ExecuteFollowerPlay(context, gameState, playerState, cardInstance, cardInHand, actionDto);
                            break;
                        case CardType.Spell:
                            // M8 Change: Pass context instead of events
                            ExecuteSpellPlay(context, gameState, playerState, cardInHand, cardDefinition, actionDto);
                            break;
                        case CardType.Amulet:
                            // Updated Call: Pass gameState, playerState, cardInHand, actionDto, events
                            // M8 Change: Pass context instead of events
                            ExecuteAmuletPlay(context, gameState, playerState, cardInHand, actionDto);
                            break;
                        default:
                            throw new GameLogicException($"Unsupported card type to play: {cardType}");
                    }

                    _logger.LogInformation("[GameService:{GameId}:PlayCard] Successfully processed PlayCard for Player {PlayerId}, Card {CardName}.", gameState.GameId, playerId, cardDefinition.Name);
                }
                catch (GameLogicException ex)
                {
                    _logger.LogWarning(ex, "[GameService:{GameId}:PlayCard] Game Logic Error during PlayCard (inside lock) for Player {PlayerId}, Card {CardUID}.", gameId, playerId, actionDto.CardUid);
                    // Do not re-throw here, let the method return the events generated so far (if any)
                    // The calling Hub method should handle notifying the client if necessary based on the exception? Or should we add an error event?
                    // For now, let's assume the hub handles notifying based on the exception. If we re-throw, the lock might not be released correctly if not handled by Hub.
                    // Let's return normally. The Hub filter will catch the exception if it bubbles up.
                    // Best practice: Don't throw from within a lock unless absolutely necessary and handled carefully.
                    // Re-throw for now, HubExceptionFilter should handle it.
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[GameService:{GameId}:PlayCard] Unexpected error during PlayCard (inside lock) for Player {PlayerId}, Card {CardUID}.", gameId, playerId, actionDto.CardUid);
                    // Wrap and re-throw
                    throw new GameLogicException("An unexpected error occurred while playing the card.", ex);
                }
            } // End lock

            // Step 3: Process card abilities outside the lock
            await ProcessCardPlayAbilitiesAsync(gameId, cardDefinition, cardInHand, actionDto, context);

            // Step 4: Return collected events
            // M8 Change: Method now returns Task, no events list to return
            // Add await Task.CompletedTask to satisfy async Task return type
            return true;
        } // End of PlayCardAsync method body

        // Implement EndTurnAsync
        // Implement EndTurnAsync (Refactored based on M7 Design Doc - Step 1: Refactor method signature and core calls)
        // Note: Helper methods ProcessEndOfTurn and ProcessStartOfTurn will be added in subsequent steps.
        // Updated signature to return tuple Task<(List<GameEventDto> Events, bool AiTurnTriggered)>
        // Roo Change: Update return type to match the actual return value (List<GameEventDto>)
        // V2 Refactor: Signature changed to return tuple including IsNextPlayerAi and Events.
        // M8 Change: Signature updated to use GameActionContext and return Task
        public async Task EndTurnAsync(string gameId, string playerId, GameActionContext context) // Renamed endingPlayerId to playerId
        {
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} attempting EndTurn.", gameId, playerId);
            var events = new List<GameEventDto>();
            string? nextPlayerId = null; // Roo Add: Store next player ID for potential AI trigger outside lock
            string? resultPlayerId = null; // Variable to store result outside lock
            bool isNextPlayerAi = false; // Roo Add: Store AI status for trigger outside lock
            bool success = false; // Flag to indicate successful completion within lock

            // --- Re-entrancy Check REMOVED ---
            // Relying solely on lock(gameLock) for state modification safety.

            try // Main try block for EndTurn logic
            {
                // Acquire lock for this specific game
                object gameLock = _gameLocks.GetOrAdd(gameId, _ => new object());
                lock (gameLock)
                {
                    // 1. Validate gameId and retrieve GameState.
                    if (!_gameStates.TryGetValue(gameId, out var gameState))
                    {
                        throw new GameLogicException($"Game not found: {gameId}");
                    }

                    // 2. Validate Player's turn and game status.
                    if (gameState.IsGameOver)
                    {
                        throw new GameLogicException("Game is already over.");
                    }

                    // --- ADD DETAILED LOGGING BEFORE THE CHECK ---
                    _logger.LogWarning("[GameService:{GameId}] EndTurn CHECK: Received PlayerId={ReceivedPlayerId}, Server State CurrentPlayerId={ServerCurrentPlayerId}, Server State FirstPlayerId={ServerFirstPlayerId}, Phase={Phase}",
                        gameId,
                        playerId,                  // The ID received from the client request
                        gameState.CurrentPlayerId, // The ID currently stored in the server state
                        gameState.FirstPlayerId,   // Double-check who should be first
                                                   // Removed: gameState.TurnNumber,
                        gameState.Phase);          // Check phase as well
                    // --- END ADDED LOGGING ---

                    bool ignoreStaleRequest = false; // Flag to indicate stale request
                    if (gameState.CurrentPlayerId != playerId) // <<< The check that fails
                    {
                        // Log a warning instead of error for stale/duplicate EndTurn requests
                        _logger.LogWarning("[GameService:{GameId}] Received EndTurn request for player {ReceivedPlayerId} but current player is {ServerCurrentPlayerId}. Ignoring stale/duplicate request.",
                           gameId, playerId, gameState.CurrentPlayerId);
                        // --- ROO DEBUG: ADDING UNIQUE LOG BEFORE SETTING FLAG ---
                        _logger.LogError("!!!!!!!!!! ROO DEBUG: STALE EndTurn DETECTED for {PlayerId}. Setting ignoreStaleRequest = true. !!!!!!!!!!!", playerId);
                        // --- END ROO DEBUG ---
                        ignoreStaleRequest = true; // Set flag to ignore
                        // Removed: throw new GameLogicException(...)
                        // --- ADD EARLY RETURN FOR STALE REQUEST ---
                        // Roo Change: Return only events for stale request, matching new method signature Task<List<GameEventDto>>
                        var actualCurrentPlayerId = gameState.CurrentPlayerId;
                        var actualOpponent = gameState.Players.FirstOrDefault(p => p.PlayerId != actualCurrentPlayerId);
                        string? actualNextPlayerId = actualOpponent?.PlayerId;
                        bool actualIsNextPlayerAi = actualOpponent?.IsAI ?? false;
                        // V2 Change: Return tuple indicating failure but providing next player info for stale request.
                        string staleNextPlayerId = actualNextPlayerId ?? throw new GameLogicException($"Opponent not found for stale request in game {gameId}.");
                        _logger.LogDebug("[GameService:{GameId}] Stale request detected. Returning failure tuple with NextPlayerId='{StaleNextPlayerId}', IsAI={ActualIsNextPlayerAi}.", gameId, staleNextPlayerId, actualIsNextPlayerAi);
                        // M8 Fix: Method signature is Task, cannot return tuple. Just return early for stale request.
                        _logger.LogWarning("[GameService:{GameId}] Stale EndTurn request for Player {PlayerId} ignored. Returning early.", gameId, playerId); // Log added for clarity
                        return;
                        // --- END EARLY RETURN ---
                    }
                    else if (gameState.Phase == GamePhase.Mulligan) // Check phase only if player ID was valid
                    {
                        // Only throw if the player ID was correct but phase is wrong
                        throw new GameLogicException("Cannot end turn during Mulligan phase.");
                    }

                    // Only proceed with turn logic if the request wasn't flagged as stale/invalid
                    if (!ignoreStaleRequest)
                    {
                        // ===>>> ADDED CHECK: Re-verify current player *before* any processing within the !ignoreStaleRequest block <<<===
                        if (gameState.CurrentPlayerId != playerId)
                        {
                            // This should theoretically not happen if the check at line 243 worked and ignoreStaleRequest was handled,
                            // unless the state changed between line 243 and here despite the lock.
                            _logger.LogError("[GameService:{GameId}] CRITICAL STATE MISMATCH DETECTED INSIDE LOCK (within !ignoreStaleRequest)! Player {PlayerId} initiated EndTurn, but CurrentPlayerId is now {CurrentPlayer}. Aborting processing.",
                                gameId, playerId, gameState.CurrentPlayerId);
                            // Skip the entire turn processing logic below if mismatch detected here.
                        }
                        else
                        {
                            // --- Original processing logic starts here ---
                            // 3. Get current and opponent player states
                            var currentPlayerState = gameState.Players.FirstOrDefault(p => p.PlayerId == playerId);
                            var opponentPlayerState = gameState.Players.FirstOrDefault(p => p.PlayerId != playerId);

                            if (currentPlayerState == null || opponentPlayerState == null)
                            {
                                // Should not happen if game state is valid
                                _logger.LogError("[GameService:{GameId}] Critical error: Player state not found for player {PlayerId} or opponent.", gameId, playerId);
                                throw new GameLogicException($"Player state not found for game {gameId}.");
                            }

                            _logger.LogDebug("[GameService:{GameId}] Validation successful for EndTurn by {PlayerId}.", gameId, playerId);

                            // 4. Call ProcessEndOfTurn for the current player
                            _logger.LogWarning("[GS_DEBUG_ENDTURN:{GameId}] Lock acquired. Before ProcessEndOfTurn for {PlayerId}. Current gameState.CurrentPlayerId = {CurrentPlayer}", gameId, currentPlayerState.PlayerId, gameState.CurrentPlayerId); // DEBUG LOG
                            ProcessEndOfTurn(currentPlayerState, gameState, context); // M8 Change: Pass context, removed AddRange
                                                                                      // _logger.LogInformation("[GameService:{GameId}] Placeholder: Call to ProcessEndOfTurn for {PlayerId}.", gameId, endingPlayerId); // Original log


                            // Global turn increment logic and associated checks/logs removed.
                            // Player turn counts are handled individually within PlayerStateDto.TurnNumber
                            // and incremented inside ProcessStartOfTurn.

                            // Determine if it's the second player's very first turn (Needed for current ProcessStartOfTurn signature)
                            // Removed temporary check for isSecondPlayerFirstTurn as the parameter is being removed
                            // from ProcessStartOfTurn.

                            // Roo Change: Only process start of next turn here if the next player is NOT AI,
                            // or if the current ending player IS AI (AI ending its turn).
                            // The AI's start-of-turn is handled by ExecuteAiTurnInternalAsync.
                            nextPlayerId = opponentPlayerState.PlayerId; // Store next player ID regardless
                            isNextPlayerAi = opponentPlayerState.IsAI;   // Store AI status regardless

                            // Determine next player and AI status
                            nextPlayerId = opponentPlayerState.PlayerId;
                            isNextPlayerAi = opponentPlayerState.IsAI;

                            // Log completion of locked section
                            _logger.LogInformation("[GameService:{GameId}] EndTurn processing (locked section) completed for {PlayerId}.", gameId, playerId);
                            resultPlayerId = nextPlayerId; // Store for return outside lock
                            success = true; // Mark success
                        } // This closes the 'else' block from line 305
                    } // This closes the 'if (!ignoreStaleRequest)' block
                    // ===>>> END ADDED CHECK <<<=== // This comment might be slightly misplaced after revert, but harmless
                } // End lock block
            } // End try block for lock
            catch (GameLogicException ex) // Catch blocks restored after lock
            {
                _logger.LogWarning(ex, "[GameService:{GameId}] Game Logic Error during EndTurn (inside lock) for Player {PlayerId}.", gameId, playerId);
                // Re-throw for the Hub filter to handle
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Unexpected error during EndTurn (inside lock) for Player {PlayerId}.", gameId, playerId);
                // Wrap and re-throw
                throw new GameLogicException("An unexpected error occurred while ending the turn.", ex);
            }

            // --- Trigger Next Turn Start (REMOVED BY REVERT) ---
            // The logic to trigger the next turn (human or AI) outside the lock is removed.
            // The caller (Hub or Executor) will need to handle triggering the next turn based on the outcome (which is now implicit via Task completion).
            // This reverts to the state where the race condition existed.

            // Return the stored result tuple after the lock has been released
            _logger.LogDebug("[GameService:{GameId}] Exiting EndTurnAsync. Success: {Success}, IsNextAI: {IsNextAI}, NextPlayer: {NextPlayer}, EventCount: {EventCount}",
                gameId, success, isNextPlayerAi, resultPlayerId ?? "null", context.Events.Count); // Use context.Events.Count
            // Method returns Task. Add await Task.CompletedTask to satisfy CS1998 if no other await is hit.
            await Task.CompletedTask;
        } // This closes EndTurnAsync

        /// <summary>
        /// Processes card abilities after a card is played, outside of the main game state lock.
        /// This method handles the Event-driven ability system integration.
        /// </summary>
        /// <param name="gameId">The game ID.</param>
        /// <param name="cardDefinition">The definition of the card that was played.</param>
        /// <param name="cardInHand">The card instance that was played from hand.</param>
        /// <param name="actionDto">The play card action details.</param>
        /// <param name="context">The game action context for collecting events.</param>
        private async Task ProcessCardPlayAbilitiesAsync(string gameId, CardDefinition cardDefinition, CardInstanceDto? cardInHand, PlayCardActionDto actionDto, GameActionContext context)
        {
            if (cardInHand == null || cardDefinition == null)
            {
                _logger.LogDebug("[GameService:{GameId}] Skipping ability processing - null card data.", gameId);
                return;
            }

            try
            {
                // Get or create ability manager for this game
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
                var abilityManagerLogger = loggerFactory.CreateLogger<AbilityManager>();
                var abilityManager = _abilityManagers.GetOrAdd(gameId, _ => new AbilityManager(abilityManagerLogger));

                // Create a CardInstance for the ability system
                var cardInstance = new CardInstance(cardInHand.InstanceId.ToString(), cardDefinition.Id, cardInHand.ControllerPlayerId)
                {
                    CurrentZone = cardInHand.Zone,
                    CardType = cardDefinition.Type,
                    CurrentCost = cardDefinition.Cost,
                    CurrentAttack = cardDefinition.Attack ?? 0,
                    CurrentHealth = cardDefinition.Health ?? 0,
                    MaxHealth = cardDefinition.Health ?? 0,
                    OwnerPlayerId = cardInHand.ControllerPlayerId
                };

                // Parse card abilities from definition
                var abilityConfigs = ParseCardAbilitiesToConfigs(cardDefinition);
                if (abilityConfigs.Count > 0)
                {
                    var abilities = abilityManager.CreateAbilitiesForCard(cardInstance, abilityConfigs);

                    _logger.LogDebug("[GameService:{GameId}] Registered {AbilityCount} abilities for card {CardName}.",
                        gameId, abilities.Count, cardDefinition.Name);
                }

                // Create and process the PlayCard event
                var gameState = _gameStates.GetValueOrDefault(gameId);
                if (gameState != null)
                {
                    var playerState = gameState.Players.FirstOrDefault(p => p.PlayerId == cardInHand.ControllerPlayerId);
                    var opponentState = gameState.Players.FirstOrDefault(p => p.PlayerId != cardInHand.ControllerPlayerId);

                    if (playerState != null && opponentState != null)
                    {
                        var gameContext = new GameContext(gameState, playerState, opponentState);
                        var playCardEvent = new PlayCardEvent(playerState, cardInstance);

                        // Process the event through the ability system
                        var generatedEventDtos = await abilityManager.ProcessEventAsync(playCardEvent, gameContext);

                        // Add generated events to context
                        foreach (var dto in generatedEventDtos)
                        {
                            context.AddEvent(dto);
                        }

                        _logger.LogDebug("[GameService:{GameId}] Processed card play abilities, generated {EventCount} events.",
                            gameId, generatedEventDtos.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Error processing card abilities for {CardName}.",
                    gameId, cardDefinition.Name);
                // Don't throw - ability processing errors shouldn't break card playing
            }
        }

        /// <summary>
        /// Parses card definition to extract ability configurations for the new ability system.
        /// </summary>
        /// <param name="cardDefinition">The card definition to parse.</param>
        /// <returns>A list of ability configurations.</returns>
        private List<AbilityConfig> ParseCardAbilitiesToConfigs(CardDefinition cardDefinition)
        {
            var abilities = new List<AbilityConfig>();

            // Parse keywords into abilities
            if (cardDefinition.Keywords != null)
            {
                foreach (var keyword in cardDefinition.Keywords)
                {
                    var ability = CreateAbilityFromKeyword(cardDefinition, keyword);
                    if (ability != null)
                    {
                        abilities.Add(ability);
                    }
                }
            }

            // Parse description for additional abilities
            if (!string.IsNullOrEmpty(cardDefinition.Description))
            {
                var descriptionAbilities = ParseDescriptionAbilities(cardDefinition);
                abilities.AddRange(descriptionAbilities);
            }

            return abilities;
        }

        /// <summary>
        /// Creates an ability configuration from a keyword.
        /// </summary>
        /// <param name="cardDefinition">The card definition.</param>
        /// <param name="keyword">The keyword to parse.</param>
        /// <returns>An ability configuration or null if not recognized.</returns>
        private AbilityConfig? CreateAbilityFromKeyword(CardDefinition cardDefinition, string keyword)
        {
            switch (keyword.ToLowerInvariant())
            {
                case "rush":
                    return new AbilityConfig
                    {
                        Id = $"rush_{cardDefinition.Id}",
                        CardId = cardDefinition.Id,
                        Name = "Rush",
                        Description = "Can attack units immediately",
                        AbilityTag = "Rush",
                        Type = AbilityType.Keyword,
                        Trigger = new TriggerConfig
                        {
                            EventTypes = new List<GameEventType> { GameEventType.CardSummoned },
                        },
                        Conditions = new List<ConditionConfig>(),
                        Targets = new List<TargetConfig>(),
                        Effects = new List<EffectConfig>
                        {
                            new EffectConfig
                            {
                                Type = "modify_property",
                                Parameters = new Dictionary<string, ValueConfig>
                                {
                                    ["property"] = ValueConfig.Static("CanAttack"),
                                    ["value"] = ValueConfig.Static(true)
                                },
                                ExecutionOrder = 1
                            }
                        },
                    };

                case "storm":
                    return new AbilityConfig
                    {
                        Id = $"storm_{cardDefinition.Id}",
                        CardId = cardDefinition.Id,
                        Name = "Storm",
                        Description = "Can attack anything immediately",
                        AbilityTag = "Storm",
                        Type = AbilityType.Keyword,
                        Trigger = new TriggerConfig
                        {
                            EventTypes = new List<GameEventType> { GameEventType.CardSummoned },
                        },
                        Conditions = new List<ConditionConfig>(),
                        Targets = new List<TargetConfig>(),
                        Effects = new List<EffectConfig>
                        {
                            new EffectConfig
                            {
                                Type = "modify_property",
                                Parameters = new Dictionary<string, ValueConfig>
                                {
                                    ["property"] = ValueConfig.Static("CanAttack"),
                                    ["value"] = ValueConfig.Static(true)
                                },
                                ExecutionOrder = 1
                            }
                        },
                    };

                default:
                    return null;
            }
        }

        /// <summary>
        /// Parses description text for additional abilities like Fanfare, Last Words, etc.
        /// </summary>
        /// <param name="cardDefinition">The card definition to parse.</param>
        /// <returns>A list of abilities found in the description.</returns>
        private List<AbilityConfig> ParseDescriptionAbilities(CardDefinition cardDefinition)
        {
            var abilities = new List<AbilityConfig>();
            var description = cardDefinition.Description?.ToLowerInvariant() ?? string.Empty;

            // Parse Fanfare abilities
            if (description.Contains("fanfare"))
            {
                var fanfareAbility = new AbilityConfig
                {
                    Id = $"fanfare_{cardDefinition.Id}",
                    CardId = cardDefinition.Id,
                    Name = "Fanfare",
                    Description = "Triggered when played from hand",
                    AbilityTag = "Fanfare",
                    Type = AbilityType.Triggered,
                    Trigger = new TriggerConfig
                    {
                        EventTypes = new List<GameEventType> { GameEventType.CardPlayed },
                    },
                    Conditions = new List<ConditionConfig>(),
                    Targets = new List<TargetConfig>(),
                    Effects = ParseEffectFromDescription(cardDefinition.Description ?? "", "fanfare"),
                };
                abilities.Add(fanfareAbility);
            }

            // Parse Last Words abilities
            if (description.Contains("last words"))
            {
                var lastWordsAbility = new AbilityConfig
                {
                    Id = $"lastwords_{cardDefinition.Id}",
                    CardId = cardDefinition.Id,
                    Name = "Last Words",
                    Description = "Triggered when destroyed",
                    AbilityTag = "LastWords",
                    Type = AbilityType.Triggered,
                    Trigger = new TriggerConfig
                    {
                        EventTypes = new List<GameEventType> { GameEventType.CardDestroyed },
                    },
                    Conditions = new List<ConditionConfig>(),
                    Targets = new List<TargetConfig>(),
                    Effects = ParseEffectFromDescription(cardDefinition.Description ?? "", "lastwords")
                };
                abilities.Add(lastWordsAbility);
            }

            return abilities;
        }

        /// <summary>
        /// Parses effects from description text.
        /// </summary>
        /// <param name="description">The description text.</param>
        /// <param name="abilityType">The type of ability.</param>
        /// <returns>A list of effect configurations.</returns>
        private List<EffectConfig> ParseEffectFromDescription(string description, string abilityType) // Changed return type
        {
            var effects = new List<EffectConfig>(); // Changed list type
            var lowerDesc = description.ToLowerInvariant();

            // Parse damage effects
            if (lowerDesc.Contains("deal") && lowerDesc.Contains("damage"))
            {
                var damageMatch = System.Text.RegularExpressions.Regex.Match(lowerDesc, @"deal (\d+) damage");
                if (damageMatch.Success && int.TryParse(damageMatch.Groups[1].Value, out int damage))
                {
                    effects.Add(new EffectConfig // Changed to EffectConfig
                    {
                        Type = "damage",
                        Parameters = new Dictionary<string, ValueConfig> // Corrected Dictionary type
                        {
                            ["damage"] = ValueConfig.Static(damage), // Use Static factory method
                            ["target"] = ValueConfig.Static("enemy") // Use Static factory method
                        },
                        ExecutionOrder = 1
                    });
                }
            }

            // Parse draw effects
            if (lowerDesc.Contains("draw") && lowerDesc.Contains("card"))
            {
                var drawMatch = System.Text.RegularExpressions.Regex.Match(lowerDesc, @"draw (\d+) card");
                if (drawMatch.Success && int.TryParse(drawMatch.Groups[1].Value, out int count))
                {
                    effects.Add(new EffectConfig // Changed to EffectConfig
                    {
                        Type = "draw",
                        Parameters = new Dictionary<string, ValueConfig> // Corrected Dictionary type
                        {
                            ["count"] = ValueConfig.Static(count) // Use Static factory method
                        },
                        ExecutionOrder = 1
                    });
                }
            }

            return effects;
        }

        /// <summary>
        /// Parses effect parameters from effect description text.
        /// This is a simplified parser for basic effects.
        /// </summary>
        /// <param name="effectDescription">The effect description text.</param>
        /// <param name="abilityType">The type of ability being parsed.</param>
        /// <returns>Parameters for the ability.</returns>
        private Dictionary<string, object> ParseEffectParameters(string effectDescription, string abilityType)
        {
            var parameters = new Dictionary<string, object>();

            // Simple text parsing for common effects
            var lowerDesc = effectDescription.ToLowerInvariant();

            // Parse damage effects
            if (lowerDesc.Contains("deal") && lowerDesc.Contains("damage"))
            {
                parameters["effect"] = "damage";

                // Try to extract damage amount
                var damageMatch = System.Text.RegularExpressions.Regex.Match(lowerDesc, @"deal (\d+) damage");
                if (damageMatch.Success && int.TryParse(damageMatch.Groups[1].Value, out int damage))
                {
                    parameters["damage"] = damage;
                }

                // Try to extract target
                if (lowerDesc.Contains("enemy leader"))
                {
                    parameters["target"] = "enemy_leader";
                }
                else if (lowerDesc.Contains("all enemies"))
                {
                    parameters["target"] = "all_enemies";
                }
                else if (lowerDesc.Contains("random enemy"))
                {
                    parameters["target"] = "random_enemy";
                }
            }

            // Parse draw effects
            if (lowerDesc.Contains("draw"))
            {
                parameters["effect"] = "draw";

                var drawMatch = System.Text.RegularExpressions.Regex.Match(lowerDesc, @"draw (\d+) card");
                if (drawMatch.Success && int.TryParse(drawMatch.Groups[1].Value, out int count))
                {
                    parameters["count"] = count;
                }
                else
                {
                    parameters["count"] = 1; // Default to 1 card
                }
            }

            // Parse summon effects
            if (lowerDesc.Contains("summon"))
            {
                parameters["effect"] = "summon";
                // For now, we'd need more sophisticated parsing to extract what to summon
                // This is a placeholder for future enhancement
            }

            return parameters;
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
        public async Task<TurnStartedEventDto> StartPlayerTurnAsync(string gameId, string startingPlayerId)
        {
            _logger.LogInformation("[GameService:{GameId}] Executing StartPlayerTurnAsync for Player {StartingPlayerId}.", gameId, startingPlayerId);
            // M8 Change: Removed local events list creation. Using context.

            // Acquire lock for this specific game
            bool isAiTurn = false; // M8 Change: Declare isAiTurn here to be accessible for return value
            object gameLock = _gameLocks.GetOrAdd(gameId, _ => new object()); // Fixed lambda syntax
            GameStateDto? gameState = null; // Declare gameState here to be accessible after the lock (Corrected Type)
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
                var startingPlayerState = gameState.Players.FirstOrDefault(p => p.PlayerId == startingPlayerId); // Fixed lambda syntax
                if (startingPlayerState == null)
                {
                    throw new GameLogicException($"Player state not found for {startingPlayerId} in game {gameId} (in StartPlayerTurnAsync).");
                }

                // --- Start of logic adapted from ProcessStartOfTurn ---

                // --- Increment Player's Individual Turn Count ---
                startingPlayerState.TurnNumber++;
                // V2 Change: Determine AI status inside lock
                isAiTurn = startingPlayerState.IsAI; // M8 Change: Assign value inside lock where startingPlayerState is valid
                int currentPlayerTurn = startingPlayerState.TurnNumber; // Use this for player-specific logic
                _logger.LogInformation("[GameService:{GameId}] Incrementing TurnNumber for {PlayerId} to {PlayerTurn}.",
                    gameState.GameId, startingPlayerState.PlayerId, currentPlayerTurn);

                _logger.LogWarning("[GS_DEBUG_SOT:{GameId}] Processing StartOfTurn for PlayerId={PlayerId}", gameState.GameId, startingPlayerState.PlayerId); // DEBUG LOG (Fixed format string)

                // --- M7 Design Doc Step 5.1: Update Game State ---
                string oldPlayerId = gameState.CurrentPlayerId; // Store old ID before changing
                gameState.CurrentPlayerId = startingPlayerState.PlayerId;
                gameState.Phase = GamePhase.Main; // Transition to Main phase
                _logger.LogWarning("[GS_DEBUG_SOT:{GameId}] Setting gameState.CurrentPlayerId from '{OldPlayer}' to '{NewPlayer}'", gameState.GameId, oldPlayerId, gameState.CurrentPlayerId); // DEBUG LOG
                gameState.GameMessage = $"Turn {currentPlayerTurn} - {startingPlayerState.PlayerId}'s turn."; // Use individual turn number for message
                _logger.LogInformation("[GameService:{GameId}] StartOfTurn processing state updated for Player={CurrentPlayerId}, PlayerTurn={PlayerTurn}, Phase={Phase}.", // Original log
                   gameState.GameId, gameState.CurrentPlayerId, currentPlayerTurn, gameState.Phase);

                // --- M7 Design Doc Step 5.2: Update Player PP ---
                if (startingPlayerState.MaxPP < 10)
                {
                    startingPlayerState.MaxPP++;
                }
                startingPlayerState.CurrentPP = startingPlayerState.MaxPP; // Refill PP
                // M7 V1.1 Change: Removed context.AddEvent for PpUpdateEventDto
                _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} PP updated: {CurrentPP}/{MaxPP}.", // Removed "Event added."
                    gameState.GameId, startingPlayerState.PlayerId, startingPlayerState.CurrentPP, startingPlayerState.MaxPP);

                // --- M7 Design Doc Step 5.3: Update Player EP (Fixed for Shadowverse Rules) ---
                int requiredEvoTurn = startingPlayerState.IsFirst ? 5 : 4; // P1 needs Turn 5, P2 needs Turn 4
                bool shouldUnlockEvolution = currentPlayerTurn >= requiredEvoTurn && startingPlayerState.MaxEP == 0;
                int previousEP = startingPlayerState.CurrentEP; // Store previous EP for logging/event

                // Evolution unlock: Set EP limit and grant full EP on first unlock
                if (shouldUnlockEvolution)
                {
                    int maxEpForPlayer = startingPlayerState.IsFirst ? 2 : 3; // P1 gets 2 EP, P2 gets 3 EP
                    startingPlayerState.MaxEP = maxEpForPlayer;
                    startingPlayerState.CurrentEP = maxEpForPlayer; // Grant full EP immediately on unlock
                    _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} evolution UNLOCKED (Turn {PlayerTurn}). Granted {MaxEP} EP.",
                       gameState.GameId, startingPlayerState.PlayerId, currentPlayerTurn, maxEpForPlayer);
                }
                // Note: EP does NOT auto-recover after unlock - only consumed through evolution

                if (startingPlayerState.CurrentEP != previousEP)
                {
                    // M7 V1.1 Change: Removed context.AddEvent for EpUpdateEventDto
                    _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} EP changed. New EP: {CurrentEP}/{MaxEP}.", // Removed "Event added."
                      gameState.GameId, startingPlayerState.PlayerId, startingPlayerState.CurrentEP, startingPlayerState.MaxEP);
                } // <<< Closing brace for the if block

                // --- M7 Design Doc Step 5.3.1: Update CanEvolve Flag ---
                // Player can evolve if they have unlocked evolution and have EP available
                bool canEvolveThisTurn = startingPlayerState.MaxEP > 0 && startingPlayerState.CurrentEP > 0;
                startingPlayerState.CanEvolve = canEvolveThisTurn;
                _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} CanEvolve set to {CanEvolve} (MaxEP: {MaxEP}, CurrentEP: {CurrentEP}).",
                    gameState.GameId, startingPlayerState.PlayerId, canEvolveThisTurn, startingPlayerState.MaxEP, startingPlayerState.CurrentEP);

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
                List<CardInstanceDto> drawnCards = DrawCards(startingPlayerState, cardsToDraw); // Use helper (Changed variable type)
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
                    // Units already on the field at the start of the turn should be able to attack
                    // (unless affected by a status effect later). Reset flags explicitly here.
                    // Summoning sickness is handled by units starting with CanAttack=false when played (unless Rush/Storm).
                    unit.CanAttack = true;          // Reset attack permission
                    unit.AttacksMadeThisTurn = 0; // Reset attack count for the new turn
                    _logger.LogTrace("[GameService:{GameId}] Reset state for unit {UnitName} ({UnitInstanceId}): CanAttack=True, AttacksMade=0",
                        gameState.GameId, unit.Name, unit.InstanceId); // Added Trace log
                }
                _logger.LogDebug("[GameService:{GameId}] Finished resetting CanAttack and AttacksMadeThisTurn flags for Player {PlayerId}'s {UnitCount} units.",
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
            // Directly use the DTO retrieved/modified in the lock. No ToDto() call needed.
            var updateEvent = new TurnStartedEventDto(gameState, startingPlayerId); // Create event directly with gameState

            _logger.LogDebug("[GameService:{GameId}] Returning TurnStartedEventDto for player {PlayerId}.",
                gameState.GameId, startingPlayerId);

            return await Task.FromResult(updateEvent); // Use Task.FromResult for async Task<T>
        } // <<< Closing brace for StartPlayerTurnAsync method
        // Implement SubmitMulliganAsync from IGameService
        // Removed duplicate signature inserted by previous diff
        // M8 Change: Added GameActionContext parameter
        public async Task SubmitMulliganAsync(string gameId, string playerId, List<Guid> cardUidsToReplace, GameActionContext context)
        {
            _logger.LogInformation("[GameService] Player {PlayerId} in Game {GameId} attempting SubmitMulligan with {CardCount} cards.",
                playerId, gameId, cardUidsToReplace?.Count ?? 0);
            // Acquire lock for this specific game
            object gameLock = _gameLocks.GetOrAdd(gameId, _ => new object());

            // Variables to store results from lock block for use outside
            GameStateDto? stateToBroadcast = null;
            bool shouldBroadcastState = false;
            PlayerMulliganCompletedEventDto? mulliganEventToBroadcast = null; // Variable for intermediate event
            bool shouldStartFirstTurn = false; // Flag to trigger first turn start outside lock
            string? startingPlayerIdForTurn1 = null; // Store starting player ID for use outside lock
            string groupName = $"Game_{gameId}"; // Group name needed outside lock

            try // Outer try block starts here
            {
                _logger.LogWarning("--- DEBUG MULLIGAN: Entering Outer Try Block ---"); // DEBUG LOG
                                                                                        // --- Decide AI Mulligan *before* lock ---
                List<string>? aiCardsToReplaceUids = null; // Initialize
                GameStateDto? gameStateForAiCheck = null; // Temporary variable to hold state for AI check
                _gameStates.TryGetValue(gameId, out gameStateForAiCheck); // Try get state for check

                if (gameStateForAiCheck != null) // Check if game state was found
                {
                    var tempOpponentState = gameStateForAiCheck.Players.FirstOrDefault(p => p.PlayerId != playerId && p.IsAI && !p.HasCompletedMulligan);
                    if (tempOpponentState != null)
                    {
                        _logger.LogInformation("[GameService:{GameId}] Requesting AI mulligan decision for {OpponentId} (outside lock).", gameId, tempOpponentState.PlayerId);
                        // Ensure we pass a valid GameStateDto copy if needed, or assume IAiService is safe with the potentially stale one for read-only decision
                        aiCardsToReplaceUids = await _aiService.DecideMulliganAsync(gameStateForAiCheck, tempOpponentState.PlayerId);
                        _logger.LogInformation("[GameService:{GameId}] AI {OpponentId} decision received (outside lock): Replace {Count} cards.",
                             gameId, tempOpponentState.PlayerId, aiCardsToReplaceUids?.Count ?? 0);
                    }
                }
                else
                {
                    _logger.LogWarning("[GameService:{GameId}] Could not retrieve game state before lock to check for AI mulligan.", gameId);
                }
                // --- End AI Mulligan Decision ---

                lock (gameLock) // Lock block now encapsulates only synchronous operations
                {
                    _logger.LogWarning("--- DEBUG MULLIGAN: Entered Lock Block ---"); // DEBUG LOG
                    _logger.LogDebug("[GameService:SubmitMulliganAsync] Entered lock block for Game {GameId}, Player {PlayerId}.", gameId, playerId);

                    // No inner try needed here anymore for the main logic flow
                    // --- Game State Retrieval & Validation ---
                    if (!_gameStates.TryGetValue(gameId, out var gameState))
                    {
                        throw new GameLogicException($"Game not found: {gameId}");
                    }
                    _logger.LogWarning("--- DEBUG MULLIGAN: GameState retrieved ---"); // DEBUG LOG

                    if (gameState.Phase != GamePhase.Mulligan)
                    {
                        throw new GameLogicException($"Invalid game phase for mulligan: {gameState.Phase}");
                    }

                    var playerState = gameState.Players.FirstOrDefault(p => p.PlayerId == playerId);
                    if (playerState == null)
                    {
                        throw new GameLogicException($"Player not found in game: {playerId}");
                    }
                    _logger.LogWarning("--- DEBUG MULLIGAN: PlayerState retrieved ---"); // DEBUG LOG

                    if (playerState.HasCompletedMulligan)
                    {
                        throw new GameLogicException($"Player {playerId} has already completed mulligan.");
                    }

                    // --- Card Replacement Logic ---
                    _logger.LogInformation("[GameService:{GameId}] Processing mulligan for {PlayerId}. Replacing {Count} cards.", gameId, playerId, cardUidsToReplace?.Count ?? 0);

                    if (cardUidsToReplace != null && cardUidsToReplace.Any())
                    {
                        _logger.LogWarning("--- DEBUG MULLIGAN: Starting Human Card Replacement Logic ---"); // DEBUG LOG
                                                                                                             // --- Actual Card Replacement Logic ---

                        // 1. Validate UIDs are in hand
                        var currentHandUids = playerState.Hand.Select(c => c.InstanceId).ToHashSet(); // Changed Uid to InstanceId
                        var invalidUids = cardUidsToReplace.Where(uid => !currentHandUids.Contains(uid)).ToList();
                        if (invalidUids.Any())
                        {
                            throw new GameLogicException($"Invalid card UIDs submitted for mulligan: {string.Join(", ", invalidUids)}");
                        }
                        _logger.LogWarning("--- DEBUG MULLIGAN: Human Replace - UIDs Validated ---"); // DEBUG LOG

                        // 2. Separate cards
                        var cardsToReplace = playerState.Hand.Where(c => cardUidsToReplace.Contains(c.InstanceId)).ToList(); // Changed Uid to InstanceId
                        var cardsToKeep = playerState.Hand.Where(c => !cardUidsToReplace.Contains(c.InstanceId)).ToList(); // Changed Uid to InstanceId
                        _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} keeping {KeepCount}, replacing {ReplaceCount} cards.", gameId, playerId, cardsToKeep.Count, cardsToReplace.Count);
                        _logger.LogWarning("--- DEBUG MULLIGAN: Human Replace - Cards Separated ---"); // DEBUG LOG

                        // 3. Return cards to deck
                        playerState.Deck.AddRange(cardsToReplace);
                        _logger.LogDebug("[GameService:{GameId}] Returned {ReplaceCount} cards to deck. Deck size now {DeckSize}.", gameId, cardsToReplace.Count, playerState.Deck.Count);
                        _logger.LogWarning("--- DEBUG MULLIGAN: Human Replace - Cards Returned to Deck ---"); // DEBUG LOG


                        // 4. Update hand to kept cards
                        playerState.Hand = cardsToKeep;
                        // 6. Draw new cards to replace the mulliganed ones


                        // 5. Shuffle deck
                        ShuffleDeck(playerState.Deck);
                        _logger.LogDebug("[GameService:{GameId}] Deck shuffled.", gameId);
                        _logger.LogWarning("--- DEBUG MULLIGAN: Human Replace - Deck Shuffled ---"); // DEBUG LOG


                        // 6. Draw new cards
                        int cardsToDrawCount = cardsToReplace.Count;
                        List<CardInstanceDto> drawnCards = new List<CardInstanceDto>(); // Changed type from CardDto
                        _logger.LogDebug("[GameService:{GameId}] Attempting to draw {DrawCount} new cards.", gameId, cardsToDrawCount);
                        for (int i = 0; i < cardsToDrawCount; i++)
                        {
                            if (playerState.Deck.Any())
                            {
                                var drawnCard = playerState.Deck[0]; // Changed CardDto to var for type inference
                                drawnCards.Add(drawnCard);
                                playerState.Deck.RemoveAt(0);
                            }
                            else
                            {
                                _logger.LogWarning("[GameService:{GameId}] Attempted to draw from empty deck during mulligan for Player {PlayerId}.", gameId, playerId);
                                break; // Stop drawing if deck is empty
                            }
                        }
                        _logger.LogDebug("[GameService:{GameId}] Drew {DrawnCount} new cards.", gameId, drawnCards.Count);
                        _logger.LogWarning("--- DEBUG MULLIGAN: Human Replace - New Cards Drawn ---"); // DEBUG LOG


                        // 7. Add newly drawn cards to hand
                        playerState.Hand.AddRange(drawnCards);
                        _logger.LogDebug("[GameService:{GameId}] Added drawn cards to hand. New hand size: {HandSize}", gameId, playerState.Hand.Count);
                        _logger.LogWarning("--- DEBUG MULLIGAN: Human Replace - Drawn Cards Added to Hand ---"); // DEBUG LOG


                        // 8. Update deck count
                        playerState.DeckCount = playerState.Deck.Count;
                        _logger.LogDebug("[GameService:{GameId}] Final deck count: {DeckCount}", gameId, playerState.DeckCount);

                        // TODO: Broadcast hand update event specifically to this player
                        _logger.LogDebug("[GameService:{GameId}] TODO: Broadcast HandUpdate event for Player {PlayerId}", gameId, playerId);

                        _logger.LogWarning("--- DEBUG MULLIGAN: Human Replace - End of Replacement Logic ---"); // DEBUG LOG
                    }
                    else
                    {
                        _logger.LogWarning("--- DEBUG MULLIGAN: Human Replace - No Cards Replaced Path ---"); // DEBUG LOG
                        _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} chose to replace 0 cards.", gameId, playerId);
                        // No card replacement needed if list is null or empty
                    }

                    // --- Mark Mulligan Complete ---
                    playerState.HasCompletedMulligan = true;
                    _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} marked as completed mulligan.", gameId, playerId);
                    _logger.LogWarning("--- DEBUG MULLIGAN: Human Mulligan Marked Complete ---"); // DEBUG LOG

                    // ---> Prepare intermediate event broadcast <---
                    // Corrected: Use constructor instead of object initializer
                    mulliganEventToBroadcast = new PlayerMulliganCompletedEventDto(playerId);
                    _logger.LogDebug("[GameService:{GameId}] Preparing PlayerMulliganCompleted event for broadcast (Player: {PlayerId}).", gameId, playerId);


                    // --- Check Opponent and Trigger AI Mulligan if needed ---
                    _logger.LogWarning("--- DEBUG MULLIGAN: Checking Opponent State ---"); // DEBUG LOG
                    var opponentState = gameState.Players.FirstOrDefault(p => p.PlayerId != playerId);
                    if (opponentState == null)
                    {
                        // This shouldn't happen in a 2-player game after validation
                        throw new GameLogicException($"Opponent not found for player {playerId} in game {gameId}.");
                    }
                    // REMOVED PREMATURE CLOSING BRACE FROM Line 244

                    // ---> START AI MULLIGAN PROCESSING (using pre-calculated decision) <---
                    if (opponentState.IsAI && !opponentState.HasCompletedMulligan)
                    {
                        _logger.LogWarning("--- DEBUG MULLIGAN: Opponent is AI and hasn't mulliganed. Processing AI mulligan. ---"); // DEBUG LOG
                        _logger.LogInformation("[GameService:{GameId}] Opponent is AI ({OpponentId}). Processing AI mulligan (inside lock). Using pre-calculated decision.", gameId, opponentState.PlayerId);

                        // Use the decision made outside the lock (aiCardsToReplaceUids variable should be accessible here)
                        if (aiCardsToReplaceUids != null && aiCardsToReplaceUids.Any())
                        {
                            // --- Actual Card Replacement Logic (Adapted from human player, using aiCardsToReplaceUids) ---
                            _logger.LogInformation("[GameService:{GameId}] AI {OpponentId} replacing {Count} cards.", gameId, opponentState.PlayerId, aiCardsToReplaceUids.Count);

                            // 1. Validate UIDs are in hand (Defensive check)
                            var currentHandUids = opponentState.Hand.Select(c => c.InstanceId).ToHashSet(); // Changed Uid to InstanceId
                            // Parse string UIDs from AI to Guid for HashSet comparison
                            var invalidUids = aiCardsToReplaceUids
                                .Where(uidStr => !(Guid.TryParse(uidStr, out Guid parsedGuid) && currentHandUids.Contains(parsedGuid)))
                                .ToList();
                            if (invalidUids.Any())
                            {
                                _logger.LogWarning("[GameService:{GameId}] AI {OpponentId} returned invalid card UIDs for mulligan (check inside lock): {InvalidUids}. Replacing 0 cards instead.",
                                    gameId, opponentState.PlayerId, string.Join(", ", invalidUids));
                                // Clear the list to prevent errors, effectively replacing 0 cards
                                // aiCardsToReplaceUids.Clear(); // Don't clear the original list, just don't proceed with replacement
                            }
                            else
                            {
                                _logger.LogWarning("--- DEBUG MULLIGAN: AI Replace - UIDs Validated ---"); // DEBUG LOG
                                                                                                           // 2. Separate cards
                                                                                                           // Convert Guid c.Uid to string for List<string>.Contains comparison
                                var cardsToReplace = opponentState.Hand.Where(c => aiCardsToReplaceUids.Contains(c.InstanceId.ToString())).ToList(); // Changed Uid to InstanceId
                                // Convert Guid c.Uid to string for List<string>.Contains comparison
                                var cardsToKeep = opponentState.Hand.Where(c => !aiCardsToReplaceUids.Contains(c.InstanceId.ToString())).ToList(); // Changed Uid to InstanceId
                                _logger.LogDebug("[GameService:{GameId}] AI {OpponentId} keeping {KeepCount}, replacing {ReplaceCount} cards.", gameId, opponentState.PlayerId, cardsToKeep.Count, cardsToReplace.Count);

                                // 3. Return cards to deck
                                opponentState.Deck.AddRange(cardsToReplace);
                                _logger.LogDebug("[GameService:{GameId}] Returned {ReplaceCount} cards to AI {OpponentId}'s deck. Deck size now {DeckSize}.", gameId, cardsToReplace.Count, opponentState.PlayerId, opponentState.Deck.Count);


                                // 4. Update hand to kept cards
                                // 6. Draw new cards for AI to replace the mulliganed ones (using DrawCards helper)
                                opponentState.Hand = cardsToKeep;

                                // 5. Shuffle deck
                                ShuffleDeck(opponentState.Deck); // Assuming ShuffleDeck is safe inside lock
                                _logger.LogDebug("[GameService:{GameId}] AI {OpponentId}'s deck shuffled.", gameId, opponentState.PlayerId);
                                _logger.LogWarning("--- DEBUG MULLIGAN: AI Replace - Deck Shuffled ---"); // DEBUG LOG


                                // 6. Draw new cards (Using DrawCards helper which modifies state)
                                int cardsToDrawCount = cardsToReplace.Count;
                                List<CardInstanceDto> drawnCards = DrawCards(opponentState, cardsToDrawCount); // Use DrawCards helper (Changed variable type)
                                _logger.LogDebug("[GameService:{GameId}] AI {OpponentId} drew {DrawnCount} new cards.", gameId, opponentState.PlayerId, drawnCards.Count);
                                _logger.LogWarning("--- DEBUG MULLIGAN: AI Replace - New Cards Drawn ---"); // DEBUG LOG


                                // 7. Add newly drawn cards to hand (Already handled by DrawCards helper)
                                // 8. Update deck count (Already handled by DrawCards helper)

                                // TODO: Broadcast AI's HandUpdate event? Maybe not necessary for opponent.
                                _logger.LogDebug("[GameService:{GameId}] AI {OpponentId} mulligan card replacement complete. Final hand size: {HandSize}, Deck count: {DeckCount}",
                                   gameId, opponentState.PlayerId, opponentState.Hand.Count, opponentState.DeckCount);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("--- DEBUG MULLIGAN: AI Replace - No Cards Replaced Path ---"); // DEBUG LOG
                            _logger.LogInformation("[GameService:{GameId}] AI {OpponentId} chose to replace 0 cards (based on pre-calculated decision).", gameId, opponentState.PlayerId);
                            // No card replacement needed
                        }

                        // Mark AI Mulligan Complete regardless of replacement
                        opponentState.HasCompletedMulligan = true;
                        _logger.LogInformation("[GameService:{GameId}] AI Player {OpponentId} marked as completed mulligan.", gameId, opponentState.PlayerId);
                        _logger.LogWarning("--- DEBUG MULLIGAN: AI Mulligan Marked Complete ---"); // DEBUG LOG

                    }
                    // ---> END AI MULLIGAN PROCESSING <---

                    // --- Check if Both Players Completed Mulligan (Now includes AI auto-completion) ---
                    _logger.LogWarning("--- DEBUG MULLIGAN: Checking if Both Players Completed ---"); // DEBUG LOG
                    if (playerState.HasCompletedMulligan && opponentState.HasCompletedMulligan)
                    {
                        _logger.LogWarning("--- DEBUG MULLIGAN: Both Players Completed - Entering Block ---"); // DEBUG LOG
                        _logger.LogWarning(">>>>>>>>>> [DEBUG] Both players completed mulligan block ENTERED. P1: {P1Done}, P2: {P2Done}. Starting game.", playerState.HasCompletedMulligan, opponentState.HasCompletedMulligan); // <-- ADDED WARNING LOG
                        _logger.LogInformation("[GameService:{GameId}] Both players have completed mulligan. Starting game.", gameId);
                        // --- Transition to First Turn ---
                        gameState.Phase = GamePhase.TurnStart; // Or Main? TurnStart seems better for drawing etc.
                                                               // M7 Design Doc Step 3.3: Turn number initialization removed from GameState
                                                               // Player turn numbers are incremented in ProcessStartOfTurn
                        gameState.CurrentPlayerId = gameState.FirstPlayerId; // Already determined in GetOrCreate
                        _logger.LogWarning("--- DEBUG MULLIGAN: Set Phase, CurrentPlayerID ---"); // DEBUG LOG
                                                                                                  // Removed gameState.TurnNumber log
                        _logger.LogWarning("[GameService:{GameId}] >>> MULLIGAN END: CurrentPlayerId set to: {CurrentPlayerId}", gameId, gameState.CurrentPlayerId);
                        // gameState.GameMessage update moved to ProcessStartOfTurn

                        // TODO: Implement Start Turn Logic (PP, Draw etc.) (Logic moved to ProcessStartOfTurn)
                        // Maybe refactor this into a StartTurn(gameState, startingPlayerId) helper method?

                        var startingPlayerState = gameState.Players.First(p => p.PlayerId == gameState.CurrentPlayerId);
                        var secondPlayerState = gameState.Players.First(p => p.PlayerId != gameState.CurrentPlayerId);

                        // Draw Cards
                        // TODO: Create DrawCard(playerState, count) helper
                        _logger.LogWarning("[GameService:{GameId}] Placeholder: Draw card logic not yet implemented.", gameId);
                        // Draw for starting player
                        // DrawCard(startingPlayerState, 1);
                        // Draw extra card for second player at game start
                        // DrawCard(secondPlayerState, 1);


                        // --- Prepare for First Turn Start (Outside Lock) ---
                        _logger.LogInformation("[GameService:{GameId}] Both players completed mulligan. Preparing to start first turn outside lock.", gameId);
                        shouldStartFirstTurn = true; // Set flag to trigger start turn outside lock
                        startingPlayerIdForTurn1 = startingPlayerState.PlayerId; // Store the ID
                        _logger.LogDebug("[GameService:{GameId}] Flag 'shouldStartFirstTurn' set. Starting player ID '{PlayerId}' stored.", gameId, startingPlayerIdForTurn1);

                        // *** Prepare state for broadcast LATER (outside lock) ***
                        stateToBroadcast = gameState;
                        shouldBroadcastState = true;
                        _logger.LogWarning("--- DEBUG MULLIGAN: Prepared State for Broadcast Outside Lock ---"); // DEBUG LOG
                                                                                                                 // firstTurnStartEvents will be assigned outside lock if shouldStartFirstTurn is true
                    }
                    else
                    {
                        _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} completed mulligan. Waiting for opponent.", gameId, playerId);
                        // Optionally: Send a specific event indicating one player is done?
                        // await _hubContext.Clients.Client(opponentConnectionId).PlayerMulliganComplete(playerId); // Still needs await if uncommented, handle outside lock
                    }

                    // --- Update Game State in Memory (Implicit by modifying gameState object) ---
                    _logger.LogWarning("--- DEBUG MULLIGAN: Reaching End of Lock Block ---"); // DEBUG LOG
                } // <<< LOCK BLOCK ENDS HERE

                // --- Start First Turn (if needed, outside lock) ---
                GameActionContext? firstTurnContext = null; // Declare context for first turn (nullable)
                if (shouldStartFirstTurn && !string.IsNullOrEmpty(startingPlayerIdForTurn1))
                {
                    _logger.LogInformation("[GameService:{GameId}] Triggering StartPlayerTurnAsync for first turn (outside lock). Player: {PlayerId}", gameId, startingPlayerIdForTurn1);
                    try
                    {
                        // M8 Change: Initialize context and pass it to StartPlayerTurnAsync
                        firstTurnContext = new GameActionContext(); // Initialize the pre-declared context
                        _logger.LogDebug("[GameService:{GameId}] Initialized context for first turn start.", gameId);
                        // M7 V1.1 Change: Call new signature, get DTO, determine AI status from DTO
                        TurnStartedEventDto firstTurnStateEvent = await this.StartPlayerTurnAsync(gameId, startingPlayerIdForTurn1);
                        bool isAiTurn = firstTurnStateEvent.GameState.Players.FirstOrDefault(p => p.PlayerId == firstTurnStateEvent.GameState.CurrentPlayerId)?.IsAI ?? false;
                        // Use firstTurnStateEvent.FullState for any state checks if needed
                        _logger.LogInformation("[GameService:{GameId}] StartPlayerTurnAsync completed for first turn. Is AI Turn: {IsAiTurn}. Returned TurnStartedEventDto.", gameId, isAiTurn);

                        // --- ROO CHANGE: Broadcast Start Turn Events IMMEDIATELY after generation, BEFORE AI execution ---
                        // M7 V1.1 Change: Comment out broadcast - StartPlayerTurnAsync now returns state, broadcasting responsibility shifts.
                        // if (firstTurnContext?.Events != null && firstTurnContext.Events.Any())
                        // {
                        //     _logger.LogInformation("[GameService:{GameId}] Broadcasting {EventCount} start-of-turn events from firstTurnContext (outside lock, before potential AI execution).", gameId, firstTurnContext.Events.Count);
                        //     await BroadcastEventsAsync(gameId, firstTurnContext.Events); // MOVED & UNCOMMENTED
                        // }
                        // else
                        // {
                        //     _logger.LogDebug("[GameService:{GameId}] No first turn start events found in firstTurnContext to broadcast before AI execution.", gameId);
                        // }
                        // --- END ROO CHANGE ---

                        // Trigger AI's first turn if applicable
                        // M7 V1.1 Change: Use the derived boolean result
                        if (isAiTurn)
                        {
                            _logger.LogInformation("[GameService:{GameId}] First player ({PlayerId}) is AI. Triggering AI's first turn after mulligan.", gameId, startingPlayerIdForTurn1);
                            // Await AI turn execution
                            await ExecuteAiActionPhaseAsync(gameId, startingPlayerIdForTurn1); // Call AI action phase directly if AI is first
                        }
                    }
                    catch (Exception startTurnEx)
                    {
                        _logger.LogError(startTurnEx, "[GameService:{GameId}] Error executing StartPlayerTurnAsync outside lock for Player {PlayerId}.", gameId, startingPlayerIdForTurn1);
                        // Decide how to handle this error. Maybe throw, maybe just log?
                        // For now, log and continue to broadcast whatever might have been prepared before the error.
                        firstTurnContext = null; // Ensure context is null if start turn failed
                    }
                }

                _logger.LogWarning("--- DEBUG MULLIGAN: Exited Lock Block, Preparing Broadcasts ---"); // DEBUG LOG
                                                                                                       // --- Perform Broadcasts outside the lock ---

                // Broadcast the intermediate mulligan completion event FIRST
                if (mulliganEventToBroadcast != null)
                {
                    _logger.LogInformation("[GameService:{GameId}] Broadcasting PlayerMulliganCompleted event for {PlayerId} (outside lock).", gameId, playerId); // Use the original playerId variable
                    await _hubContext.Clients.Group(groupName).PlayerMulliganCompleted(mulliganEventToBroadcast);
                }

                // Prepare and broadcast AI mulligan completed event if applicable
                PlayerMulliganCompletedEventDto? aiMulliganEvent = null;
                PlayerStateDto? opponentStateForEvent = null; // Need opponent state to check IsAI and HasCompletedMulligan

                // Need to safely get game state and opponent state again, potentially within a short lock or using TryGetValue
                // Using TryGetValue is safer outside the main lock
                if (_gameStates.TryGetValue(gameId, out var finalGameStateCheck))
                {
                    opponentStateForEvent = finalGameStateCheck.Players.FirstOrDefault(p => p.PlayerId != playerId);
                }

                // Check if the opponent is AI and completed mulligan *during the preceding lock block*.
                // We rely on HasCompletedMulligan flag being set correctly within the lock.
                if (opponentStateForEvent != null && opponentStateForEvent.IsAI && opponentStateForEvent.HasCompletedMulligan)
                {
                    // Check if the human player's event wasn't already the AI's event (e.g., if AI was last)
                    if (mulliganEventToBroadcast?.SourcePlayerId != opponentStateForEvent.PlayerId)
                    {
                        aiMulliganEvent = new PlayerMulliganCompletedEventDto(opponentStateForEvent.PlayerId);
                        _logger.LogDebug("[GameService:{GameId}] Preparing AI PlayerMulliganCompleted event for broadcast (outside lock check). Player: {PlayerId}", gameId, opponentStateForEvent.PlayerId);
                    }
                }

                if (aiMulliganEvent != null)
                {
                    _logger.LogInformation("[GameService:{GameId}] Broadcasting AI PlayerMulliganCompleted event for {PlayerId} (outside lock).", gameId, aiMulliganEvent.SourcePlayerId);
                    await _hubContext.Clients.Group(groupName).PlayerMulliganCompleted(aiMulliganEvent);
                }


                // Broadcast the final game state AFTER the intermediate event, if needed
                if (shouldBroadcastState && stateToBroadcast != null)
                {
                    _logger.LogInformation("[GameService:{GameId}] Broadcasting updated game state after mulligan completion (outside lock).", gameId);
                    // Perform the actual await outside the lock
                    await _hubContext.Clients.Group(groupName).ReceiveGameState(stateToBroadcast);

                    // --- Broadcast Start Turn Events AFTER full state ---
                    // M8 Change: Check if context is not null before accessing events
                    // Roo Change: REMOVED redundant broadcast of first turn events.
                    // These events should be handled by the actual turn execution flow (AI or Human).
                    // The full state broadcast above should suffice for post-mulligan sync.
                    // --- ROO CHANGE: Original broadcast block removed as it was moved earlier ---
                    // if (firstTurnContext?.Events != null && firstTurnContext.Events.Any()) // Add null-conditional operator ?.
                    // {
                    //     _logger.LogWarning("[GameService:{GameId}] Intentionally SKIPPING broadcast of {EventCount} potentially redundant start-of-turn events from firstTurnContext (outside lock).", gameId, firstTurnContext.Events.Count);
                    //     // await BroadcastEventsAsync(gameId, firstTurnContext.Events); // REMOVED
                    // }
                    // else
                    // {
                    //     _logger.LogDebug("[GameService:{GameId}] No first turn start events found in firstTurnContext to potentially broadcast (context is null or empty).", gameId);
                    // }
                    // --- END ROO CHANGE ---
                }
                // No need for await Task.CompletedTask anymore

                _logger.LogWarning("--- DEBUG MULLIGAN: Reaching End of Outer Try Block ---"); // DEBUG LOG
            } // <<< OUTER TRY BLOCK ENDS HERE
            catch (GameLogicException ex) // Catch blocks are now correctly placed after the try block
            {
                _logger.LogWarning(ex, "[GameService] Game Logic Error during SubmitMulligan for Player {PlayerId} in Game {GameId}", playerId, gameId);
                _logger.LogWarning("Full GameLogicException Details: {ExceptionString}", ex.ToString()); // Log full details
                throw; // Re-throw for the Hub to handle and notify the specific caller
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Unexpected error during SubmitMulligan for Player {PlayerId} in Game {GameId}", playerId, gameId);
                _logger.LogError("Full Exception Details: {ExceptionString}", ex.ToString()); // Log full details
                throw new GameLogicException("An unexpected error occurred during mulligan submission.", ex); // Wrap for Hub
            } // This brace closes the 'catch (Exception ex)' block
        } // This brace closes the SubmitMulliganAsync method
          // REMOVED EXTRA CLOSING BRACE FROM Line 319
          // --- Added GetGameStateAsync Implementation ---
        /// <summary>
        /// Retrieves the current game state for the specified game ID.
        /// Does not create a new game if the state does not exist.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <returns>The GameStateDto if found, otherwise null.</returns>
        public async Task<GameStateDto?> GetGameStateAsync(string gameId)
        {
            _logger.LogDebug("[GameService] Attempting to get game state for Game {GameId}.", gameId);

            if (_gameStates.TryGetValue(gameId, out var existingState))
            {
                _logger.LogDebug("[GameService] Found existing game state for Game {GameId}.", gameId);
                // Wrap synchronous result in a completed task.
                return await Task.FromResult(existingState);
            }
            else
            {
                _logger.LogDebug("[GameService] No existing game state found for Game {GameId}.", gameId);
                // Return a completed task with a null result.
                return await Task.FromResult<GameStateDto?>(null);
            }
        }
        // --- End Added GetGameStateAsync Implementation ---

        // --- Refactored to GetOrCreateGameStateAsync ---
        /// <summary>
        /// Gets the current game state for the given game ID, or creates a new one if it doesn't exist.
        /// </summary>
        /// <param name="gameId">The unique identifier for the game.</param>
        /// <returns>The GameStateDto for the game, or null if creation failed unexpectedly.</returns>
        public async Task<GameStateDto?> GetOrCreateGameStateAsync(string gameId) // Removed playerId parameter for now
        {
            _logger.LogInformation("[GameService] Attempting to get or create game state for Game {GameId}.", gameId);

            if (_gameStates.TryGetValue(gameId, out var existingState))
            {
                _logger.LogInformation("[GameService] Found existing game state for Game {GameId}.", gameId);
                return existingState;
            }

            _logger.LogInformation("[GameService] No existing game state found for Game {GameId}. Creating new state.", gameId);

            // --- New Game State Creation Logic ---
            try
            {
                // TODO: Need a mechanism to assign Player 1 and Player 2 IDs when a game *starts*.
                // For now, using placeholders. This logic should ideally live elsewhere (e.g., matchmaking service or initial Hub join).
                string player1Id = "player1"; // Placeholder
                string player2Id = "player2"; // Placeholder

                List<CardDefinition> allCardDefinitions = new List<CardDefinition>();
                // Create a scope to resolve Scoped ICardRepository
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var cardRepository = scope.ServiceProvider.GetRequiredService<ICardRepository>();
                    _logger.LogDebug("[GameService:{GameId}] Resolving ICardRepository within scope.", gameId);
                    try
                    {
                        // Fetch ALL card definitions
                        var allCardDefinitionsEnumerable = await cardRepository.GetAllCardDefinitionsAsync();
                        // Load ALL card definitions (Removed IsCollectible filter)
                        allCardDefinitions = allCardDefinitionsEnumerable.ToList(); // Assign the full list
                        _logger.LogInformation("[GameService:{GameId}] Fetched {Count} total card definitions.", gameId, allCardDefinitions.Count);
                    }
                    catch (Exception repoEx)
                    {
                        _logger.LogError(repoEx, "[GameService:{GameId}] Error fetching card definitions from repository within scope.", gameId);
                        // Handle error, maybe return null or throw, depending on requirements
                        return null; // Can't create game without card definitions
                    }
                }

                // Check if any definitions were found
                if (allCardDefinitions == null || !allCardDefinitions.Any())
                {
                    _logger.LogError("[GameService:{GameId}] No card definitions found in the repository. Cannot create game state.", gameId);
                    return null; // Cannot proceed without cards
                }

                // --- Fixed Test Deck Creation Logic ---
                int targetDeckSize = 20; // Set target deck size to 20 for testing
                List<CardInstanceDto> player1Deck = new List<CardInstanceDto>(targetDeckSize); // Use CardInstanceDto
                List<CardInstanceDto> player2Deck = new List<CardInstanceDto>(targetDeckSize); // Use CardInstanceDto

                // Create a lookup dictionary for efficient access to definitions
                var definitionLookup = allCardDefinitions.ToDictionary(def => def.Id);

                // --- Roo Add: Fetch Leader Definition (Using actual Leader ID 0) ---
                const int leaderCardId = 0; // Use actual Leader ID
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
                        player1Deck.Add(new CardInstanceDto // Use CardInstanceDto with correct properties and conversions
                        {
                            InstanceId = Guid.NewGuid(),
                            // Assign int Id directly
                            CardId = cardDef.Id,
                            Name = cardDef.Name,
                            Cost = cardDef.Cost,
                            Type = cardDef.Type,
                            Attack = cardDef.Attack ?? 0,             // Handle nullable int?
                            Health = cardDef.Health ?? 0,             // Handle nullable int?
                            CurrentAttack = cardDef.Attack ?? 0,      // Initialize Current stats, handle nullable
                            CurrentHealth = cardDef.Health ?? 0,      // Initialize Current stats, handle nullable
                            MaxHealth = cardDef.Health ?? 0,          // Initialize Max Health, handle nullable
                            Description = cardDef.Description ?? string.Empty,
                            // EffectDescriptions removed as CardDefinition lacks a direct source
                            Keywords = cardDef.Keywords ?? new List<string>(), // Handle nullable List<string>?
                            CanEvolve = cardDef.EvolveId.HasValue, // Check HasValue for int?
                            IsEvolved = false,                   // Starts unevolved
                            CanAttack = false,                   // Cannot attack from deck
                            Zone = CardZone.Deck                 // Initial zone
                        });

                        // Create unique CardInstanceDto instances for each player's deck
                        player2Deck.Add(new CardInstanceDto // Use CardInstanceDto with correct properties and conversions
                        {
                            InstanceId = Guid.NewGuid(),
                            // Assign int Id directly
                            CardId = cardDef.Id,
                            Name = cardDef.Name,
                            Cost = cardDef.Cost,
                            Type = cardDef.Type,
                            Attack = cardDef.Attack ?? 0,             // Handle nullable int?
                            Health = cardDef.Health ?? 0,             // Handle nullable int?
                            CurrentAttack = cardDef.Attack ?? 0,      // Initialize Current stats, handle nullable
                            CurrentHealth = cardDef.Health ?? 0,      // Initialize Current stats, handle nullable
                            MaxHealth = cardDef.Health ?? 0,          // Initialize Max Health, handle nullable
                            Description = cardDef.Description ?? string.Empty,
                            // EffectDescriptions removed as CardDefinition lacks a direct source
                            Keywords = cardDef.Keywords ?? new List<string>(), // Handle nullable List<string>?
                            CanEvolve = cardDef.EvolveId.HasValue, // Check HasValue for int?
                            IsEvolved = false,                   // Starts unevolved
                            CanAttack = false,                   // Cannot attack from deck
                            Zone = CardZone.Deck                 // Initial zone
                        });
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
                List<CardInstanceDto> player1Hand = DrawInitialHand(player1Deck, initialHandSize); // Changed type
                List<CardInstanceDto> player2Hand = DrawInitialHand(player2Deck, initialHandSize); // Changed type
                // Deck count in PlayerStateDto will be updated later when assigning the deck

                // Decide first player randomly
                bool player1IsFirst = new Random().Next(0, 2) == 0;
                string firstPlayerId = player1IsFirst ? player1Id : player2Id;
                string secondPlayerId = player1IsFirst ? player2Id : player1Id;

                // --- Roo Add: Create Leader Card Instances ---
                var player1LeaderInstance = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(),
                    CardId = leaderCardDef.Id,
                    Name = leaderCardDef.Name,
                    Cost = leaderCardDef.Cost,
                    Type = leaderCardDef.Type, // Should be Leader type ideally
                    Attack = leaderCardDef.Attack ?? 0,
                    Health = leaderCardDef.Health ?? 0, // Leader health is usually tracked separately
                    CurrentAttack = leaderCardDef.Attack ?? 0,
                    CurrentHealth = leaderCardDef.Health ?? 0, // Use PlayerStateDto.LeaderHealth instead?
                    MaxHealth = leaderCardDef.Health ?? 0,
                    Description = leaderCardDef.Description ?? string.Empty,
                    Keywords = leaderCardDef.Keywords ?? new List<string>(),
                    CanEvolve = leaderCardDef.EvolveId.HasValue,
                    IsEvolved = false,
                    CanAttack = false, // Leaders typically don't attack directly this way
                    Zone = CardZone.Leader, // Special zone
                    ControllerPlayerId = player1Id
                    // Other properties like MaxAttack, AttacksMadeThisTurn might not apply or need defaults
                };
                var player2LeaderInstance = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(),
                    CardId = leaderCardDef.Id,
                    Name = leaderCardDef.Name,
                    Cost = leaderCardDef.Cost,
                    Type = leaderCardDef.Type, // Should be Leader type ideally
                    Attack = leaderCardDef.Attack ?? 0,
                    Health = leaderCardDef.Health ?? 0, // Leader health is usually tracked separately
                    CurrentAttack = leaderCardDef.Attack ?? 0,
                    CurrentHealth = leaderCardDef.Health ?? 0, // Use PlayerStateDto.LeaderHealth instead?
                    MaxHealth = leaderCardDef.Health ?? 0,
                    Description = leaderCardDef.Description ?? string.Empty,
                    Keywords = leaderCardDef.Keywords ?? new List<string>(),
                    CanEvolve = leaderCardDef.EvolveId.HasValue,
                    IsEvolved = false,
                    CanAttack = false, // Leaders typically don't attack directly this way
                    Zone = CardZone.Leader, // Special zone
                    ControllerPlayerId = player2Id
                    // Other properties like MaxAttack, AttacksMadeThisTurn might not apply or need defaults
                };
                // --- End Roo Add ---

                var player1State = new PlayerStateDto
                {
                    PlayerId = player1Id,
                    IsAI = false, // Assuming player 1 is human
                    CurrentPP = 0, // Start at 0 PP before turn 1 begins
                    MaxPP = 0,
                    CurrentEP = 0, // Start with 0 EP
                    MaxEP = 0,     // Start with 0 EP limit, will be set when evolution unlocks
                    CanEvolve = false,
                    Hand = player1Hand,
                    Deck = player1Deck, // Store the actual deck
                    DeckCount = player1Deck.Count, // Sync count after draw
                    Field = new List<CardInstanceDto>(), // Changed type
                    IsFirst = player1IsFirst,
                    PlayedCardCountThisTurn = 0, // Reset counter
                    TurnNumber = 0, // Initialize TurnNumber
                    Graveyard = new List<CardInstanceDto>(), // Initialize Graveyard (Changed type)
                    LeaderCard = player1LeaderInstance // Roo Add: Assign Leader Card
                };

                var player2State = new PlayerStateDto
                {
                    PlayerId = player2Id,
                    IsAI = true, // Assuming player 2 is AI
                    CurrentPP = 0,
                    MaxPP = 0,
                    CurrentEP = 0, // Start with 0 EP
                    MaxEP = 0,     // Start with 0 EP limit, will be set when evolution unlocks
                    CanEvolve = false,
                    Hand = player2Hand,
                    Deck = player2Deck, // Store the actual deck
                    DeckCount = player2Deck.Count, // Sync count after draw
                    Field = new List<CardInstanceDto>(), // Changed type
                    IsFirst = !player1IsFirst,
                    PlayedCardCountThisTurn = 0, // Reset counter
                    TurnNumber = 0, // Initialize TurnNumber
                    Graveyard = new List<CardInstanceDto>(), // Initialize Graveyard (Changed type)
                    LeaderCard = player2LeaderInstance // Roo Add: Assign Leader Card
                };

                // Player decks are now stored directly in PlayerStateDto.Deck

                var newGameState = new GameStateDto
                {
                    GameId = gameId,
                    CurrentPlayerId = firstPlayerId, // First player takes the first turn
                    FirstPlayerId = firstPlayerId,
                    // TurnNumber removed from GameStateDto
                    Phase = GamePhase.Mulligan, // Start in Mulligan phase (using Enum)
                    Players = new List<PlayerStateDto> { player1State, player2State },
                    IsGameOver = false,
                    WinnerId = null,
                    GameStatus = GameStatus.InProgress, // Use enum
                    GameMessage = "Game created. Waiting for players." // Initial message
                };

                // _logger.LogInformation("[GameService:{GameId}] >>> Attempting to add newly created game state to dictionary...", gameId); // REMOVED DIAGNOSTIC LOG
                // ADDED: Log initial hand details for player 1 before adding state
                try // Added try-catch for safety during logging complex objects
                {
                    string player1HandDetails = string.Join(", ", player1State.Hand.Select(c => $"{{InstanceId='{c.InstanceId}', Name='{c.Name}', ID='{c.CardId}'}}")); // Changed c.Uid to c.InstanceId and label in string
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
        } // This closes the GetOrCreateGameStateAsync method body

        // TODO: Implement helper methods used above - Placeholder Implementations
        // Placeholder implementation for AttackAsync
        // --- Attack Logic Implementation (M7.2 - Rev3 Plan) ---
        // Implements IGameService.AttackAsync
        // M8 Change: Added GameActionContext parameter, changed return type to Task<bool>
        public async Task<bool> AttackAsync(string gameId, string playerId, AttackActionDto actionDto, GameActionContext context)
        {
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} executing Attack: Attacker={AttackerUid}, Target={TargetUid}.",
                gameId, playerId, actionDto.AttackerUid, actionDto.TargetUid);



            try
            {
                // 1. Get Game State & Players
                if (!_gameStates.TryGetValue(gameId, out var gameState))
                {
                    throw new GameLogicException($"Game not found: {gameId}");
                }

                if (gameState.IsGameOver)
                {
                    throw new GameLogicException("Cannot attack, game is already over.");
                }

                var currentPlayer = gameState.Players.FirstOrDefault(p => p.PlayerId == playerId);
                var opponentPlayer = gameState.Players.FirstOrDefault(p => p.PlayerId != playerId);

                if (currentPlayer == null || opponentPlayer == null)
                {
                    throw new GameLogicException($"Player state error in game {gameId}.");
                }

                // 2. Validation
                // 2a. Turn Validation
                if (gameState.CurrentPlayerId != playerId)
                {
                    throw new GameLogicException($"It's not player {playerId}'s turn (Current: {gameState.CurrentPlayerId}).");
                }
                // 2b. Phase Validation (Assuming Battle Phase exists)
                // TODO: Get actual GamePhase enum values if different
                if (gameState.Phase != GamePhase.Main) // Check GamePhase enum value
                {
                    throw new GameLogicException($"Cannot attack during {gameState.Phase} phase.");
                }

                // 2c. Attacker Validation
                var attacker = currentPlayer.Field.FirstOrDefault(u => u.InstanceId.ToString() == actionDto.AttackerUid); // Changed Uid to InstanceId
                if (attacker == null)
                {
                    throw new GameLogicException($"Attacker unit (UID: {actionDto.AttackerUid}) not found on player {playerId}'s field.");
                }
                // TODO: Get actual CardType enum values if different
                if (attacker.Type != CardType.Follower.ToString()) // Check CardType enum value
                {
                    throw new GameLogicException($"Only Followers can attack. Entity type: {attacker.Type}");
                }
                // Modified Check: Fail if CanAttack is false OR if already attacked this turn
                if (!attacker.CanAttack || attacker.AttacksMadeThisTurn >= attacker.MaxAttacksPerTurn)
                {
                    // Updated exception message to be more informative
                    throw new GameLogicException($"Attacker unit {attacker.Name} (UID: {actionDto.AttackerUid}) cannot attack (CanAttack: {attacker.CanAttack}, AttacksMade: {attacker.AttacksMadeThisTurn}).");
                }

                // 2d. Target Validation
                var targetEntity = FindEntityByUid(gameState, actionDto.TargetUid); // CALL HELPER

                // Explicit null check to satisfy compiler for subsequent uses
                if (targetEntity is null) // Use 'is null' pattern for reference types
                {
                    throw new GameLogicException($"Target entity (UID: {actionDto.TargetUid}) not found.");
                }

                // Now targetEntity is known to be non-null here

                // Ensure target is opponent's entity
                bool isTargetFriendly = false;
                // Add null-forgiving operator (!) as targetEntity is checked for null earlier
                if (targetEntity is PlayerStateDto targetPlayer)
                {
                    isTargetFriendly = targetPlayer.PlayerId == playerId;
                }
                // Add null-forgiving operator (!)
                else if (targetEntity is CardInstanceDto targetUnit) // Changed type check
                {
                    // Now targetUnit is CardInstanceDto, InstanceId access is valid
                    isTargetFriendly = currentPlayer.Field.Any(u => u.InstanceId == targetUnit.InstanceId);
                }
                if (isTargetFriendly)
                {
                    throw new GameLogicException("Cannot target friendly entities with an attack.");
                }

                // 2e. Ward Validation
                var opponentUnitsWithWard = opponentPlayer.Field
                                            .Where(u => u.Keywords.Contains("Ward", StringComparer.OrdinalIgnoreCase))
                                            .ToList();
                if (opponentUnitsWithWard.Any())
                {
                    // Add null-forgiving operator (!)
                    // Assuming targetEntity/targetWardUnit is now CardInstanceDto, compare InstanceId (Guid)
                    if (!(targetEntity is CardInstanceDto targetWardUnit && opponentUnitsWithWard.Any(w => w.InstanceId == targetWardUnit.InstanceId))) // Changed UnitStateDto to CardInstanceDto, Uid to InstanceId
                    {
                        throw new GameLogicException($"Must target a unit with Ward. Target UID: {actionDto.TargetUid}");
                    }
                }

                // --- Validation Passed ---
                _logger.LogDebug("[GameService:{GameId}] Attack validation passed for {AttackerName} -> {TargetUid}.",
                    gameId, attacker.Name, actionDto.TargetUid);

                // 3. Execution
                attacker.AttacksMadeThisTurn++; // Increment attack count for this turn (as per M7 design)


                // Phase 1: Attack Declared Event (Aligned with M7.2)
                context.AddEvent(new AttackDeclaredEventDto(playerId, attacker.InstanceId.ToString(), actionDto.TargetUid)); // M8 Change: Use context | Changed Uid to InstanceId
                _logger.LogWarning("[Roo Test:{GameId}] AttackDeclaredEvent generation SKIPPED.", gameState.GameId); // Roo Test: Add log
                _logger.LogDebug("[GameService:{GameId}] Generated AttackDeclaredEvent (Player: {PlayerId}, Attacker: {AttackerUid}, Target: {TargetUid})",
                    gameId, playerId, attacker.InstanceId, actionDto.TargetUid); // Changed Uid to InstanceId
                // Phase 2: Clash Timing (Placeholder)
                // TODO: Implement Clash effects if target is Unit

                // Phase 3: Damage Timing
                _logger.LogDebug("[GameService:{GameId}] Entering Damage Phase.", gameId);
                int attackerAttack = attacker.CurrentAttack; // Removed ?? 0 as CurrentAttack is not nullable
                // Add null-forgiving operator (!) to satisfy non-nullable parameter type
                // Pass necessary context to ApplyDamage for DamageDealtEventDto
                ApplyDamage(targetEntity, attackerAttack, context, currentPlayer.PlayerId, opponentPlayer.PlayerId, attacker.InstanceId.ToString(), "Attack"); // M8 Change: Pass context | Changed Uid to InstanceId

                // Counter-attack if target is a unit and attacker is still alive
                // Add null-forgiving operator (!)
                if (targetEntity is CardInstanceDto defendingUnit && (attacker.CurrentHealth) > 0) // Check attacker health before counter | Removed ?? 0 | Changed type check
                {
                    _logger.LogDebug("[GameService:{GameId}] Target is unit, initiating counter-attack.", gameId);
                    int defenderAttack = defendingUnit.CurrentAttack; // Removed ?? 0
                                                                      // Check if defender is still alive before counter-attacking
                                                                      // BUG-018 Fix: Comment out check to ensure counter-attack happens even if defender dies
                                                                      // if ((defendingUnit.CurrentHealth) > 0) // Removed ?? 0
                                                                      // { // BUG-018 Fix: Also comment out braces and else block
                                                                      // Explicit cast for attacker (UnitStateDto) to IBoardEntityState needed for ApplyDamage
                                                                      // Pass necessary context for counter-attack damage event
                    ApplyDamage((ICard)attacker, defenderAttack, context, opponentPlayer.PlayerId, currentPlayer.PlayerId, defendingUnit.InstanceId.ToString(), "Attack"); // M8 Change: Pass context | Changed Uid to InstanceId
                    // } // BUG-018 Fix: Also comment out braces and else block
                    // else // BUG-018 Fix: Also comment out braces and else block
                    // { // BUG-018 Fix: Also comment out braces and else block
                    //     _logger.LogDebug("[GameService:{GameId}] Defending unit {DefenderUid} destroyed before counter-attack.", gameId, defendingUnit.InstanceId); // Changed Uid to InstanceId
                    // } // BUG-018 Fix: Also comment out braces and else block
                }

                // Phase 4: Death Timing
                _logger.LogDebug("[GameService:{GameId}] Entering Death Phase.", gameId);
                // Explicit cast for attacker (UnitStateDto) to IBoardEntityState needed for CheckAndHandleDeath
                CheckAndHandleDeath((ICard)attacker, currentPlayer, opponentPlayer, gameState, context); // CALL HELPER
                if (!gameState.IsGameOver) // Check if game ended during attacker's death check
                {
                    // Add null-forgiving operator (!) to satisfy non-nullable parameter type
                    CheckAndHandleDeath(targetEntity!, opponentPlayer, currentPlayer, gameState, context); // CALL HELPER
                }

                _logger.LogInformation("[GameService:{GameId}] Attack execution completed for Player {PlayerId}.", gameId, playerId); // M8 Change: Removed event count
                                                                                                                                      // M8 Change: Return true indicating successful execution (if no exception)
                return await Task.FromResult(true); // Use Task.FromResult for async Task<T>
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
          // Implements IGameService.EvolveAsync M7.3 - Chunk 1/5 (Corrected)
          // Implements IGameService.EvolveAsync
          // M8 Change: Added GameActionContext parameter, changed return type to Task<bool>
        public async Task<bool> EvolveAsync(string gameId, string playerId, EvolveActionDto actionDto, GameActionContext context)
        {
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} attempting Evolve: Unit={UnitUID}",
                gameId, playerId, actionDto.UnitUid);



            // Resolve dependencies within a scope
            using var scope = _serviceScopeFactory.CreateScope();
            var cardRepository = scope.ServiceProvider.GetRequiredService<ICardRepository>();

            try
            {
                // 1. Validate gameId and retrieve GameState.
                if (!_gameStates.TryGetValue(gameId, out var gameState))
                {
                    throw new GameLogicException($"Game not found: {gameId}");
                }

                // 2. Basic Validation: Game Status, Player's Turn, Game Phase
                if (gameState.IsGameOver) throw new GameLogicException("Game is already over.");
                if (gameState.CurrentPlayerId != playerId) throw new GameLogicException($"It's not player {playerId}'s turn.");
                if (gameState.Phase != GamePhase.Main) throw new GameLogicException("Can only evolve during the Main phase.");
                if (string.IsNullOrEmpty(actionDto.UnitUid)) throw new GameLogicException("Target unit UID cannot be empty.");

                // 3. Find Player State and Target Unit State
                var playerState = gameState.Players.FirstOrDefault(p => p.PlayerId == playerId);
                if (playerState == null) throw new GameLogicException($"Player state not found for {playerId}.");

                var targetUnit = playerState.Field.FirstOrDefault(u => u.InstanceId.ToString() == actionDto.UnitUid); // Changed Uid to InstanceId
                if (targetUnit == null) throw new GameLogicException($"Target unit with UID {actionDto.UnitUid} not found on player {playerId}'s board.");

                // --- Evolution Eligibility Validation (Chunk 2 starts next) ---
                // 4. Evolution Eligibility Validation - Chunk 2/5 (Corrected)
                // a. Check EP Cost (Assume 1 EP for now)
                const int evolveEpCost = 1;
                if (playerState.CurrentEP < evolveEpCost) throw new GameLogicException($"Not enough EP to evolve. Requires {evolveEpCost}, has {playerState.CurrentEP}.");

                // b. Check CanEvolve Flag
                if (!playerState.CanEvolve) throw new GameLogicException("Player cannot evolve units this turn (likely already evolved or turn restriction).");
                // Added Check: Unit Already Evolved
                if (targetUnit.IsEvolved) throw new GameLogicException($"Unit {targetUnit.Name} ({targetUnit.InstanceId}) is already evolved."); // Changed Uid to InstanceId


                // c. Check Turn Number Restrictions (Placeholder - needs refinement based on exact rules)
                // Rule: Player 1 can evolve starting turn 5, Player 2 starting turn 4.
                int requiredTurn = playerState.IsFirst ? 5 : 4;
                // Use the player's individual turn number for the check
                if (playerState.TurnNumber < requiredTurn)
                {
                    // Update exception message to use player's turn number
                    throw new GameLogicException($"Cannot evolve yet. Player {(playerState.IsFirst ? 1 : 2)} can evolve starting turn {requiredTurn} (Player's current turn: {playerState.TurnNumber}).");
                }

                // --- Fetch Card Definitions (Chunk 3 starts next) ---
                // 5. Fetch Base and Evolved Card Definitions - Chunk 3/5
                var baseCardDef = await cardRepository.GetCardDefinitionByIdAsync(targetUnit.CardId); // Pass int CardId directly
                if (baseCardDef == null) throw new GameLogicException($"Base card definition not found for CardId: {targetUnit.CardId}");
                // Check if EvolveId (now int?) has a value
                if (!baseCardDef.EvolveId.HasValue) throw new GameLogicException($"Card {baseCardDef.Name} ({baseCardDef.Id}) does not have an evolved form defined.");

                // Pass the int value directly, safe due to the HasValue check above
                var evolvedCardDef = await cardRepository.GetCardDefinitionByIdAsync(baseCardDef.EvolveId.Value);
                if (evolvedCardDef == null) throw new GameLogicException($"Evolved card definition not found for EvolvedCardId: {baseCardDef.EvolveId.Value}"); // Use .Value here too for logging

                // --- Perform Evolution (Chunk 4 starts next) ---
                // --- Perform Evolution (Chunk 4/5) ---
                _logger.LogDebug("[GameService:{GameId}] Performing evolution for Unit {UnitName} ({UnitUid}).", gameId, targetUnit.Name, targetUnit.InstanceId); // Changed Uid to InstanceId

                // a. Deduct EP Cost
                playerState.CurrentEP -= evolveEpCost;
                // Corrected to use constructor
                var epEvent = new EpUpdateEventDto(playerId, playerState.CurrentEP, playerState.MaxEP);
                context.Events.Add(epEvent); // M8 Change: Add to context instead of events list
                _logger.LogDebug("[GameService:{GameId}] Deducted {Cost} EP. Player {PlayerId} EP: {CurrentEP}/{MaxEP}.", gameId, evolveEpCost, playerId, playerState.CurrentEP, playerState.MaxEP);


                // b. Update Unit State using 'with' expression for immutability
                var originalUnitState = targetUnit; // Keep original for comparison/event data if needed
                var evolvedUnitState = originalUnitState with
                {
                    // Update core stats from evolved definition
                    CardId = evolvedCardDef.Id, // Use int Id directly
                    Name = evolvedCardDef.Name,
                    CurrentAttack = evolvedCardDef.Attack ?? 0, // Handle nullable int? from CardDefinition
                    MaxAttack = evolvedCardDef.Attack, // Evolved form starts with full stats
                    CurrentHealth = evolvedCardDef.Health ?? 0, // Handle nullable int? from CardDefinition
                    MaxHealth = evolvedCardDef.Health ?? 0, // Handle nullable int? from CardDefinition
                    IsEvolved = true, // Mark as evolved

                    // Combine keywords (Ensure no duplicates)
                    Keywords = (originalUnitState.Keywords ?? new List<string>())
                               .Union(evolvedCardDef.Keywords ?? new List<string>())
                               .Distinct()
                               .ToList(),

                    // Reset attack count, but check for Rush
                    AttacksMadeThisTurn = 0, // Base reset
                    CanAttack = (evolvedCardDef.Keywords?.Contains("Rush", StringComparer.OrdinalIgnoreCase) ?? false) // Allow attack if Rush
                };

                // Replace the old unit state with the new evolved state in the player's field list
                // Assuming targetUnit is now CardInstanceDto, compare InstanceId (Guid)
                int unitIndex = playerState.Field.FindIndex(u => u.InstanceId == targetUnit.InstanceId); // Changed Uid to InstanceId
                if (unitIndex != -1)
                {
                    playerState.Field[unitIndex] = evolvedUnitState;
                    _logger.LogDebug("[GameService:{GameId}] Unit {UnitUid} state updated to evolved form in player's field.", gameId, targetUnit.InstanceId); // Changed Uid to InstanceId
                }
                else
                {
                    // Should not happen if validation passed, but log defensively
                    _logger.LogError("[GameService:{GameId}] CRITICAL: Evolving unit {UnitUid} not found in player's field during update phase!", gameId, targetUnit.InstanceId); // Changed Uid to InstanceId
                    throw new GameLogicException($"Internal error: Evolving unit {targetUnit.InstanceId} disappeared."); // Changed Uid to InstanceId
                }


                // c. Generate Stat Change & Unit Evolved Events
                // Corrected StatChangeEventDto initialization based on actual definition
                // Corrected: Use constructor with sourcePlayerId (playerId is the source in Evolve context)
                var statEvent = new StatChangeEventDto(playerId, evolvedUnitState.InstanceId.ToString(), evolvedUnitState.CurrentAttack, evolvedUnitState.CurrentHealth, evolvedUnitState.MaxHealth); // Changed Uid to InstanceId
                context.Events.Add(statEvent); // M8 Change: Add to context instead of events list

                // Corrected UnitEvolvedEventDto initialization based on actual definition
                // Create a CardInstanceDto specifically for the event from the UnitStateDto
                var evolvedCardInstanceForEvent = new CardInstanceDto
                {
                    InstanceId = evolvedUnitState.InstanceId, // Map InstanceId (Guid) to InstanceId (Guid) | Changed Uid to InstanceId
                    CardId = evolvedUnitState.CardId, // Direct assignment as CardId is already int
                    Name = evolvedUnitState.Name,
                    Cost = evolvedUnitState.Cost,
                    Type = evolvedUnitState.Type,
                    Attack = evolvedUnitState.MaxAttack ?? 0, // Use MaxAttack from UnitStateDto for base Attack in CardInstanceDto? Or Current? Let's use Max for now.
                    Health = evolvedUnitState.MaxHealth, // Removed ?? 0
                    CurrentAttack = evolvedUnitState.CurrentAttack, // Removed ?? 0
                    CurrentHealth = evolvedUnitState.CurrentHealth, // Removed ?? 0
                    MaxHealth = evolvedUnitState.MaxHealth, // Removed ?? 0
                    Description = "", // CardInstanceDto doesn't have Description from UnitStateDto
                    Keywords = evolvedUnitState.Keywords ?? new List<string>(),
                    CanEvolve = false, // Already evolved
                    IsEvolved = evolvedUnitState.IsEvolved,
                    CanAttack = evolvedUnitState.CanAttack,
                    Zone = CardZone.Field, // It's on the field
                    AttacksMadeThisTurn = evolvedUnitState.AttacksMadeThisTurn,
                    MaxAttack = evolvedUnitState.MaxAttack ?? 0 // Assuming CardInstanceDto also has MaxAttack
                };
                var evolvedEvent = new UnitEvolvedEventDto(playerId, evolvedCardInstanceForEvent); // Pass the new CardInstanceDto
                context.Events.Add(evolvedEvent); // M8 Change: Add to context instead of events list
                _logger.LogDebug("[GameService:{GameId}] Generated StatChange and UnitEvolved events for {UnitUid}.", gameId, evolvedUnitState.InstanceId); // Changed Uid to InstanceId


                // d. Update Player State (Prevent further evolution this turn)
                playerState.CanEvolve = false;
                _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} CanEvolve flag set to false for the rest of the turn.", gameId, playerId);

                // Send EP update to sync CanEvolve state to frontend
                var canEvolveUpdateEvent = new EpUpdateEventDto(playerId, playerState.CurrentEP, playerState.MaxEP);
                context.Events.Add(canEvolveUpdateEvent);

                // --- Broadcasting & Cleanup (Chunk 5/5) ---
                _logger.LogInformation("[GameService:{GameId}] Evolution logic completed for Player {PlayerId}, Unit {UnitUid}.", gameId, playerId, evolvedUnitState.InstanceId); // M8 Change: Removed event count from log | Changed Uid to InstanceId

                // M8 Change: Return true indicating successful execution (if no exception)
                return await Task.FromResult(true); // Use Task.FromResult for async Task<T>
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning(ex, "[GameService:{GameId}] Game Logic Error during Evolve for Player {PlayerId}", gameId, playerId);
                throw; // Re-throw for the Hub to handle and notify the specific caller
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Unexpected error during Evolve for Player {PlayerId}", gameId, playerId);
                throw new GameLogicException("An unexpected error occurred during unit evolution.", ex); // Wrap for Hub
            }
        } // Closing brace for the NEW EvolveAsync method body

        // Removed the placeholder CreateDeckFromDefinitionAsync method as it's replaced by inline logic

        // --- Helper Methods for End Turn Logic ---

        /// <summary>
        /// Helper method to draw a specified number of cards from the player's deck list.
        /// Modifies the playerState's Deck and Hand directly. Handles empty deck scenario.
        /// Updates playerState.DeckCount.
        /// </summary>
        /// <summary>
        /// Draws a specified number of cards from the player's deck.
        /// Handles empty deck scenario. Updates player's DeckCount and Hand.
        /// </summary>
        /// <param name="playerState">The player drawing the cards.</param>
        /// <param name="drawCount">The number of cards to draw.</param>
        /// <returns>A list of cards drawn.</returns>
        private List<CardInstanceDto> DrawCards(PlayerStateDto playerState, int drawCount)
        {
            _logger.LogDebug("[GameService] Player {PlayerId} attempting to draw {DrawCount} cards. Deck size: {DeckSize}",
                playerState.PlayerId, drawCount, playerState.Deck.Count);

            List<CardInstanceDto> drawnCards = new();
            for (int i = 0; i < drawCount; i++)
            {
                if (playerState.Deck.Count > 0)
                {
                    CardInstanceDto drawnCard = playerState.Deck[0];
                    drawnCards.Add(drawnCard);
                    playerState.Deck.RemoveAt(0);
                }
                else
                {
                    _logger.LogWarning("[GameService] Attempted to draw from empty deck for Player {PlayerId}.", playerState.PlayerId);
                    break;
                }
            }

            playerState.DeckCount = playerState.Deck.Count;
            playerState.Hand.AddRange(drawnCards);

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
        private void ProcessEndOfTurn(PlayerStateDto endingPlayerState, GameStateDto gameState, GameActionContext context) // M8 Change: Added context, void return
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

            // M7 Design Doc - Step 4.2: TODO - Trigger "End of your turn" effects

            // M7 Design Doc - Step 4.3: Reset Attack Counts/States for player's units
            foreach (var unit in endingPlayerState.Field)
            {
                unit.AttacksMadeThisTurn = 0; // Reset attack count
                                              // Reset CanAttack unless the unit has Storm (Storm allows attacking the turn it's played AND subsequent turns)
                                              // Rush only allows attack on the turn it's played. Non-Rush/Storm units regain CanAttack at start of their turn.
                                              // For simplicity now, we only reset the count. CanAttack logic handled in ProcessStartOfTurn.
                                              // unit.CanAttack = unit.Keywords.Contains("Storm", StringComparer.OrdinalIgnoreCase); // Reset CanAttack based on Storm status? Let's handle CanAttack reset in StartOfTurn.
            }
            _logger.LogDebug("[GameService:{GameId}] Reset attack counts for Player {PlayerId}'s units.", gameState.GameId, endingPlayerState.PlayerId);


            // M7 Design Doc - Step 4.4: Generate TurnEndedEvent
            var opponentPlayerState = gameState.Players.FirstOrDefault(p => p.PlayerId != endingPlayerState.PlayerId);
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
            context.AddEvent(new TurnEndedEventDto(endingPlayerState.PlayerId, opponentPlayerState.PlayerId)); // M8 Change: Use context
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

        private void ShuffleDeck(List<CardInstanceDto> deck) // Start of ShuffleDeck (Parameter type changed)
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
                CardInstanceDto value = deck[k]; // Changed type to match deck parameter
                deck[k] = deck[n];
                deck[n] = value;
            }
        }

        private List<CardInstanceDto> DrawInitialHand(List<CardInstanceDto> deck, int handSize) // Changed types to CardInstanceDto
        {
            _logger.LogWarning("[GameService] Placeholder DrawInitialHand called.");
            // Placeholder: Draw cards from the top of the deck
            List<CardInstanceDto> hand = new List<CardInstanceDto>(); // Changed type to CardInstanceDto
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

        private ICard? FindEntityByUid(GameStateDto gameState, string uid)
        {
            if (!Guid.TryParse(uid, out Guid targetGuid)) // Try parse the target UID string to Guid
            {
                // If the UID is not a valid Guid, it might be a PlayerId (for leader targeting via PlayerStateDto)
                // Check PlayerId first, as PlayerStateDto might represent the leader card which implements ICard indirectly
                var targetPlayerById = gameState.Players.FirstOrDefault(p => p.PlayerId == uid);
                if (targetPlayerById != null)
                {
                    _logger.LogDebug("[GameService:FindEntityByUid] Found target entity by PlayerId: {PlayerId}", uid);
                    // If the target ID matches a player ID, return their LeaderCard as the targetable entity
                    if (targetPlayerById.LeaderCard != null)
                    {
                        return targetPlayerById.LeaderCard;
                    }
                    else
                    {
                        return null; // Or handle appropriately
                    }
                }
                // If not a valid Guid and not a PlayerId, it's an invalid target UID format
                _logger.LogWarning("[GameService:FindEntityByUid] Target UID '{TargetUid}' is not a valid Guid or PlayerId.", uid);
                return null;
            }

            // If it's a valid Guid, search followers and leader cards by InstanceId
            foreach (var player in gameState.Players)
            {
                // Check Leader Card InstanceId
                if (player.LeaderCard?.InstanceId == targetGuid)
                {
                    _logger.LogDebug("[GameService:FindEntityByUid] Found target entity by LeaderCard InstanceId: {InstanceId} for Player {PlayerId}", targetGuid, player.PlayerId);
                    return (ICard)player.LeaderCard; // Return the Leader CardInstanceDto, cast to ICard
                }

                // Check units on the player's field by InstanceId
                var unit = player.Field.FirstOrDefault(u => u.InstanceId == targetGuid);
                if (unit != null)
                {
                    _logger.LogDebug("[GameService:FindEntityByUid] Found target entity by Field Unit InstanceId: {InstanceId} for Player {PlayerId}", targetGuid, player.PlayerId);
                    return (ICard)unit; // Found unit, cast to ICard
                }
            }

            _logger.LogWarning("[GameService:FindEntityByUid] Target entity with InstanceId/PlayerId '{TargetUid}' not found after checking all players, leaders, and fields.", uid);
            return null; // Not found
        }

        // M8 Change: Signature updated to use GameActionContext
        private void ApplyDamage(ICard targetEntity, int damage, GameActionContext context, string sourcePlayerId, string targetPlayerId, string? sourceUid, string damageType)
        {
            // Check for null or non-positive damage first
            if (targetEntity == null || damage <= 0) return; // Corrected <=

            // Determine target UID for logging/events and get original health
            string eventTargetUid;
            int originalHealth;
            int newHealth;
            int changeAmount;

            if (targetEntity is CardInstanceDto cardInstance)
            {
                eventTargetUid = cardInstance.InstanceId.ToString();
                originalHealth = cardInstance.CurrentHealth; // Use CurrentHealth for CardInstanceDto
                _logger.LogTrace("[GameService:ApplyDamage] Target is CardInstanceDto. Using InstanceId '{InstanceId}'. Original Health: {OriginalHealth}", eventTargetUid, originalHealth);

                // Apply damage to CardInstanceDto
                cardInstance.CurrentHealth -= damage;
                newHealth = cardInstance.CurrentHealth;
            }
            else if (targetEntity is PlayerStateDto playerState && playerState.LeaderCard != null)
            {
                // Target is PlayerStateDto, use LeaderCard's InstanceId and Health
                eventTargetUid = playerState.LeaderCard.InstanceId.ToString();
                originalHealth = playerState.LeaderCard.CurrentHealth; // Use LeaderCard.CurrentHealth
                _logger.LogTrace("[GameService:ApplyDamage] Target is PlayerStateDto. Using LeaderCard InstanceId '{InstanceId}'. Original Health: {OriginalHealth}", eventTargetUid, originalHealth);

                // Apply damage to LeaderCard
                playerState.LeaderCard.CurrentHealth -= damage;
                newHealth = playerState.LeaderCard.CurrentHealth;
            }
            else
            {
                // Fallback/Error case: Log warning and do not apply damage if type is unexpected or LeaderCard is null
                eventTargetUid = targetEntity.Uid.ToString(); // Use original Uid as fallback for logging
                originalHealth = targetEntity.Health; // Use original Health as fallback for logging
                _logger.LogWarning("[GameService:ApplyDamage] Target entity type is unexpected ({EntityType}) or LeaderCard is null for PlayerStateDto. Damage not applied. Fallback UID: '{FallbackUid}'. Original Health: {OriginalHealth}", targetEntity.GetType().Name, eventTargetUid, originalHealth);
                return; // Do not proceed with damage application or event generation for unexpected types
            }

            changeAmount = newHealth - originalHealth;

            // Log damage application (using eventTargetUid)
            _logger.LogDebug("[GameService] Applying {Damage} damage to Entity {TargetUid} (Owned by {TargetPlayerId}) from Source {SourceUid} (Player {SourcePlayerId}). Type: {DamageType}. Original Health: {OriginalHealth}, New Health: {NewHealth}",
                damage, eventTargetUid, targetPlayerId, sourceUid ?? "Unknown", sourcePlayerId, damageType, originalHealth, newHealth);

            // Generate events (using eventTargetUid and correct newHealth/changeAmount)
            context.AddEvent(new DamageDealtEventDto(sourcePlayerId, eventTargetUid, damage, targetPlayerId, sourceUid, damageType));
            context.AddEvent(new HealthChangedEventDto(targetPlayerId, eventTargetUid, newHealth, changeAmount));

            _logger.LogDebug("[GameService] Entity {TargetUid} health changed to {NewHealth}. Change: {ChangeAmount}.",
               eventTargetUid, newHealth, changeAmount);
        }

        private void CheckAndHandleDeath(ICard entity, PlayerStateDto owner, PlayerStateDto opponent, GameStateDto gameState, GameActionContext context)
        {
            if (entity == null)
            {
                _logger.LogWarning("[GameService:{GameId}] CheckAndHandleDeath called with null entity.", gameState.GameId);
                return;
            }

            bool isOnField = true;
            if (entity is CardInstanceDto unitCheck) // Changed type check
            {
                // Now unitCheck is CardInstanceDto, InstanceId access is valid
                isOnField = owner.Field.Any(u => u.InstanceId == unitCheck.InstanceId);
            }

            // Only return early if it's NOT a leader AND (!isOnField or Health > 0)
            // Leaders should always proceed to the health check regardless of isOnField status.
            if (entity is CardInstanceDto card && card.Zone != CardZone.Leader && (!isOnField || card.CurrentHealth > 0))
            {
                return; // Return early for non-leader entities that are not dead or not on field
            }
            // If it IS a leader, or a follower on field with health <= 0, continue processing...
            // Also check health directly here for leaders, as the above condition skips it for them.
            if (entity.Health > 0) return; // Return if entity (leader or follower) is not dead

            _logger.LogInformation("[GameService:{GameId}] Entity {EntityUid} has zero or less health ({Health}). Processing death/game over check.", // Adjusted log message
                gameState.GameId, entity.Uid, entity.Health);

            if (entity is CardInstanceDto cardInstance) // Check if the entity is a CardInstanceDto (Follower or Leader)
            {
                // Check if this CardInstanceDto is the Leader and is defeated
                if (cardInstance.Zone == CardZone.Leader && cardInstance.CurrentHealth <= 0)
                {
                    _logger.LogInformation("[GameService:{GameId}] Player {PlayerId}'s Leader ({LeaderInstanceId}) has been defeated (Health: {LeaderHealth}).",
                        gameState.GameId, owner.PlayerId, cardInstance.InstanceId, cardInstance.CurrentHealth);

                    if (!gameState.IsGameOver)
                    {
                        gameState.IsGameOver = true;
                        gameState.WinnerId = opponent.PlayerId;
                        gameState.GameMessage = $"Game Over! Winner: {opponent.PlayerId}";
                        _logger.LogInformation("[GameService:{GameId}] Game Over! Winner declared: {WinnerId}", gameState.GameId, opponent.PlayerId);
                        context.AddEvent(new GameOverEventDto(owner.PlayerId, opponent.PlayerId, opponent.PlayerId, owner.PlayerId)); // Add GameOver event
                    }
                    else
                    {
                        _logger.LogWarning("[GameService:{GameId}] Player {PlayerId}'s Leader defeated, but game was already marked as over.", gameState.GameId, owner.PlayerId);
                    }
                    // Important: Return here after handling leader death to prevent follower death logic from running on the leader
                    return;
                }

                // If it's not a defeated leader, check if it's a defeated follower on the field
                if (cardInstance.Zone == CardZone.Field && cardInstance.CurrentHealth <= 0)
                {
                    var unitToRemove = owner.Field.FirstOrDefault(u => u.InstanceId == cardInstance.InstanceId);
                    if (unitToRemove != null)
                    {
                        bool removed = owner.Field.Remove(unitToRemove);
                        if (removed)
                        {
                            _logger.LogInformation("[GameService:{GameId}] Unit {UnitName} ({UnitInstanceId}) removed from Player {OwnerId}'s field.",
                               gameState.GameId, cardInstance.Name, cardInstance.InstanceId, owner.PlayerId);
                            context.AddEvent(new UnitDestroyedEventDto(opponent.PlayerId, cardInstance.InstanceId.ToString(), owner.PlayerId));

                            // TODO: Graveyard logic
                        }
                        else
                        {
                            _logger.LogWarning("[GameService:{GameId}] Failed to remove unit {UnitInstanceId} from owner {OwnerId}'s field list, though it was found and health <= 0.", gameState.GameId, cardInstance.InstanceId, owner.PlayerId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("[GameService:{GameId}] Dead unit {UnitInstanceId} checked again but not found on owner {OwnerId}'s field list.", gameState.GameId, cardInstance.InstanceId, owner.PlayerId);
                    }
                }
                // If it's a CardInstanceDto but not a defeated leader or follower (e.g., healthy leader/follower), do nothing more here.
            }
            // Removed the redundant 'else if (entity is PlayerStateDto player)' block
            else // Handle cases where entity is not CardInstanceDto (shouldn't happen with current logic)
            {
                _logger.LogError("[GameService:{GameId}] CheckAndHandleDeath called with an unexpected entity type: {EntityType}. UID: {EntityUid}", gameState.GameId, entity.GetType().Name, entity.Uid);
            }
        } // End of CheckAndHandleDeath
          // --- Helper Methods for HandlePlayCardAsync ---

        // Fetches CardDefinition and CardDto from hand asynchronously before main lock
        private async Task<(CardDefinition? CardDefinition, CardInstanceDto? CardInHand)> FetchCardDataAsync(string gameId, string playerId, string cardUid)
        {
            _logger.LogDebug("[GameService:{GameId}:FetchCardData] Starting fetch for CardUID {CardUID}.", gameId, cardUid);
            if (string.IsNullOrEmpty(cardUid))
                throw new GameLogicException("Card UID cannot be empty for fetching.");

            string cardIdToFetch;
            CardInstanceDto? cardInHand;
            object gameLock = _gameLocks.GetOrAdd(gameId, _ => new object());

            // Short lock just to get cardId from hand state
            lock (gameLock)
            {
                if (!_gameStates.TryGetValue(gameId, out var tempGameState))
                    throw new GameLogicException($"Game not found: {gameId} (during card fetch)");
                var tempPlayerState = tempGameState.Players.FirstOrDefault(p => p.PlayerId == playerId);
                if (tempPlayerState == null)
                    throw new GameLogicException($"Player state error in game {gameId} (during card fetch).");

                cardInHand = tempPlayerState.Hand.FirstOrDefault(c => Guid.TryParse(cardUid, out Guid uidToCompare) && c.InstanceId == uidToCompare);
                if (cardInHand == null)
                    throw new GameLogicException($"Card with UID {cardUid} not found in player {playerId}'s hand (during card fetch).");
                cardIdToFetch = cardInHand.CardId.ToString();
            }

            _logger.LogDebug("[GameService:{GameId}:FetchCardData] Need definition for CardId {CardIdToFetch}.", gameId, cardIdToFetch);
            using var scope = _serviceScopeFactory.CreateScope();
            var cardRepository = scope.ServiceProvider.GetRequiredService<ICardRepository>();
            var cardDefinition = await cardRepository.GetCardDefinitionByIdAsync(int.Parse(cardIdToFetch));
            if (cardDefinition == null)
                throw new GameLogicException($"Card definition not found for CardId: {cardIdToFetch}");

            _logger.LogDebug("[GameService:{GameId}:FetchCardData] Fetched definition for {CardName} ({CardId}).", gameId, cardDefinition.Name, cardDefinition.Id);
            return (cardDefinition, cardInHand);
        }

        // Validates preconditions for playing a card inside the main lock
        private void ValidatePlayCardPreconditions(GameStateDto gameState, PlayerStateDto playerState, CardDefinition cardDefinition, CardInstanceDto? cardInHand, PlayCardActionDto actionDto, string playerId) // Changed CardDto to CardInstanceDto?
        {
            _logger.LogDebug("[GameService:{GameId}:ValidatePlayCard] Validating preconditions for Player {PlayerId}, Card {CardName}.", gameState.GameId, playerId, cardDefinition.Name);
            if (gameState.IsGameOver) throw new GameLogicException("Game is already over.");
            if (gameState.CurrentPlayerId != playerId) throw new GameLogicException($"It's not player {playerId}'s turn.");
            if (gameState.Phase != GamePhase.Main) throw new GameLogicException($"Can only play cards during the Main phase (Current: {gameState.Phase}).");

            // Re-validate card still in hand inside lock
            // Compare InstanceId from CardInstanceDto with InstanceId from CardInstanceDto?
            if (!playerState.Hand.Any(c => c.InstanceId == cardInHand?.InstanceId)) // Changed cardInHand.Uid to cardInHand?.InstanceId (added null check)
                throw new GameLogicException($"Card {cardInHand?.InstanceId} disappeared from hand unexpectedly (inside lock validation)."); // Changed cardInHand.Uid to cardInHand?.InstanceId (added null check)

            // Null check for cardInHand before accessing Cost (though definition should exist if hand check passed)
            if (cardInHand == null) throw new GameLogicException("Internal error: Card instance became null after hand check.");
            if (playerState.CurrentPP < cardDefinition.Cost) throw new GameLogicException($"Not enough PP to play {cardDefinition.Name}. Requires {cardDefinition.Cost}, has {playerState.CurrentPP}.");

            const int MaxFieldSize = 7; // Define here or pass as argument
            if (!Enum.TryParse<CardType>(cardDefinition.Type, true, out var cardType))
            {
                throw new GameLogicException($"Unknown card type: {cardDefinition.Type}");
            }

            if (cardType == CardType.Follower || cardType == CardType.Amulet) // Both take field space
            {
                if (playerState.Field.Count >= MaxFieldSize)
                {
                    throw new GameLogicException($"Cannot play {cardDefinition.Name}, field is full ({playerState.Field.Count}/{MaxFieldSize}).");
                }
            }
            _logger.LogDebug("[GameService:{GameId}:ValidatePlayCard] Preconditions validated successfully.", gameState.GameId);
        }

        // Applies PP cost and removes card from hand inside the main lock - Accepts gameId for logging
        // M8 Change: Signature updated to use GameActionContext
        private void ApplyPlayCardCostAndRemoveCard(GameActionContext context, string gameId, PlayerStateDto playerState, CardInstanceDto? cardInHand, CardDefinition cardDefinition) // Changed CardDto to CardInstanceDto?
        {
            _logger.LogDebug("[GameService:{GameId}:ApplyCost] Applying cost and removing card {CardInstanceId} ({CardName}).", gameId, cardInHand?.InstanceId, cardDefinition.Name); // Use gameId param, changed Uid to InstanceId
            playerState.CurrentPP -= cardDefinition.Cost;
            // M8 Change: Use context to add event
            context.AddEvent(new PpUpdateEventDto(playerState.PlayerId, playerState.CurrentPP, playerState.MaxPP));

            // b. Remove card from hand
            // Ensure cardInHand is not null before attempting removal (already checked in ValidatePlayCardPreconditions, but defensive check here)
            if (cardInHand == null)
            {
                _logger.LogError("[GameService:{GameId}:ApplyCost] CRITICAL: cardInHand is null before attempting removal!", gameId);
                throw new GameLogicException("Internal error: Card instance became null unexpectedly.");
            }

            // Find the card instance in the hand list by InstanceId and remove it
            var itemToRemove = playerState.Hand.FirstOrDefault(c => c.InstanceId == cardInHand.InstanceId);
            bool removed = false;
            if (itemToRemove != null)
            {
                removed = playerState.Hand.Remove(itemToRemove);
            }

            if (!removed)
            {
                // This indicates a potentially serious state inconsistency
                _logger.LogError("[GameService:{GameId}:ApplyCost] CRITICAL: Failed to remove card {CardInstanceId} from hand after validation!", gameId, cardInHand.InstanceId); // Changed Uid to InstanceId
                // Consider if throwing an exception is appropriate here, or if the game can continue.
                // If this happens, the client's hand and server's hand are out of sync.
                // For now, log critical error but continue, though this might lead to further issues.
                // Optionally: throw new GameLogicException($"Failed to remove card {cardInHand.InstanceId} from hand.");
            }
            else
            {
                _logger.LogDebug("[GameService:{GameId}:ApplyCost] Card {CardInstanceId} removed from hand.", gameId, cardInHand.InstanceId); // Changed Uid to InstanceId
                                                                                                                                              // M8 Change: Use context to add event
                                                                                                                                              // Use null-conditional operator for safety, though cardInHand shouldn't be null here if removed is true
                                                                                                                                              // Removed HandUpdateEventDto creation - Rely on PpUpdateEventDto for client hand updates
            }

            playerState.PlayedCardCountThisTurn++; // Increment played count AFTER successful removal

            _logger.LogDebug("[GameService:{GameId}:ApplyCost] Cost applied. Player {PlayerId} PP: {CurrentPP}/{MaxPP}. Hand size: {HandCount}. Played count: {PlayedCount}.", // Removed "HandUpdateEvent added" as it's logged above
                gameId, playerState.PlayerId, playerState.CurrentPP, playerState.MaxPP, playerState.Hand.Count, playerState.PlayedCardCountThisTurn); // Use gameId param

            // --- BUG-011 DEBUG LOG 1/3 REMOVED (Contained reference to HandUpdateEventDto) ---
        }
        // Creates a new CardInstanceDto representing a follower entering the field
        private CardInstanceDto CreateCardInstanceForBattle(CardDefinition cardDefinition, string controllerPlayerId)
        {
            _logger.LogDebug("[GameService:CreateCardInstanceForField] Creating CardInstance for {CardName} ({CardId}) for Player {PlayerId}.",
                cardDefinition.Name, cardDefinition.Id, controllerPlayerId);

            // Use int CardId directly
            int parsedCardId = cardDefinition.Id;

            return new CardInstanceDto
            {
                InstanceId = Guid.NewGuid(), // Unique instance ID on the field
                CardId = parsedCardId,
                Name = cardDefinition.Name,
                Cost = cardDefinition.Cost,
                Type = cardDefinition.Type,
                Attack = cardDefinition.Attack ?? 0, // Base Attack
                Health = cardDefinition.Health ?? 0, // Base Health
                CurrentAttack = cardDefinition.Attack ?? 0, // Initial Current Attack
                CurrentHealth = cardDefinition.Health ?? 0, // Initial Current Health
                MaxHealth = cardDefinition.Health ?? 0, // Initial Max Health
                Description = cardDefinition.Description ?? string.Empty,
                Keywords = cardDefinition.Keywords ?? new List<string>(),
                CanEvolve = cardDefinition.EvolveId.HasValue, // Check if int? EvolveId has value
                IsEvolved = false,
                // Correctly set initial CanAttack based on Rush/Storm keywords
                CanAttack = cardDefinition.Keywords?.Contains("Rush", StringComparer.OrdinalIgnoreCase) == true ||
                            cardDefinition.Keywords?.Contains("Storm", StringComparer.OrdinalIgnoreCase) == true, // True if Rush or Storm, else False
                Zone = CardZone.Field, // Set zone to Field
                AttacksMadeThisTurn = 0,
                MaxAttacksPerTurn = 1, // Default to 1 attack per turn for followers
                ControllerPlayerId = controllerPlayerId, // Set the controller
                CurrentCountdown = null, // Followers don't have countdown
                // MaxCountdown = null, // Removed non-existent property
                MaxAttack = cardDefinition.Attack // Initialize MaxAttack
                // EffectDescriptions removed
            };
        }

        // Executes the logic for playing a Follower inside the main lock (BoardIndex removed)
        // M8 Change: Signature updated to use GameActionContext
        // M9 Change: cardFromHand type updated to CardInstanceDto?
        // Refactored: Changed unitState parameter to cardInstance (CardInstanceDto)
        private void ExecuteFollowerPlay(GameActionContext context, GameStateDto gameState, PlayerStateDto playerState, CardInstanceDto cardInstance, CardInstanceDto? cardFromHand, PlayCardActionDto actionDto)
        {
            _logger.LogDebug("[GameService:{GameId}:ExecuteFollower] Executing play for {UnitName} ({UnitInstanceId}).", gameState.GameId, cardInstance.Name, cardInstance.InstanceId); // Use InstanceId
            // V0.1 Simplified: Always add follower to the end of the field. BoardIndex logic removed.
            // Refactored: Add the CardInstanceDto directly
            playerState.Field.Add(cardInstance);
            // Add event for entity entering play
            // Use CardSummonedEventDto and pass cardFromHand as the summoned card instance
            // Note: cardFromHand represents the card *as it was in the hand*. cardInstance represents it *on the field*.
            // CardSummonedEventDto expects the card instance *as it is summoned*. Use cardInstance here.
            var entityEnteredEvent = new CardSummonedEventDto(playerState.PlayerId, cardInstance, "Played"); // Pass the instance entering the field
            context.AddEvent(entityEnteredEvent);
            // Update log to reference CardSummonedEventDto if needed, or keep generic unit log
            _logger.LogInformation("[GameService:{GameId}] Unit {UnitName} ({UnitInstanceId}) entered play for Player {PlayerId}. CardSummonedEvent added.", // Updated event name in log, use InstanceId
                gameState.GameId, cardInstance.Name, cardInstance.InstanceId, playerState.PlayerId); // Use InstanceId
            _logger.LogDebug("[GameService:{GameId}:ExecuteFollower] Added {UnitName} to end of field.", gameState.GameId, cardInstance.Name);

            // Convert Guid cardFromHand.Uid to string for DTO constructor
            // M8 Change: Use context to add event
            // Refactor: Replace UnitPlayedEventDto with CardPlayedEventDto
            // Use the constructor and initialize CardData
            context.AddEvent(new CardPlayedEventDto(playerState.PlayerId, cardFromHand)); // Corrected constructor call - This event uses the card *from hand*
            // _logger.LogWarning("[Roo Test:{GameId}] UnitPlayedEvent generation SKIPPED for {UnitName} ({UnitUid}).", gameState.GameId, unitState.Name, unitState.Uid); // Roo Test: Remove log
            _logger.LogInformation("[GameService:{GameId}] Follower {UnitName} ({UnitInstanceId}) played onto field for Player {PlayerId}. CardPlayedEvent added.", // Update log message, use InstanceId
                gameState.GameId, cardInstance.Name, cardInstance.InstanceId, playerState.PlayerId); // Use InstanceId
            // TODO: Fanfare (Future)
        }

        // Executes the logic for playing a Spell inside the main lock - Uses playerState.Graveyard
        // M8 Change: Signature updated to use GameActionContext
        // M9 Change: cardFromHand type updated to CardInstanceDto?
        private void ExecuteSpellPlay(GameActionContext context, GameStateDto gameState, PlayerStateDto playerState, CardInstanceDto? cardFromHand, CardDefinition cardDefinition, PlayCardActionDto actionDto)
        {
            // Use null-conditional operator ?. for logging as cardFromHand is now nullable
            _logger.LogDebug("[GameService:{GameId}:ExecuteSpell] Executing play for {CardName} ({CardInstanceId}).", gameState.GameId, cardFromHand?.Name, cardFromHand?.InstanceId); // Changed Uid to InstanceId
            // M8 Change: Use context to add event
            // Unified CardPlayedEventDto is used for all card types (Spell, Unit, Amulet)
            // SpellPlayedEventDto is deprecated and replaced by CardPlayedEventDto
            // Ensure cardFromHand is not null before accessing properties for the event
            if (cardFromHand != null)
            {
                // Use unified CardPlayedEventDto for spell playing
                context.AddEvent(new CardPlayedEventDto(playerState.PlayerId, cardFromHand));
            }
            else
            {
                _logger.LogWarning("[GameService:{GameId}:ExecuteSpell] cardFromHand was null, cannot generate CardPlayedEvent.", gameState.GameId);
            }


            // --- Target Finding and Validation ---
            ICard? targetEntity = null;
            if (!string.IsNullOrEmpty(actionDto.TargetUid))
            {
                _logger.LogDebug("[GameService:{GameId}:ExecuteSpell] Spell requires target. Finding entity with UID: {TargetUID}", gameState.GameId, actionDto.TargetUid);
                targetEntity = FindEntityByUid(gameState, actionDto.TargetUid); // Use existing helper

                if (targetEntity == null)
                {
                    // If target was specified but not found, this is an error
                    _logger.LogWarning("[GameService:{GameId}:ExecuteSpell] Specified target UID '{TargetUID}' not found.", gameState.GameId, actionDto.TargetUid);
                    // Throw an exception as playing a targeted spell without a valid target is an error
                    throw new GameLogicException($"Target entity with UID {actionDto.TargetUid} not found.");
                }
                else
                {
                    _logger.LogInformation("[GameService:{GameId}:ExecuteSpell] Found target entity: UID={TargetEntityUID}, Type={TargetEntityType}", gameState.GameId, targetEntity.Uid, targetEntity.GetType().Name);
                    // TODO: Validate if the spell *can* target this entity type (e.g., "Deal damage to an enemy follower")
                }
            }
            else
            {
                _logger.LogDebug("[GameService:{GameId}:ExecuteSpell] Spell does not require a target or none was provided.", gameState.GameId);
                // TODO: Validate if the spell definition *actually* doesn't require a target. Throw if it does but none was provided.
            }

            // --- Spell Effect Application (Placeholder) ---
            _logger.LogWarning("[GameService:{GameId}:ExecuteSpell] Placeholder: Spell effect logic for {CardName} ({CardId}) not yet implemented.", gameState.GameId, cardDefinition.Name, cardDefinition.Id);
            // TODO: Implement actual spell effects based on cardDefinition, using targetEntity if applicable.
            // Example: if (cardDefinition.Id == "S002") { ApplyDamage(targetEntity, 1, events, playerState.PlayerId, opponentId, cardFromHand.Uid.ToString(), "SpellEffect"); }


            // Add to graveyard (Use playerState)
            if (playerState.Graveyard == null) playerState.Graveyard = new List<CardInstanceDto>(); // Initialize if null, Changed type
            if (cardFromHand != null) // Explicit null check
            {
                playerState.Graveyard.Add(cardFromHand); // Add the actual CardInstanceDto instance
                // Use null-conditional operator ?. and InstanceId for logging
                _logger.LogDebug("[GameService:{GameId}:ExecuteSpell] Added card {CardInstanceId} to player {PlayerId}'s graveyard. Size: {GraveyardSize}", gameState.GameId, cardFromHand?.InstanceId, playerState.PlayerId, playerState.Graveyard.Count); // Changed Uid to InstanceId
            }
            else
            {
                _logger.LogWarning("[GameService:{GameId}:ExecuteSpell] cardFromHand was null, not adding to graveyard.", gameState.GameId);
            }


            // Generate GraveyardUpdateEvent (Card added to graveyard)
            // M8 Change: Use context to add event
            // Use null-conditional operator ?. for cardAdded as cardFromHand is nullable
            context.AddEvent(new GraveyardUpdateEventDto(playerState.PlayerId, playerState.Graveyard.Count, cardAdded: cardFromHand)); // Restore event generation
            // _logger.LogWarning("[Roo Test:{GameId}] GraveyardUpdateEvent generation SKIPPED for {CardName} in ExecuteSpellPlay.", gameState.GameId, cardFromHand.Name); // Roo Test: Remove log
            // Corrected truncated log message - Restore log message below
            // Use null-conditional operator ?. and InstanceId for logging
            _logger.LogDebug("[GameService:{GameId}:ExecuteSpellPlay] Added spell {CardInstanceId} to graveyard. GraveyardUpdateEvent added.", gameState.GameId, cardFromHand?.InstanceId); // Changed Uid to InstanceId
        } // Closing brace for ExecuteSpellPlay



        // Executes the logic for playing an Amulet (Simplified for V0.1)
        // M8 Change: Signature updated to use GameActionContext
        // M9 Change: cardFromHand type updated to CardInstanceDto?
        private void ExecuteAmuletPlay(GameActionContext context, GameStateDto gameState, PlayerStateDto playerState, CardInstanceDto? cardFromHand, PlayCardActionDto actionDto)
        {
            // Simplified V0.1: Log and maybe add placeholder event. No state change yet.
            // Use null-conditional operator ?. for logging as cardFromHand is now nullable
            _logger.LogInformation("[GameService:{GameId}] Amulet {CardName} ({CardInstanceId}) played by Player {PlayerId}. (Logic Deferred in V0.1)",
                 gameState.GameId, cardFromHand?.Name, cardFromHand?.InstanceId, playerState.PlayerId); // Changed Uid to InstanceId

            // Add Amulet to graveyard (Consistent with Spell V0.1)
            if (playerState.Graveyard == null) playerState.Graveyard = new List<CardInstanceDto>(); // Changed type
            if (cardFromHand != null) // Explicit null check
            {
                // Use unified CardPlayedEventDto for amulet playing
                // AmuletPlayedEventDto is deprecated and replaced by CardPlayedEventDto
                context.AddEvent(new CardPlayedEventDto(playerState.PlayerId, cardFromHand));
                _logger.LogInformation("[GameService:{GameId}:ExecuteAmuletPlay] Added CardPlayedEvent for {AmuletName} ({AmuletInstanceId}).", gameState.GameId, cardFromHand.Name, cardFromHand.InstanceId);

                playerState.Graveyard.Add(cardFromHand); // Add the actual CardInstanceDto instance
                // M8 Change: Use context to add event
                // Use null-conditional operator ?. for cardAdded as cardFromHand is nullable
                context.AddEvent(new GraveyardUpdateEventDto(playerState.PlayerId, playerState.Graveyard.Count, cardAdded: cardFromHand)); // Restore event generation
                // Use null-conditional operator ?. and InstanceId for logging
                _logger.LogDebug("[GameService:{GameId}:ExecuteAmuletPlay] Added amulet {CardInstanceId} to player {PlayerId}'s graveyard. Size: {GraveyardSize}. GraveyardUpdateEvent added.", gameState.GameId, cardFromHand?.InstanceId, playerState.PlayerId, playerState.Graveyard.Count); // Changed Uid to InstanceId
            }
            else
            {
                _logger.LogWarning("[GameService:{GameId}:ExecuteAmuletPlay] cardFromHand was null, not adding to graveyard or sending GraveyardUpdateEvent.", gameState.GameId);
                // Note: GraveyardUpdateEvent is NOT sent if cardFromHand is null here. Decide if this is correct behavior.
            }
            // _logger.LogWarning("[Roo Test:{GameId}] GraveyardUpdateEvent generation SKIPPED for {CardName} in ExecuteAmuletPlay.", gameState.GameId, cardFromHand.Name); // Roo Test: Remove log
            // Use null-conditional operator ?. and InstanceId for logging
            _logger.LogDebug("[GameService:{GameId}:ExecuteAmuletPlay] Added amulet {CardInstanceId} to player {PlayerId}'s graveyard. Size: {GraveyardSize}. GraveyardUpdateEvent added.", gameState.GameId, cardFromHand?.InstanceId, playerState.PlayerId, playerState.Graveyard.Count); // Changed Uid to InstanceId


            // TODO: Add Amulet state handling, add to field, trigger effects (Future)
            // For V0.1, simply add to graveyard (done above)
            // Use null-conditional operator ?. and InstanceId for logging
            _logger.LogDebug("[GameService:{GameId}] Amulet {CardInstanceId} processing complete for V0.1 (added to graveyard).", gameState.GameId, cardFromHand?.InstanceId); // Changed Uid to InstanceId
        } // Closing brace for ExecuteAmuletPlay

        /// <summary>
        /// Public method to explicitly trigger the execution of an AI player's turn.
        /// Called by the Hub. Wraps the internal execution logic.
        /// </summary>
        public async Task TriggerAiTurnAsync(string gameId, string aiPlayerId)
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
        private async Task ExecuteAiActionPhaseAsync(string gameId, string aiPlayerId, string? previousPlayerId = null)
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

                var nextPlayer = gameState.Players.FirstOrDefault(p => p.PlayerId != aiPlayerId);

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
