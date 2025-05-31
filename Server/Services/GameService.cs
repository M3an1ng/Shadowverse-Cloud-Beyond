// Server/Services/GameService.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Hubs; // GameHub
using ShadowverseBeyond.Server.Hubs.ClientInterfaces; // IGameClient
using ShadowverseBeyond.Server.Dtos.SignalR;        // DTOs (Restored for GameEventDto base class)
// Removed: using ShadowverseBeyond.Server.Dtos.SignalR;        // DTOs (Caused Ambiguity)
using ShadowverseBeyond.Server.Dtos.Enums; // Add using for CardType enum
// Removed duplicate: using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Exceptions; // GameLogicException
using Shadowverse.Domain.Interfaces; // Corrected namespace for IBoardEntityState (and potentially others)
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
namespace ShadowverseBeyond.Server.Services
{
    public class GameService : IGameService
    {
        private readonly IHubContext<GameHub, IGameClient> _hubContext;
        private readonly ILogger<GameService> _logger;

        // Delegate type for event broadcasting handlers
        private delegate Task EventBroadcastHandler(string groupName, GameEventDto gameEvent);
        // Dictionary to map event types to their broadcast handlers
        private readonly Dictionary<Type, EventBroadcastHandler> _eventHandlers;

        // Remove Scoped ICardRepository injection from Singleton GameService constructor
        // private readonly ICardRepository _cardRepository;
        private readonly IServiceScopeFactory _serviceScopeFactory; // Inject IServiceScopeFactory instead
        private readonly IAiService _aiService; // Added for AI logic
        private readonly IServiceProvider _serviceProvider; // Inject IServiceProvider to resolve Scoped dependencies

        // Central Game State Storage
        private readonly ConcurrentDictionary<string, GameStateDto> _gameStates = new();
        // Lock objects per game to prevent concurrent modification of a single game's state
        private readonly ConcurrentDictionary<string, object> _gameLocks = new();
        // Flag to prevent re-entrant EndTurn processing for the same game - REMOVED by Roo
        // private readonly ConcurrentDictionary<string, bool> _isProcessingEndTurn = new();
        // Define the fixed 20-card test deck list
        private static readonly List<string> _fixedTestDeckCardIds = new List<string>
        {
            "F001", "F001", "F001", // Goblin x3
            "F002", "F002", "F002", // Fighter x3
            "F005", "F005", "F005", // Shield Guardian x3
            "F006", "F006", "F006", // Bellringer Angel x3
            "S001", "S001", "S001", // Insight x3
            "S002", "S002", "S002", // Angelic Snipe x3
            "F003",                 // Knight x1
            "F004"                  // Heavy Goblin x1
        };

        // Duplicate declaration removed by Roo.
        // Update constructor signature
        public GameService(IHubContext<GameHub, IGameClient> hubContext, ILogger<GameService> logger, IServiceScopeFactory serviceScopeFactory, IAiService aiService, IServiceProvider serviceProvider) // Inject IServiceProvider
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // Assign IServiceScopeFactory
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService)); // Assign IAiService
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)); // Assign IServiceProvider
            // _cardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository)); // Removed assignment

            // Initialize the event handler dictionary
            _eventHandlers = new Dictionary<Type, EventBroadcastHandler>
            {
                { typeof(TurnStartedEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).TurnStarted((TurnStartedEventDto)evt) },
                { typeof(TurnEndedEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).TurnEnded((TurnEndedEventDto)evt) },
                { typeof(PpUpdateEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).PpUpdate((PpUpdateEventDto)evt) },
                { typeof(EpUpdateEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).EpUpdate((EpUpdateEventDto)evt) },
                { typeof(HandUpdateEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).HandUpdate((HandUpdateEventDto)evt) },
                { typeof(DeckUpdateEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).DeckUpdate((DeckUpdateEventDto)evt) },
                { typeof(CardDrawnEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).CardDrawn((CardDrawnEventDto)evt) }, // Added CardDrawnEventDto handler
                { typeof(UnitPlayedEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).UnitPlayed((UnitPlayedEventDto)evt) },
                { typeof(SpellPlayedEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).SpellPlayed((SpellPlayedEventDto)evt) },
                { typeof(AmuletPlayedEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).AmuletPlayed((AmuletPlayedEventDto)evt) },
                { typeof(UnitEvolvedEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).UnitEvolved((UnitEvolvedEventDto)evt) },
                { typeof(UnitDestroyedEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).UnitDestroyed((UnitDestroyedEventDto)evt) },
                { typeof(AttackDeclaredEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).AttackDeclared((AttackDeclaredEventDto)evt) },
                { typeof(DamageDealtEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).DamageDealt((DamageDealtEventDto)evt) },
                { typeof(HealthChangedEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).HealthChanged((HealthChangedEventDto)evt) },
                { typeof(StatChangeEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).StatChange((StatChangeEventDto)evt) },
                { typeof(GraveyardUpdateEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).GraveyardUpdate((GraveyardUpdateEventDto)evt) },
                { typeof(GameOverEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).GameOver((GameOverEventDto)evt) },
                { typeof(PlayerMulliganCompletedEventDto), async (groupName, evt) => await _hubContext.Clients.Group(groupName).PlayerMulliganCompleted((PlayerMulliganCompletedEventDto)evt) }
                // Add other existing event types here if any were missed
            };
        }

        // Updated PlayCardAsync implementation
        // M8 Change: Signature updated to use GameActionContext and return Task
        public async Task PlayCardAsync(GameActionContext context, string gameId, string playerId, PlayCardActionDto actionDto)
        { // Start of PlayCardAsync body (replacement for line 64 onwards)
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} attempting PlayCard: CardUID={CardUID}, TargetUID={TargetUID}.",
                gameId, playerId, actionDto.CardUid, actionDto.TargetUid ?? "None");

            // M8 Change: Removed local events list creation

            // Step 1: Fetch card data asynchronously before acquiring the main lock
            CardDefinition? cardDefinition = null;
            CardDto? cardInHand = null;
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

                    switch (cardType)
                    {
                        case CardType.Follower:
                            var unitState = CreateUnitState(cardDefinition);
                            // M8 Change: Pass context instead of events
                            ExecuteFollowerPlay(context, gameState, playerState, unitState, cardInHand, actionDto);
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

            // Step 3: Return collected events
            // M8 Change: Method now returns Task, no events list to return
        } // End of PlayCardAsync method body

        // Implement EndTurnAsync
        // Implement EndTurnAsync (Refactored based on M7 Design Doc - Step 1: Refactor method signature and core calls)
        // Note: Helper methods ProcessEndOfTurn and ProcessStartOfTurn will be added in subsequent steps.
        // Updated signature to return tuple Task<(List<GameEventDto> Events, bool AiTurnTriggered)>
        // Roo Change: Update return type to match the actual return value (List<GameEventDto>)
        // V2 Refactor: Signature changed to return tuple including IsNextPlayerAi and Events.
        public async Task<(bool Success, bool IsNextPlayerAi, string? NextPlayerId, List<GameEventDto> Events)> EndTurnAsync(string gameId, string endingPlayerId)
        {
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} attempting EndTurn.", gameId, endingPlayerId);
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
                        endingPlayerId,                  // The ID received from the client request
                        gameState.CurrentPlayerId, // The ID currently stored in the server state
                        gameState.FirstPlayerId,   // Double-check who should be first
                                                   // Removed: gameState.TurnNumber,
                        gameState.Phase);          // Check phase as well
                    // --- END ADDED LOGGING ---

                    bool ignoreStaleRequest = false; // Flag to indicate stale request
                    if (gameState.CurrentPlayerId != endingPlayerId) // <<< The check that fails
                    {
                        // Log a warning instead of error for stale/duplicate EndTurn requests
                        _logger.LogWarning("[GameService:{GameId}] Received EndTurn request for player {ReceivedPlayerId} but current player is {ServerCurrentPlayerId}. Ignoring stale/duplicate request.",
                           gameId, endingPlayerId, gameState.CurrentPlayerId);
                        // --- ROO DEBUG: ADDING UNIQUE LOG BEFORE SETTING FLAG ---
                        _logger.LogError("!!!!!!!!!! ROO DEBUG: STALE EndTurn DETECTED for {PlayerId}. Setting ignoreStaleRequest = true. !!!!!!!!!!!", endingPlayerId);
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
                        return (Success: false, IsNextPlayerAi: actualIsNextPlayerAi, NextPlayerId: staleNextPlayerId, Events: new List<GameEventDto>());
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
                        if (gameState.CurrentPlayerId != endingPlayerId)
                        {
                            // This should theoretically not happen if the check at line 243 worked and ignoreStaleRequest was handled,
                            // unless the state changed between line 243 and here despite the lock.
                            _logger.LogError("[GameService:{GameId}] CRITICAL STATE MISMATCH DETECTED INSIDE LOCK (within !ignoreStaleRequest)! Player {PlayerId} initiated EndTurn, but CurrentPlayerId is now {CurrentPlayer}. Aborting processing.",
                                gameId, endingPlayerId, gameState.CurrentPlayerId);
                            // Skip the entire turn processing logic below if mismatch detected here.
                        }
                        else
                        {
                            // --- Original processing logic starts here ---
                            // 3. Get current and opponent player states
                            var currentPlayerState = gameState.Players.FirstOrDefault(p => p.PlayerId == endingPlayerId);
                            var opponentPlayerState = gameState.Players.FirstOrDefault(p => p.PlayerId != endingPlayerId);

                            if (currentPlayerState == null || opponentPlayerState == null)
                            {
                                // Should not happen if game state is valid
                                _logger.LogError("[GameService:{GameId}] Critical error: Player state not found for player {PlayerId} or opponent.", gameId, endingPlayerId);
                                throw new GameLogicException($"Player state not found for game {gameId}.");
                            }

                            _logger.LogDebug("[GameService:{GameId}] Validation successful for EndTurn by {PlayerId}.", gameId, endingPlayerId);

                            // 4. Call ProcessEndOfTurn for the current player
                            _logger.LogWarning("[GS_DEBUG_ENDTURN:{GameId}] Lock acquired. Before ProcessEndOfTurn for {PlayerId}. Current gameState.CurrentPlayerId = {CurrentPlayer}", gameId, currentPlayerState.PlayerId, gameState.CurrentPlayerId); // DEBUG LOG
                            events.AddRange(ProcessEndOfTurn(currentPlayerState, gameState)); // Call commented out until helper exists
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

                            // Roo Change START: Remove ProcessStartOfTurn call from EndTurnAsync
                            // The StartPlayerTurnAsync will be called separately by the Hub or AiTurnExecutor.
                            // We still need to determine the next player and if they are AI for the trigger logic below.
                            // if (!isNextPlayerAi || currentPlayerState.IsAI) // Condition no longer needed here
                            // {
                            //     _logger.LogInformation("[GameService:{GameId}] Intentionally NOT processing StartOfTurn for {NextPlayerId} within EndTurnAsync.",
                            //         gameId, nextPlayerId);
                            // }
                            // else // Condition no longer needed here
                            // {
                            // Roo Change END
                            // Log that we are intentionally skipping the AI's ProcessStartOfTurn here.
                            _logger.LogInformation("[GameService:{GameId}] Skipping ProcessStartOfTurn for AI player {NextPlayerId} within human player {PlayerId}'s EndTurnAsync call. AI turn will be triggered separately.",
                                gameId, nextPlayerId, endingPlayerId);
                            // Do NOT call ProcessStartOfTurn or add its events here for the AI.
                        }

                        // Store next player info for AI trigger check outside lock
                        _logger.LogDebug("[GameService:{GameId}] Stored next player info inside lock: NextPlayerId={NextId}, IsAI={IsAI}", gameId, nextPlayerId, isNextPlayerAi);
                        // _logger.LogInformation("[GameService:{GameId}] Called ProcessStartOfTurn for next player {OpponentId}.", gameId, opponentPlayerState.PlayerId); // Removed GlobalTurn from log // Original log


                        // 7. Log completion
                        _logger.LogInformation("[GameService:{GameId}] EndTurn processing completed for {PlayerId}.", gameId, endingPlayerId);
                        // V2 Change: Store the determined next player ID and mark success for return after lock
                        resultPlayerId = nextPlayerId;
                        success = true; // Mark as successful if we reached here without throwing inside lock
                        // Removed previous placeholder log and comment from original line 232
                    }
                    // ===>>> END ADDED CHECK <<<===
                }
            } // End lock block
            catch (GameLogicException ex) // Catch blocks restored after lock
            {
                _logger.LogWarning(ex, "[GameService:{GameId}] Game Logic Error during EndTurn (inside lock) for Player {PlayerId}.", gameId, endingPlayerId);
                // Re-throw for the Hub filter to handle
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Unexpected error during EndTurn (inside lock) for Player {PlayerId}.", gameId, endingPlayerId);
                // Wrap and re-throw
                throw new GameLogicException("An unexpected error occurred while ending the turn.", ex);
            }

            // Return the stored result tuple after the lock has been released
            _logger.LogDebug("[GameService:{GameId}] Exiting EndTurnAsync. Success: {Success}, IsNextAI: {IsNextAI}, NextPlayer: {NextPlayer}, EventCount: {EventCount}",
                gameId, success, isNextPlayerAi, resultPlayerId ?? "null", events.Count);
            return (Success: success, IsNextPlayerAi: isNextPlayerAi, NextPlayerId: resultPlayerId, Events: events);
            // V2 Change: AI Trigger logic removed from EndTurnAsync. It should be handled by the caller (e.g., GameHub or AiTurnExecutor).
        } // This closes EndTurnAsync

        /// &lt;summary&gt;
        /// Processes the start-of-turn logic for the specified player.
        /// This includes incrementing turn count, updating PP/EP, drawing cards,
        /// resetting unit states, and generating necessary start-of-turn events.
        /// This method is called after mulligan completion for the first player,
        /// and by EndTurnAsync (for human players) or AiTurnExecutor (for AI players)
        /// for subsequent turns.
        /// &lt;/summary&gt;
        /// &lt;param name="gameId"&gt;The ID of the game instance.&lt;/param&gt;
        /// &lt;param name="playerId"&gt;The ID of the player whose turn is starting.&lt;/param&gt;
        /// &lt;returns&gt;A list of game events generated during start-of-turn processing.&lt;/returns&gt;
        /// &lt;exception cref="GameLogicException"&gt;Thrown if the game or player state is invalid.&lt;/exception&gt;
        // V2 Refactor: Signature changed to return tuple (Events, IsAiTurn) and use startingPlayerId parameter.
        public async Task<(List<GameEventDto> Events, bool IsAiTurn)> StartPlayerTurnAsync(string gameId, string startingPlayerId)
        {
            _logger.LogInformation("[GameService:{GameId}] Executing StartPlayerTurnAsync for Player {StartingPlayerId}.", gameId, startingPlayerId);
            var events = new List<GameEventDto>();

            // Acquire lock for this specific game
            bool isAiTurnResult = false; // Variable to store AI status, declared outside lock
            object gameLock = _gameLocks.GetOrAdd(gameId, _ => new object()); // Fixed lambda syntax
            lock (gameLock)
            {
                // 1. Validate gameId and retrieve GameState.
                // bool isAiTurnResult = false; // Declaration moved outside lock
                if (!_gameStates.TryGetValue(gameId, out var gameState))
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
                isAiTurnResult = startingPlayerState.IsAI;
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
                events.Add(new PpUpdateEventDto(startingPlayerState.PlayerId, startingPlayerState.CurrentPP, startingPlayerState.MaxPP));
                _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} PP updated: {CurrentPP}/{MaxPP}. Event added.",
                    gameState.GameId, startingPlayerState.PlayerId, startingPlayerState.CurrentPP, startingPlayerState.MaxPP);

                // --- M7 Design Doc Step 5.3: Update Player EP ---
                int requiredEvoTurn = startingPlayerState.IsFirst ? 5 : 4; // P1 needs Turn 5, P2 needs Turn 4
                bool canGainEp = currentPlayerTurn >= requiredEvoTurn;
                int previousEP = startingPlayerState.CurrentEP; // Store previous EP for logging/event
                if (canGainEp && startingPlayerState.CurrentEP < startingPlayerState.MaxEP)
                {
                    startingPlayerState.CurrentEP++;
                    _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} gained EP (PlayerTurn {PlayerTurn} &gt;= Required {ReqTurn}). New EP: {CurrentEP}/{MaxEP}.",
                       gameState.GameId, startingPlayerState.PlayerId, currentPlayerTurn, requiredEvoTurn, startingPlayerState.CurrentEP, startingPlayerState.MaxEP); // Log uses currentPlayerTurn
                }
                if (startingPlayerState.CurrentEP != previousEP)
                {
                    events.Add(new EpUpdateEventDto(startingPlayerState.PlayerId, startingPlayerState.CurrentEP, startingPlayerState.MaxEP));
                    _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} EP changed. New EP: {CurrentEP}/{MaxEP}. Event added.",
                       gameState.GameId, startingPlayerState.PlayerId, startingPlayerState.CurrentEP, startingPlayerState.MaxEP);
                } // <<< Closing brace for the if block

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
                List<CardDto> drawnCards = DrawCards(startingPlayerState, cardsToDraw); // Use helper
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
                    // Hand Update (Size Change)
                    // Pass the entire list of drawn cards to the HandUpdateEventDto constructor
                    events.Add(new HandUpdateEventDto(startingPlayerState.PlayerId, startingPlayerState.Hand.Count, drawnCards)); // Pass the list to the 3rd parameter
                    // Deck Update (Size Change)
                    events.Add(new DeckUpdateEventDto(startingPlayerState.PlayerId, startingPlayerState.DeckCount));
                    // Individual Card Drawn Events (for animations/triggers)
                    foreach (var card in drawnCards)
                    {
                        // Convert Guid card.Uid to string for DTO constructor
                        events.Add(new CardDrawnEventDto(startingPlayerState.PlayerId, card.Uid.ToString(), card.CardId));
                    }
                    _logger.LogDebug("[GameService:{GameId}] Added HandUpdate, DeckUpdate, and {DrawnCount} CardDrawn events.",
                        gameState.GameId, drawnCards.Count);
                }

                // --- M7 Design Doc Step 5.5: Reset Unit States ---
                foreach (var unit in startingPlayerState.Field)
                {
                    // Reset CanAttack for units that didn't just arrive (unless they have Storm)
                    // Simplified V0.1: Always allow attack at start of turn if unit survived.
                    // More complex logic (Summoning Sickness) can be added later.
                    unit.CanAttack = true; // Reset attack permission
                                           // Note: AttacksMadeThisTurn is reset in ProcessEndOfTurn
                }
                _logger.LogDebug("[GameService:{GameId}] Reset CanAttack flag for Player {PlayerId}'s units.", gameState.GameId, startingPlayerState.PlayerId);

                // --- M7 Design Doc Step 5.6: Generate TurnStartedEvent ---
                // Use the player's current turn number
                events.Add(new TurnStartedEventDto(startingPlayerState.PlayerId, currentPlayerTurn, gameState.Phase.ToString())); // Added gameState.Phase.ToString()
                _logger.LogInformation("[GameService:{GameId}] Generated TurnStartedEvent for Player {PlayerId}, PlayerTurn {PlayerTurn}, Phase {Phase}.", // Added Phase to log
                    gameState.GameId, startingPlayerState.PlayerId, currentPlayerTurn, gameState.Phase);

                // --- End of logic adapted from ProcessStartOfTurn ---
            } // <<< Closing brace for the lock block
            // V2 Change: Return tuple using the determined AI status stored in isAiTurnResult
            _logger.LogDebug("[GameService:{GameId}] StartPlayerTurnAsync finished for {StartingPlayerId}. Is AI Turn: {IsAiTurnResult}. Event Count: {EventCount}", gameId, startingPlayerId, isAiTurnResult, events.Count);
            return (events, isAiTurnResult);
        } // <<< Closing brace for StartPlayerTurnAsync method
        // Implement SubmitMulliganAsync from IGameService
        // Removed duplicate signature inserted by previous diff
        public async Task SubmitMulliganAsync(string gameId, string playerId, List<Guid> cardUidsToReplace)
        {
            _logger.LogInformation("[GameService] Player {PlayerId} in Game {GameId} attempting SubmitMulligan with {CardCount} cards.",
                playerId, gameId, cardUidsToReplace?.Count ?? 0);
            // Acquire lock for this specific game
            object gameLock = _gameLocks.GetOrAdd(gameId, _ => new object());

            // Variables to store results from lock block for use outside
            GameStateDto? stateToBroadcast = null;
            List<GameEventDto>? firstTurnStartEvents = null; // Declare outside lock, initialize null
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
                        var currentHandUids = playerState.Hand.Select(c => c.Uid).ToHashSet();
                        var invalidUids = cardUidsToReplace.Where(uid => !currentHandUids.Contains(uid)).ToList();
                        if (invalidUids.Any())
                        {
                            throw new GameLogicException($"Invalid card UIDs submitted for mulligan: {string.Join(", ", invalidUids)}");
                        }
                        _logger.LogWarning("--- DEBUG MULLIGAN: Human Replace - UIDs Validated ---"); // DEBUG LOG

                        // 2. Separate cards
                        var cardsToReplace = playerState.Hand.Where(c => cardUidsToReplace.Contains(c.Uid)).ToList();
                        var cardsToKeep = playerState.Hand.Where(c => !cardUidsToReplace.Contains(c.Uid)).ToList();
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
                        List<CardDto> drawnCards = new List<CardDto>();
                        _logger.LogDebug("[GameService:{GameId}] Attempting to draw {DrawCount} new cards.", gameId, cardsToDrawCount);
                        for (int i = 0; i < cardsToDrawCount; i++)
                        {
                            if (playerState.Deck.Any())
                            {
                                CardDto drawnCard = playerState.Deck[0];
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
                            var currentHandUids = opponentState.Hand.Select(c => c.Uid).ToHashSet();
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
                                var cardsToReplace = opponentState.Hand.Where(c => aiCardsToReplaceUids.Contains(c.Uid.ToString())).ToList();
                                // Convert Guid c.Uid to string for List<string>.Contains comparison
                                var cardsToKeep = opponentState.Hand.Where(c => !aiCardsToReplaceUids.Contains(c.Uid.ToString())).ToList();
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
                                List<CardDto> drawnCards = DrawCards(opponentState, cardsToDrawCount); // Use DrawCards helper
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
                if (shouldStartFirstTurn && !string.IsNullOrEmpty(startingPlayerIdForTurn1))
                {
                    _logger.LogInformation("[GameService:{GameId}] Triggering StartPlayerTurnAsync for first turn (outside lock). Player: {PlayerId}", gameId, startingPlayerIdForTurn1);
                    try
                    {
                        // Explicitly use 'this' qualifier
                        // Capture the tuple result
                        var startTurnResult = await this.StartPlayerTurnAsync(gameId, startingPlayerIdForTurn1);
                        firstTurnStartEvents = startTurnResult.Events; // Assign events part
                        _logger.LogInformation("[GameService:{GameId}] StartPlayerTurnAsync completed for first turn. Is AI Turn: {IsAiTurn}. Generated {EventCount} events.", gameId, startTurnResult.IsAiTurn, firstTurnStartEvents?.Count ?? 0); // Update log

                        // Trigger AI's first turn if applicable
                        if (startTurnResult.IsAiTurn)
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
                    if (firstTurnStartEvents != null && firstTurnStartEvents.Any())
                    {
                        _logger.LogInformation("[GameService:{GameId}] Broadcasting {EventCount} start-of-turn events (outside lock).", gameId, firstTurnStartEvents.Count);
                        foreach (var gameEvent in firstTurnStartEvents) // Broadcast each event individually
                        {
                            // Assuming BroadcastEventAsync exists or will be added
                            await BroadcastEventAsync(gameId, gameEvent);
                        }
                    }
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
                        var allCardDefinitionsEnumerable = await cardRepository.GetAllAsync();
                        // Filter for collectible cards BEFORE converting to list
                        var collectibleCardDefinitions = allCardDefinitionsEnumerable.Where(c => c.IsCollectible).ToList();
                        allCardDefinitions = collectibleCardDefinitions; // Assign the filtered list
                        _logger.LogInformation("[GameService:{GameId}] Fetched {Count} collectible card definitions.", gameId, allCardDefinitions.Count);
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
                List<CardDto> player1Deck = new List<CardDto>(targetDeckSize);
                List<CardDto> player2Deck = new List<CardDto>(targetDeckSize);

                // Create a lookup dictionary for efficient access to definitions
                var definitionLookup = allCardDefinitions.ToDictionary(def => def.Id);

                // Validate if all required definitions were fetched
                var missingDefs = _fixedTestDeckCardIds.Distinct().Where(id => !definitionLookup.ContainsKey(id)).ToList();
                if (missingDefs.Any())
                {
                    _logger.LogError("[GameService:{GameId}] Missing required card definitions for test deck: {MissingIds}. Cannot create game state.", gameId, string.Join(", ", missingDefs));
                    return null; // Cannot proceed
                }

                // Build decks using the fixed list of card IDs
                foreach (var cardId in _fixedTestDeckCardIds)
                {
                    if (definitionLookup.TryGetValue(cardId, out var cardDef))
                    {
                        // Create unique CardDto instances for each player's deck
                        player1Deck.Add(new CardDto
                        {
                            Uid = Guid.NewGuid(), // Assign Guid directly
                            CardId = cardDef.Id,
                            Name = cardDef.Name,
                            Cost = cardDef.Cost,
                            Type = cardDef.Type,
                            BaseAttack = cardDef.Attack,
                            BaseHealth = cardDef.Health,
                            // Initialize Current stats from Base stats for initial deck cards
                            CurrentAttack = cardDef.Attack,
                            CurrentHealth = cardDef.Health,
                            Keywords = cardDef.Keywords ?? new List<string>()
                        });

                        player2Deck.Add(new CardDto
                        {
                            Uid = Guid.NewGuid(), // Assign Guid directly
                            CardId = cardDef.Id,
                            Name = cardDef.Name,
                            Cost = cardDef.Cost,
                            Type = cardDef.Type,
                            BaseAttack = cardDef.Attack,
                            BaseHealth = cardDef.Health,
                            // Initialize Current stats from Base stats for initial deck cards
                            CurrentAttack = cardDef.Attack,
                            CurrentHealth = cardDef.Health,
                            Keywords = cardDef.Keywords ?? new List<string>()
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
                List<CardDto> player1Hand = DrawInitialHand(player1Deck, initialHandSize);
                List<CardDto> player2Hand = DrawInitialHand(player2Deck, initialHandSize);
                // Deck count in PlayerStateDto will be updated later when assigning the deck

                // Decide first player randomly
                bool player1IsFirst = new Random().Next(0, 2) == 0;
                string firstPlayerId = player1IsFirst ? player1Id : player2Id;
                string secondPlayerId = player1IsFirst ? player2Id : player1Id;

                var player1State = new PlayerStateDto
                {
                    PlayerId = player1Id,
                    IsAI = false, // Assuming player 1 is human
                    LeaderHealth = 20,
                    MaxLeaderHealth = 20,
                    CurrentPP = 0, // Start at 0 PP before turn 1 begins
                    MaxPP = 0,
                    CurrentEP = 0,
                    MaxEP = player1IsFirst ? 2 : 3,    // Assuming T4/T5 evo points (P1/P2) - adjust later?
                    CanEvolve = false,
                    Hand = player1Hand,
                    Deck = player1Deck, // Store the actual deck
                    DeckCount = player1Deck.Count, // Sync count after draw
                    Field = new List<UnitStateDto>(),
                    IsFirst = player1IsFirst,
                    PlayedCardCountThisTurn = 0, // Reset counter
                    TurnNumber = 0, // Initialize TurnNumber
                    Graveyard = new List<CardDto>() // Initialize Graveyard
                };

                var player2State = new PlayerStateDto
                {
                    PlayerId = player2Id,
                    IsAI = true, // Assuming player 2 is AI
                    LeaderHealth = 20,
                    MaxLeaderHealth = 20,
                    CurrentPP = 0,
                    MaxPP = 0,
                    CurrentEP = 0,
                    MaxEP = player1IsFirst ? 3 : 2,    // EP Max
                    CanEvolve = false,
                    Hand = player2Hand,
                    Deck = player2Deck, // Store the actual deck
                    DeckCount = player2Deck.Count, // Sync count after draw
                    Field = new List<UnitStateDto>(),
                    IsFirst = !player1IsFirst,
                    PlayedCardCountThisTurn = 0, // Reset counter
                    TurnNumber = 0, // Initialize TurnNumber
                    Graveyard = new List<CardDto>() // Initialize Graveyard
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
                    string player1HandDetails = string.Join(", ", player1State.Hand.Select(c => $"{{UID='{c.Uid}', Name='{c.Name}', ID='{c.CardId}'}}"));
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
        public async Task<List<GameEventDto>> AttackAsync(string gameId, string playerId, AttackActionDto actionDto)
        {
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} executing Attack: Attacker={AttackerUid}, Target={TargetUid}.",
                gameId, playerId, actionDto.AttackerUid, actionDto.TargetUid);

            var events = new List<GameEventDto>();

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
                var attacker = currentPlayer.Field.FirstOrDefault(u => u.Uid.ToString() == actionDto.AttackerUid);
                if (attacker == null)
                {
                    throw new GameLogicException($"Attacker unit (UID: {actionDto.AttackerUid}) not found on player {playerId}'s field.");
                }
                // TODO: Get actual CardType enum values if different
                if (attacker.Type != CardType.Follower.ToString()) // Check CardType enum value
                {
                    throw new GameLogicException($"Only Followers can attack. Entity type: {attacker.Type}");
                }
                if (!attacker.CanAttack)
                {
                    throw new GameLogicException($"Attacker unit {attacker.Name} (UID: {actionDto.AttackerUid}) cannot attack this turn.");
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
                else if (targetEntity is UnitStateDto targetUnit)
                {
                    isTargetFriendly = currentPlayer.Field.Any(u => u.Uid == targetUnit.Uid);
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
                    if (!(targetEntity is UnitStateDto targetWardUnit && opponentUnitsWithWard.Any(w => w.Uid == targetWardUnit.Uid)))
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
                // events.Add(new AttackDeclaredEventDto(playerId, attacker.Uid.ToString(), actionDto.TargetUid)); // Roo Test: Isolate SpellPlayed - Commented out
                _logger.LogWarning("[Roo Test:{GameId}] AttackDeclaredEvent generation SKIPPED.", gameState.GameId); // Roo Test: Add log
                _logger.LogDebug("[GameService:{GameId}] Generated AttackDeclaredEvent (Player: {PlayerId}, Attacker: {AttackerUid}, Target: {TargetUid})",
                    gameId, playerId, attacker.Uid, actionDto.TargetUid);
                // Phase 2: Clash Timing (Placeholder)
                // TODO: Implement Clash effects if target is Unit

                // Phase 3: Damage Timing
                _logger.LogDebug("[GameService:{GameId}] Entering Damage Phase.", gameId);
                int attackerAttack = attacker.CurrentAttack ?? 0;
                // Add null-forgiving operator (!) to satisfy non-nullable parameter type
                // Pass necessary context to ApplyDamage for DamageDealtEventDto
                ApplyDamage(targetEntity, attackerAttack, events, currentPlayer.PlayerId, opponentPlayer.PlayerId, attacker.Uid.ToString(), "Attack"); // CALL HELPER

                // Counter-attack if target is a unit and attacker is still alive
                // Add null-forgiving operator (!)
                if (targetEntity is UnitStateDto defendingUnit && (attacker.CurrentHealth ?? 0) > 0) // Check attacker health before counter
                {
                    _logger.LogDebug("[GameService:{GameId}] Target is unit, initiating counter-attack.", gameId);
                    int defenderAttack = defendingUnit.CurrentAttack ?? 0;
                    // Check if defender is still alive before counter-attacking
                    if ((defendingUnit.CurrentHealth ?? 0) > 0)
                    {
                        // Explicit cast for attacker (UnitStateDto) to IBoardEntityState needed for ApplyDamage
                        // Pass necessary context for counter-attack damage event
                        ApplyDamage((IBoardEntityState)attacker, defenderAttack, events, opponentPlayer.PlayerId, currentPlayer.PlayerId, defendingUnit.Uid.ToString(), "Attack"); // CALL HELPER
                    }
                    else
                    {
                        _logger.LogDebug("[GameService:{GameId}] Defending unit {DefenderUid} destroyed before counter-attack.", gameId, defendingUnit.Uid);
                    }
                }

                // Phase 4: Death Timing
                _logger.LogDebug("[GameService:{GameId}] Entering Death Phase.", gameId);
                // Explicit cast for attacker (UnitStateDto) to IBoardEntityState needed for CheckAndHandleDeath
                CheckAndHandleDeath((IBoardEntityState)attacker, currentPlayer, opponentPlayer, gameState, events); // CALL HELPER
                if (!gameState.IsGameOver) // Check if game ended during attacker's death check
                {
                    // Add null-forgiving operator (!) to satisfy non-nullable parameter type
                    CheckAndHandleDeath(targetEntity!, opponentPlayer, currentPlayer, gameState, events); // CALL HELPER
                }

                _logger.LogInformation("[GameService:{GameId}] Attack execution completed for Player {PlayerId}. Events generated: {EventCount}", gameId, playerId, events.Count);
                return await Task.FromResult(events); // Suppress CS1998 by awaiting a completed task
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
        public async Task<List<GameEventDto>> EvolveAsync(string gameId, string playerId, EvolveActionDto actionDto)
        {
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} attempting Evolve: Unit={UnitUID}",
                gameId, playerId, actionDto.UnitUid);

            var events = new List<GameEventDto>();

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

                var targetUnit = playerState.Field.FirstOrDefault(u => u.Uid.ToString() == actionDto.UnitUid);
                if (targetUnit == null) throw new GameLogicException($"Target unit with UID {actionDto.UnitUid} not found on player {playerId}'s board.");

                // --- Evolution Eligibility Validation (Chunk 2 starts next) ---
                // 4. Evolution Eligibility Validation - Chunk 2/5 (Corrected)
                // a. Check EP Cost (Assume 1 EP for now)
                const int evolveEpCost = 1;
                if (playerState.CurrentEP < evolveEpCost) throw new GameLogicException($"Not enough EP to evolve. Requires {evolveEpCost}, has {playerState.CurrentEP}.");

                // b. Check CanEvolve Flag
                if (!playerState.CanEvolve) throw new GameLogicException("Player cannot evolve units this turn (likely already evolved or turn restriction).");
                // Added Check: Unit Already Evolved
                if (targetUnit.IsEvolved) throw new GameLogicException($"Unit {targetUnit.Name} ({targetUnit.Uid}) is already evolved.");


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
                var baseCardDef = await cardRepository.GetCardByIdAsync(targetUnit.CardId);
                if (baseCardDef == null) throw new GameLogicException($"Base card definition not found for CardId: {targetUnit.CardId}");
                if (string.IsNullOrEmpty(baseCardDef.EvolveId)) throw new GameLogicException($"Card {baseCardDef.Name} ({baseCardDef.Id}) does not have an evolved form defined.");

                var evolvedCardDef = await cardRepository.GetCardByIdAsync(baseCardDef.EvolveId);
                if (evolvedCardDef == null) throw new GameLogicException($"Evolved card definition not found for EvolvedCardId: {baseCardDef.EvolveId}");

                // --- Perform Evolution (Chunk 4 starts next) ---
                // --- Perform Evolution (Chunk 4/5) ---
                _logger.LogDebug("[GameService:{GameId}] Performing evolution for Unit {UnitName} ({UnitUid}).", gameId, targetUnit.Name, targetUnit.Uid);

                // a. Deduct EP Cost
                playerState.CurrentEP -= evolveEpCost;
                // Corrected to use constructor
                var epEvent = new EpUpdateEventDto(playerId, playerState.CurrentEP, playerState.MaxEP);
                // events.Add(epEvent); // Roo Test: Isolate SpellPlayed - Commented out
                _logger.LogWarning("[Roo Test:{GameId}] EpUpdateEvent generation SKIPPED in EvolveAsync.", gameState.GameId); // Roo Test: Add log
                _logger.LogDebug("[GameService:{GameId}] Deducted {Cost} EP. Player {PlayerId} EP: {CurrentEP}/{MaxEP}.", gameId, evolveEpCost, playerId, playerState.CurrentEP, playerState.MaxEP);


                // b. Update Unit State using 'with' expression for immutability
                var originalUnitState = targetUnit; // Keep original for comparison/event data if needed
                var evolvedUnitState = originalUnitState with
                {
                    // Update core stats from evolved definition
                    CardId = evolvedCardDef.Id, // Update CardId to evolved version
                    Name = evolvedCardDef.Name,
                    CurrentAttack = evolvedCardDef.Attack,
                    MaxAttack = evolvedCardDef.Attack, // Evolved form starts with full stats
                    CurrentHealth = evolvedCardDef.Health,
                    MaxHealth = evolvedCardDef.Health, // Evolved form starts with full stats
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
                int unitIndex = playerState.Field.FindIndex(u => u.Uid == targetUnit.Uid);
                if (unitIndex != -1)
                {
                    playerState.Field[unitIndex] = evolvedUnitState;
                    _logger.LogDebug("[GameService:{GameId}] Unit {UnitUid} state updated to evolved form in player's field.", gameId, targetUnit.Uid);
                }
                else
                {
                    // Should not happen if validation passed, but log defensively
                    _logger.LogError("[GameService:{GameId}] CRITICAL: Evolving unit {UnitUid} not found in player's field during update phase!", gameId, targetUnit.Uid);
                    throw new GameLogicException($"Internal error: Evolving unit {targetUnit.Uid} disappeared.");
                }


                // c. Generate Stat Change & Unit Evolved Events
                // Corrected StatChangeEventDto initialization based on actual definition
                // Corrected: Use constructor with sourcePlayerId (playerId is the source in Evolve context)
                var statEvent = new StatChangeEventDto(playerId, evolvedUnitState.Uid.ToString(), evolvedUnitState.CurrentAttack, evolvedUnitState.CurrentHealth, evolvedUnitState.MaxHealth);
                // events.Add(statEvent); // Roo Test: Isolate SpellPlayed - Commented out
                _logger.LogWarning("[Roo Test:{GameId}] StatChangeEvent generation SKIPPED in EvolveAsync.", gameState.GameId); // Roo Test: Add log

                // Corrected UnitEvolvedEventDto initialization based on actual definition
                var evolvedEvent = new UnitEvolvedEventDto(playerId, targetUnit.Uid.ToString(), evolvedUnitState);
                // events.Add(evolvedEvent); // Roo Test: Isolate SpellPlayed - Commented out
                _logger.LogWarning("[Roo Test:{GameId}] UnitEvolvedEvent generation SKIPPED in EvolveAsync.", gameState.GameId); // Roo Test: Add log
                _logger.LogDebug("[GameService:{GameId}] Generated StatChange and UnitEvolved events for {UnitUid}.", gameId, evolvedUnitState.Uid);


                // d. Update Player State (Prevent further evolution this turn)
                playerState.CanEvolve = false;
                _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} CanEvolve flag set to false for the rest of the turn.", gameId, playerId);
                // --- Broadcasting & Cleanup (Chunk 5/5) ---
                _logger.LogInformation("[GameService:{GameId}] Evolution logic completed for Player {PlayerId}, Unit {UnitUid}. Returning {EventCount} events.",
                    gameId, playerId, evolvedUnitState.Uid, events.Count);

                return events; // Return the collected events
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
        private List<CardDto> DrawCards(PlayerStateDto playerState, int drawCount)
        {
            // --- Roo Debug: Log DrawCards Entry ---
            _logger.LogWarning("[DEBUG_DRAW_HELPER] Entering DrawCards for Player {PlayerId}. Requesting {DrawCount} cards. Initial Deck Count: {InitialDeckCount}",
                playerState.PlayerId, drawCount, playerState.Deck.Count);
            // --- End Roo Debug ---

            // Assuming GameId is accessible via playerState or context if needed for detailed logging
            _logger.LogDebug("[GameService] Player {PlayerId} attempting to draw {DrawCount} cards. Deck size: {DeckSize}",
                playerState.PlayerId, drawCount, playerState.Deck.Count);

            List<CardDto> drawnCards = new List<CardDto>();
            for (int i = 0; i < drawCount; i++)
            {
                if (playerState.Deck.Any())
                {
                    // --- Roo Debug: Log before accessing Deck[0] ---
                    _logger.LogWarning("[DEBUG_DRAW_HELPER] Inside loop (i={LoopIndex}), Player {PlayerId}. Attempting to access Deck[0]. Current Deck Count: {DeckCount}",
                        i, playerState.PlayerId, playerState.Deck.Count);
                    // --- End Roo Debug ---
                    CardDto drawnCard = playerState.Deck[0];
                    drawnCards.Add(drawnCard);
                    playerState.Deck.RemoveAt(0);
                    // --- Roo Debug: Log after removing from Deck ---
                    _logger.LogWarning("[DEBUG_DRAW_HELPER] Inside loop (i={LoopIndex}), Player {PlayerId}. Removed card {CardId} ({CardUid}) from deck. New Deck Count: {DeckCount}",
                       i, playerState.PlayerId, drawnCard.CardId, drawnCard.Uid, playerState.Deck.Count);
                    // --- End Roo Debug ---
                }
                else
                {
                    _logger.LogWarning("[GameService] Attempted to draw from empty deck for Player {PlayerId}.", playerState.PlayerId);
                    // TODO: Implement fatigue/damage for drawing from empty deck? For now, just stop drawing.
                    break;
                }
            }
            playerState.DeckCount = playerState.Deck.Count; // Update deck count
            playerState.Hand.AddRange(drawnCards); // Add drawn cards to hand
            // --- Roo Debug: Log DrawCards Exit ---
            _logger.LogWarning("[DEBUG_DRAW_HELPER] Exiting DrawCards for Player {PlayerId}. Drawn cards count: {DrawnCount}. Final Hand Count: {HandCount}. Final Deck Count: {FinalDeckCount}",
                 playerState.PlayerId, drawnCards.Count, playerState.Hand.Count, playerState.DeckCount);
            // --- End Roo Debug ---
            _logger.LogDebug("[GameService] Player {PlayerId} drew {ActualDrawCount} cards. New hand size: {HandSize}, New deck size: {DeckSize}", // Keep original debug log too
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
        private List<GameEventDto> ProcessEndOfTurn(PlayerStateDto endingPlayerState, GameStateDto gameState)
        {
            // Removed TurnNumber from log
            _logger.LogInformation("[GameService:{GameId}] Processing EndOfTurn for Player {PlayerId}.",
                gameState.GameId, endingPlayerState.PlayerId);
            var events = new List<GameEventDto>();

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
            events.Add(new TurnEndedEventDto(endingPlayerState.PlayerId, opponentPlayerState.PlayerId)); // Use 2-argument constructor
                                                                                                         // Removed endingTurnNumber and {TurnNumber} from log as it no longer exists
            _logger.LogInformation("[GameService:{GameId}] Generated TurnEndedEvent. Next Player: {NextPlayerId}.",
                gameState.GameId, opponentPlayerState.PlayerId);


            _logger.LogInformation("[GameService:{GameId}] EndOfTurn processing complete for Player {PlayerId}. Events generated: {Count}", gameState.GameId, endingPlayerState.PlayerId, events.Count);
            return events;
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

        private void ShuffleDeck(List<CardDto> deck) // Start of ShuffleDeck
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
                CardDto value = deck[k];
                deck[k] = deck[n];
                deck[n] = value;
            }
        }

        private List<CardDto> DrawInitialHand(List<CardDto> deck, int handSize)
        {
            _logger.LogWarning("[GameService] Placeholder DrawInitialHand called.");
            // Placeholder: Draw cards from the top of the deck
            List<CardDto> hand = new List<CardDto>();
            int drawCount = Math.Min(handSize, deck.Count);
            for (int i = 0; i < drawCount; i++)
            {
                if (deck.Count > 0) // Check if deck is not empty
                {
                    hand.Add(deck[0]);
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

        private IBoardEntityState? FindEntityByUid(GameStateDto gameState, string uid)
        {
            foreach (var player in gameState.Players)
            {
                // Check if UID matches PlayerId (assuming PlayerId is the UID for leaders)
                if (player.PlayerId == uid)
                {
                    return player; // Found player
                }
                // Check units on the player's field
                // Ensure Uid is compared as string
                var unit = player.Field.FirstOrDefault(u => u.Uid.ToString() == uid);
                if (unit != null)
                {
                    return unit; // Found unit
                }
            }
            return null; // Not found
        }

        // Modified signature to accept necessary context for DamageDealtEventDto
        private void ApplyDamage(IBoardEntityState targetEntity, int damage, List<GameEventDto> events, string sourcePlayerId, string targetPlayerId, string? sourceUid, string damageType)
        {
            // Check for null or non-positive damage first
            if (targetEntity == null || damage <= 0) return; // Corrected <=

            _logger.LogDebug("[GameService] Applying {Damage} damage to Entity {TargetUid} (Owned by {TargetPlayerId}) from Source {SourceUid} (Player {SourcePlayerId}). Type: {DamageType}. Current Health: {CurrentHealth}",
                damage, targetEntity.Uid, targetPlayerId, sourceUid ?? "Unknown", sourcePlayerId, damageType, targetEntity.Health);

            int originalHealth = targetEntity.Health;
            targetEntity.Health -= damage;
            int newHealth = targetEntity.Health;
            int changeAmount = newHealth - originalHealth;

            // Generate Damage Dealt event using the new constructor
            // events.Add(new DamageDealtEventDto(sourcePlayerId, targetEntity.Uid, damage, targetPlayerId, sourceUid, damageType)); // Roo Test: Isolate SpellPlayed - Commented out
            _logger.LogWarning("[Roo Test] DamageDealtEvent generation SKIPPED for target {TargetUid}.", targetEntity.Uid); // Roo Test: Add log

            // Generate Health Changed event
            // Corrected to use the new constructor, passing targetPlayerId as the owner of the entity whose health changed
            // events.Add(new HealthChangedEventDto(targetPlayerId, targetEntity.Uid, newHealth, changeAmount)); // Roo Test: Isolate SpellPlayed - Commented out
            _logger.LogWarning("[Roo Test] HealthChangedEvent generation SKIPPED for target {TargetUid}.", targetEntity.Uid); // Roo Test: Add log

            _logger.LogDebug("[GameService] Entity {TargetUid} health changed to {NewHealth}. Change: {ChangeAmount}. Events added.",
               targetEntity.Uid, newHealth, changeAmount);
        }

        private void CheckAndHandleDeath(IBoardEntityState entity, PlayerStateDto owner, PlayerStateDto opponent, GameStateDto gameState, List<GameEventDto> events)
        {
            if (entity == null)
            {
                _logger.LogWarning("[GameService:{GameId}] CheckAndHandleDeath called with null entity.", gameState.GameId);
                return;
            }

            bool isOnField = true;
            if (entity is UnitStateDto unitCheck)
            {
                isOnField = owner.Field.Any(u => u.Uid == unitCheck.Uid);
            }

            if (!isOnField || entity.Health > 0) return; // Not dead or already removed

            _logger.LogInformation("[GameService:{GameId}] Entity {EntityUid} has zero or less health ({Health}) and is present. Processing death.",
                gameState.GameId, entity.Uid, entity.Health);

            if (entity is UnitStateDto unit)
            {
                var unitToRemove = owner.Field.FirstOrDefault(u => u.Uid == unit.Uid);
                if (unitToRemove != null)
                {
                    bool removed = owner.Field.Remove(unitToRemove);
                    if (removed)
                    {
                        _logger.LogInformation("[GameService:{GameId}] Unit {UnitName} ({UnitUid}) removed from Player {OwnerId}'s field.",
                           gameState.GameId, unit.Name, unit.Uid, owner.PlayerId);
                        // events.Add(new UnitDestroyedEventDto(opponent.PlayerId, unit.Uid.ToString(), owner.PlayerId)); // Roo Test: Isolate SpellPlayed - Commented out
                        _logger.LogWarning("[Roo Test:{GameId}] UnitDestroyedEvent generation SKIPPED for {UnitUid}.", gameState.GameId, unit.Uid); // Roo Test: Add log
                                                                                                                                                    // TODO: Graveyard logic
                    }
                    else
                    {
                        _logger.LogWarning("[GameService:{GameId}] Failed to remove unit {UnitUid} from owner {OwnerId}'s field list, though it was found and health <= 0.", gameState.GameId, unit.Uid, owner.PlayerId); // Corrected <=
                    }
                }
                else
                {
                    _logger.LogWarning("[GameService:{GameId}] Dead unit {UnitUid} checked again but not found on owner {OwnerId}'s field list.", gameState.GameId, unit.Uid, owner.PlayerId);
                }
            }
            else if (entity is PlayerStateDto player) // Player (Leader) died
            {
                // Note: Checking player.Health access later if issues persist
                _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} has been defeated.", gameState.GameId, player.PlayerId); // Temporarily removed health log

                if (!gameState.IsGameOver)
                {
                    gameState.IsGameOver = true;
                    gameState.WinnerId = opponent.PlayerId;
                    // Note: Checking gameState.Status access later if issues persist
                    // gameState.Status = GameStatus.Finished;
                    gameState.GameMessage = $"Game Over! Winner: {opponent.PlayerId}";
                    _logger.LogInformation("[GameService:{GameId}] Game Over! Winner declared: {WinnerId}", gameState.GameId, opponent.PlayerId);

                    // Corrected to use constructor, passing necessary IDs from context
                    // events.Add(new GameOverEventDto(owner.PlayerId, opponent.PlayerId, owner.PlayerId, null)); // Roo Test: Isolate SpellPlayed - Commented out
                    _logger.LogWarning("[Roo Test:{GameId}] GameOverEvent generation SKIPPED for Player {PlayerId}.", gameState.GameId, owner.PlayerId); // Roo Test: Add log
                }
                else
                {
                    _logger.LogWarning("[GameService:{GameId}] Player {PlayerId} defeated, but game was already marked as over.", gameState.GameId, player.PlayerId);
                }
            }
            else
            {
                _logger.LogError("[GameService:{GameId}] CheckAndHandleDeath called with an unknown entity type implementing IBoardEntityState. UID: {EntityUid}", gameState.GameId, entity.Uid);
            }
        } // End of CheckAndHandleDeath
          // --- Helper Methods for HandlePlayCardAsync ---

        // Fetches CardDefinition and CardDto from hand asynchronously before main lock
        private async Task<(CardDefinition? CardDefinition, CardDto? CardInHand)> FetchCardDataAsync(string gameId, string playerId, string cardUid)
        {
            _logger.LogWarning("--- FetchCardDataAsync ENTERED for Game {GameId}, Player {PlayerId}, Card {CardUid} ---", gameId, playerId, cardUid); // TRACE 1
            _logger.LogDebug("[GameService:{GameId}:FetchCardData] Starting fetch for CardUID {CardUID}.", gameId, cardUid);
            if (string.IsNullOrEmpty(cardUid))
                throw new GameLogicException("Card UID cannot be empty for fetching.");

            string cardIdToFetch;
            CardDto? cardInHand;
            object gameLock = _gameLocks.GetOrAdd(gameId, _ => new object());

            // Short lock just to get cardId from hand state
            lock (gameLock)
            {
                _logger.LogWarning("--- FetchCardDataAsync GOT LOCK for Game {GameId}, Player {PlayerId} ---", gameId, playerId); // TRACE 2
                if (!_gameStates.TryGetValue(gameId, out var tempGameState))
                    throw new GameLogicException($"Game not found: {gameId} (during card fetch)");
                var tempPlayerState = tempGameState.Players.FirstOrDefault(p => p.PlayerId == playerId);
                if (tempPlayerState == null)
                    throw new GameLogicException($"Player state error in game {gameId} (during card fetch).");

                // Parse input string cardUid to Guid for comparison
                // --- ADD DETAILED LOGGING BEFORE FINDING CARD ---
                var currentHandUidsForLog = tempPlayerState.Hand.Select(c => c.Uid.ToString()).ToList();
                _logger.LogWarning("[GameService:{GameId}:FetchCardData:DEBUG] Checking hand for Player {PlayerId}. Server believes hand contains UIDs: [{HandUidsString}]. Looking for UID: {CardUidToFind}",
                    gameId, playerId, string.Join(", ", currentHandUidsForLog), cardUid);
                // --- END ADDED LOGGING ---
                cardInHand = tempPlayerState.Hand.FirstOrDefault(c => Guid.TryParse(cardUid, out Guid uidToCompare) && c.Uid == uidToCompare);
                // TRACE 3 - Log hand state simply before lookup
                var handUids = tempPlayerState.Hand?.Select(c => c.Uid.ToString()).ToList() ?? new List<string>();
                _logger.LogWarning("--- FetchCardDataAsync CHECKING HAND for Player {PlayerId}. Hand UIDs: [{HandUidsString}]. Seeking: {CardUid}",
                    playerId, string.Join(",", handUids), cardUid);
                if (cardInHand == null)
                    throw new GameLogicException($"Card with UID {cardUid} not found in player {playerId}'s hand (during card fetch).");
                cardIdToFetch = cardInHand.CardId;
            } // Release short lock

            _logger.LogDebug("[GameService:{GameId}:FetchCardData] Need definition for CardId {CardIdToFetch}.", gameId, cardIdToFetch);
            // Fetch definition asynchronously outside the lock
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var cardRepository = scope.ServiceProvider.GetRequiredService<ICardRepository>();
                _logger.LogDebug("[GameService:{GameId}:FetchCardData] Resolved ICardRepository.", gameId);
                var cardDefinition = await cardRepository.GetCardByIdAsync(cardIdToFetch);
                if (cardDefinition == null)
                    throw new GameLogicException($"Card definition not found for CardId: {cardIdToFetch}");
                _logger.LogDebug("[GameService:{GameId}:FetchCardData] Fetched definition for {CardName} ({CardId}).", gameId, cardDefinition.Name, cardDefinition.Id);
                return (cardDefinition, cardInHand); // Return both pieces of data
            }
        }

        // Validates preconditions for playing a card inside the main lock
        private void ValidatePlayCardPreconditions(GameStateDto gameState, PlayerStateDto playerState, CardDefinition cardDefinition, CardDto cardInHand, PlayCardActionDto actionDto, string playerId)
        {
            _logger.LogDebug("[GameService:{GameId}:ValidatePlayCard] Validating preconditions for Player {PlayerId}, Card {CardName}.", gameState.GameId, playerId, cardDefinition.Name);
            if (gameState.IsGameOver) throw new GameLogicException("Game is already over.");
            if (gameState.CurrentPlayerId != playerId) throw new GameLogicException($"It's not player {playerId}'s turn.");
            if (gameState.Phase != GamePhase.Main) throw new GameLogicException($"Can only play cards during the Main phase (Current: {gameState.Phase}).");

            // Re-validate card still in hand inside lock
            if (!playerState.Hand.Any(c => c.Uid == cardInHand.Uid))
                throw new GameLogicException($"Card {cardInHand.Uid} disappeared from hand unexpectedly (inside lock validation).");

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
        private void ApplyPlayCardCostAndRemoveCard(GameActionContext context, string gameId, PlayerStateDto playerState, CardDto cardInHand, CardDefinition cardDefinition)
        {
            _logger.LogDebug("[GameService:{GameId}:ApplyCost] Applying cost and removing card {CardUID} ({CardName}).", gameId, cardInHand.Uid, cardDefinition.Name); // Use gameId param
            playerState.CurrentPP -= cardDefinition.Cost;
            // M8 Change: Use context to add event
            context.AddEvent(new PpUpdateEventDto(playerState.PlayerId, playerState.CurrentPP, playerState.MaxPP));

            bool removed = playerState.Hand.Remove(cardInHand);
            if (!removed)
            {
                _logger.LogError("[GameService:{GameId}:ApplyCost] CRITICAL: Failed to remove card {CardUID} from hand after validation!", gameId, cardInHand.Uid); // Use gameId param
                throw new GameLogicException("Internal error removing card from hand.");
            }
            playerState.PlayedCardCountThisTurn++;

            // Generate HandUpdateEvent (Card removed from hand)
            // Convert Guid cardInHand.Uid to string? for DTO constructor
            // M8 Change: Use context to add event
            context.AddEvent(new HandUpdateEventDto(playerState.PlayerId, playerState.Hand.Count, discardedCardUid: cardInHand.Uid.ToString()));
            _logger.LogDebug("[GameService:{GameId}:ApplyCost] Cost applied. Player {PlayerId} PP: {CurrentPP}/{MaxPP}. Hand size: {HandCount}. Played count: {PlayedCount}. HandUpdateEvent added.",
                gameId, playerState.PlayerId, playerState.CurrentPP, playerState.MaxPP, playerState.Hand.Count, playerState.PlayedCardCountThisTurn); // Use gameId param
        }
        // Creates a new UnitStateDto for a follower
        private UnitStateDto CreateUnitState(CardDefinition cardDefinition)
        {
            _logger.LogDebug("[GameService:CreateUnitState] Creating UnitState for {CardName} ({CardId}).", cardDefinition.Name, cardDefinition.Id);
            return new UnitStateDto
            {
                Uid = Guid.NewGuid(), // Unique instance ID on the field
                CardId = cardDefinition.Id,
                Name = cardDefinition.Name,
                Type = cardDefinition.Type, // Store type string
                CurrentAttack = cardDefinition.Attack,
                MaxAttack = cardDefinition.Attack,
                CurrentHealth = cardDefinition.Health,
                MaxHealth = cardDefinition.Health,
                // Correctly set initial CanAttack based on Rush/Storm keywords
                CanAttack = cardDefinition.Keywords?.Contains("Rush", StringComparer.OrdinalIgnoreCase) == true ||
                            cardDefinition.Keywords?.Contains("Storm", StringComparer.OrdinalIgnoreCase) == true, // True if Rush or Storm, else False
                IsEvolved = false,
                AttacksMadeThisTurn = 0,
                Keywords = cardDefinition.Keywords ?? new List<string>(),
                // Add missing Cost property initialization
                Cost = cardDefinition.Cost
            };
        }

        // Executes the logic for playing a Follower inside the main lock (BoardIndex removed)
        // M8 Change: Signature updated to use GameActionContext
        private void ExecuteFollowerPlay(GameActionContext context, GameStateDto gameState, PlayerStateDto playerState, UnitStateDto unitState, CardDto cardFromHand, PlayCardActionDto actionDto)
        {
            _logger.LogDebug("[GameService:{GameId}:ExecuteFollower] Executing play for {UnitName} ({UnitUid}).", gameState.GameId, unitState.Name, unitState.Uid);
            // V0.1 Simplified: Always add follower to the end of the field. BoardIndex logic removed.
            playerState.Field.Add(unitState);
            _logger.LogDebug("[GameService:{GameId}:ExecuteFollower] Added {UnitName} to end of field.", gameState.GameId, unitState.Name);

            // Convert Guid cardFromHand.Uid to string for DTO constructor
            // M8 Change: Use context to add event
            context.AddEvent(new UnitPlayedEventDto(playerState.PlayerId, playerState.PlayerId, unitState, cardFromHand.Uid.ToString())); // Restore event generation
                                                                                                                                          // _logger.LogWarning("[Roo Test:{GameId}] UnitPlayedEvent generation SKIPPED for {UnitName} ({UnitUid}).", gameState.GameId, unitState.Name, unitState.Uid); // Roo Test: Remove log
            _logger.LogInformation("[GameService:{GameId}] Follower {UnitName} ({UnitUid}) played onto field for Player {PlayerId}. UnitPlayedEvent added.", // Update log
                gameState.GameId, unitState.Name, unitState.Uid, playerState.PlayerId);
            // TODO: Fanfare (Future)
        }

        // Executes the logic for playing a Spell inside the main lock - Uses playerState.Graveyard
        // M8 Change: Signature updated to use GameActionContext
        private void ExecuteSpellPlay(GameActionContext context, GameStateDto gameState, PlayerStateDto playerState, CardDto cardFromHand, CardDefinition cardDefinition, PlayCardActionDto actionDto)
        {
            _logger.LogDebug("[GameService:{GameId}:ExecuteSpell] Executing play for {CardName} ({CardUID}).", gameState.GameId, cardFromHand.Name, cardFromHand.Uid);
            // Assuming constructor takes string UIDs based on other events (Potential type issue flagged by compiler, needs DTO check if persists)
            // Corrected: Use cardDefinition.Id (int) for 2nd arg, cardFromHand.Uid (string) for 3rd
            // Convert Guid cardFromHand.Uid to string for DTO constructor
            // M8 Change: Use context to add event
            context.AddEvent(new SpellPlayedEventDto(playerState.PlayerId, cardDefinition.Id, cardFromHand.Uid.ToString(), actionDto.TargetUid)); // Pass TargetUid

            // --- Target Finding and Validation ---
            IBoardEntityState? targetEntity = null;
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
            if (playerState.Graveyard == null) playerState.Graveyard = new List<CardDto>(); // Initialize if null
            playerState.Graveyard.Add(cardFromHand); // Add the actual CardDto instance
            _logger.LogDebug("[GameService:{GameId}:ExecuteSpell] Added card {CardUID} to player {PlayerId}'s graveyard. Size: {GraveyardSize}", gameState.GameId, cardFromHand.Uid, playerState.PlayerId, playerState.Graveyard.Count);

            // Generate GraveyardUpdateEvent (Card added to graveyard)
            // M8 Change: Use context to add event
            context.AddEvent(new GraveyardUpdateEventDto(playerState.PlayerId, playerState.Graveyard.Count, cardAdded: cardFromHand)); // Restore event generation
                                                                                                                                       // _logger.LogWarning("[Roo Test:{GameId}] GraveyardUpdateEvent generation SKIPPED for {CardName} in ExecuteSpellPlay.", gameState.GameId, cardFromHand.Name); // Roo Test: Remove log
                                                                                                                                       // Corrected truncated log message - Restore log message below
            _logger.LogDebug("[GameService:{GameId}:ExecuteSpellPlay] Added spell {CardUID} to graveyard. GraveyardUpdateEvent added.", gameState.GameId, cardFromHand.Uid); // Restore log message
        } // Closing brace for ExecuteSpellPlay



        // Executes the logic for playing an Amulet (Simplified for V0.1)
        // M8 Change: Signature updated to use GameActionContext
        private void ExecuteAmuletPlay(GameActionContext context, GameStateDto gameState, PlayerStateDto playerState, CardDto cardFromHand, PlayCardActionDto actionDto)
        {
            // Simplified V0.1: Log and maybe add placeholder event. No state change yet.
            _logger.LogInformation("[GameService:{GameId}] Amulet {CardName} ({CardUID}) played by Player {PlayerId}. (Logic Deferred in V0.1)",
                 gameState.GameId, cardFromHand.Name, cardFromHand.Uid, playerState.PlayerId);
            // Assume AmuletPlayedEventDto exists: AmuletPlayedEventDto(sourcePlayerId, ownerPlayerId, amuletStateDto/cardDto?, cardUidFromHand)
            // For now, skip AmuletPlayedEvent if DTO is unclear.
            _logger.LogWarning("[GameService:{GameId}] AmuletPlayedEvent creation skipped - DTO structure needs confirmation.", gameState.GameId);

            // Add Amulet to graveyard (Consistent with Spell V0.1)
            if (playerState.Graveyard == null) playerState.Graveyard = new List<CardDto>();
            playerState.Graveyard.Add(cardFromHand);
            // M8 Change: Use context to add event
            context.AddEvent(new GraveyardUpdateEventDto(playerState.PlayerId, playerState.Graveyard.Count, cardAdded: cardFromHand)); // Restore event generation
                                                                                                                                       // _logger.LogWarning("[Roo Test:{GameId}] GraveyardUpdateEvent generation SKIPPED for {CardName} in ExecuteAmuletPlay.", gameState.GameId, cardFromHand.Name); // Roo Test: Remove log
            _logger.LogDebug("[GameService:{GameId}:ExecuteAmuletPlay] Added amulet {CardUID} to player {PlayerId}'s graveyard. Size: {GraveyardSize}. GraveyardUpdateEvent added.", gameState.GameId, cardFromHand.Uid, playerState.PlayerId, playerState.Graveyard.Count); // Keep this log


            // TODO: Add Amulet state handling, add to field, trigger effects (Future)
            // For V0.1, simply add to graveyard (done above)
            _logger.LogDebug("[GameService:{GameId}] Amulet {CardUID} processing complete for V0.1 (added to graveyard).", gameState.GameId, cardFromHand.Uid);
        } // Closing brace for ExecuteAmuletPlay

        /// &lt;summary&gt;
        /// Public method to explicitly trigger the execution of an AI player's turn.
        /// Called by the Hub. Wraps the internal execution logic.
        /// &lt;/summary&gt;
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
            await aiTurnExecutorInstance.ExecuteTurnAsync(gameId, aiPlayerId);
            _logger.LogInformation("[AI_TURN:{GameId}:{PlayerId}] AiTurnExecutor (resolved via scope) completed successfully.", gameId, aiPlayerId);

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
                    var (nextHumanStartTurnEvents, isNextTurnAi) = await StartPlayerTurnAsync(gameId, nextPlayer.PlayerId); // isNextTurnAi should be false here

                    // --- BUG-010 DIAGNOSTIC LOGGING START ---
                    _logger.LogWarning("[BUG-010_DIAG:{GameId}] StartPlayerTurnAsync call completed. Returned IsAI={IsAiTurn}, EventCount={EventCount}",
                        gameId, isNextTurnAi, nextHumanStartTurnEvents?.Count ?? 0);
                    // --- END BUG-010 DIAGNOSTIC LOGGING ---


                    if (nextHumanStartTurnEvents != null && nextHumanStartTurnEvents.Any())
                    {
                        _logger.LogInformation("[AI_TURN:{GameId}:{PlayerId}] Broadcasting {EventCount} events for start of human player {NextPlayerId}'s turn.", gameId, aiPlayerId, nextHumanStartTurnEvents.Count, nextPlayer.PlayerId);
                        await BroadcastEventsAsync(gameId, nextHumanStartTurnEvents);
                    }
                    else
                    {
                        _logger.LogInformation("[AI_TURN:{GameId}:{PlayerId}] No events generated during start of human player {NextPlayerId}'s turn.", gameId, aiPlayerId, nextPlayer.PlayerId);
                    }
                }
                else if (nextPlayer == null)
                {
                    _logger.LogWarning("[AI_TURN:{GameId}:{PlayerId}] Could not find opponent player after AI turn. Cannot start next turn.", gameId, aiPlayerId);
                }
                else // nextPlayer is AI
                {
                    _logger.LogInformation("[AI_TURN:{GameId}:{PlayerId}] Next player ({NextPlayerId}) is AI. Not starting their turn here (should be handled by their own trigger).", gameId, aiPlayerId, nextPlayer.PlayerId);
                }
            }
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
            try
            {
                // Using dictionary-based dispatch
                var eventType = gameEvent.GetType();
                if (_eventHandlers.TryGetValue(eventType, out var handler))
                {
                    await handler(groupName, gameEvent); // Invoke the registered delegate
                    _logger.LogDebug("[GameService:{GameId}] Successfully invoked handler for event type {EventType} via dictionary.", gameId, eventType.Name);
                }
                else
                {
                    _logger.LogWarning("[GameService:{GameId}] Unknown event type encountered in BroadcastEventAsync (Handler not found in dictionary): {EventType}", gameId, eventType.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Error broadcasting event {EventType} to group {GroupName}.", gameId, gameEvent.GetType().Name, groupName);
                // Do not re-throw here, as broadcasting failure shouldn't crash the main game logic flow.
            }
        } // <<< END BroadcastEventAsync method

    } // Closing brace for GameService class
} // Closing brace for ShadowverseBeyond.Server.Services namespace

