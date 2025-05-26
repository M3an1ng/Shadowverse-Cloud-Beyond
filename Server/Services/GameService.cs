// Server/Services/GameService.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Hubs; // GameHub
using ShadowverseBeyond.Server.Hubs.ClientInterfaces; // IGameClient
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
namespace ShadowverseBeyond.Server.Services
{
    public class GameService : IGameService
    {
        private readonly IHubContext<GameHub, IGameClient> _hubContext;
        private readonly ILogger<GameService> _logger;
        // Remove Scoped ICardRepository injection from Singleton GameService constructor
        // private readonly ICardRepository _cardRepository;
        private readonly IServiceScopeFactory _serviceScopeFactory; // Inject IServiceScopeFactory instead

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

        // Central Game State Storage
        private readonly ConcurrentDictionary<string, GameStateDto> _gameStates = new();
        // Update constructor signature
        public GameService(IHubContext<GameHub, IGameClient> hubContext, ILogger<GameService> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // Assign IServiceScopeFactory
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            // _cardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository)); // Removed assignment
        }

        // Updated PlayCardAsync implementation
        public async Task<List<GameEventDto>> PlayCardAsync(string gameId, string playerId, PlayCardActionDto actionDto)
        {
            var targetDisplay = actionDto.TargetUid?.ToString() ?? "None"; // Corrected to TargetUid and added null-conditional operator
            _logger.LogInformation("[GameService] Player {PlayerId} in Game {GameId} attempting PlayCard: CardUID={CardUID}, Target={Target}",
                playerId, gameId, actionDto.CardUid, targetDisplay); // Corrected to CardUid

            try
            {
                // --- Basic Validation ---
                if (string.IsNullOrEmpty(actionDto.CardUid)) // Corrected to CardUid
                {
                    throw new GameLogicException("Card UID cannot be empty.");
                }

                // TODO M4+: Implement actual game logic:
                // 1. Validate gameId and playerId exist.
                // 2. Load GameState associated with gameId.
                // 3. Validate Player's turn, PP cost, card in hand, target validity etc. based on GameState.
                // 4. If valid:
                //    a. Calculate effects.
                //    b. Update GameState (potentially creating new entities like followers on board).
                //    c. Determine resulting events (UnitPlayed, PpUpdate, DrawCard, DamageDealt etc.).
                //    d. Save updated GameState.
                //    e. Broadcast generated events via HubContext.

                // --- Placeholder Logic & Broadcasting ---
                _logger.LogInformation("[GameService] Placeholder: Simulating successful card play for {CardUID} by {PlayerId}.", actionDto.CardUid, playerId); // Corrected to CardUid

                // Simulate a unit being played event
                // Corrected constructor call based on actual UnitPlayedEventDto definition
                // Create UnitPlayedEventDto using object initializer
                // TODO M4+: This placeholder needs access to the actual card definition and game state to populate real data.
                var unitPlayedEvent = new ShadowverseBeyond.Server.Dtos.SignalR.Events.UnitPlayedEventDto
                {
                    PlayerId = playerId,
                    UnitData = new UnitStateDto // Initialize nested UnitStateDto
                    {
                        Uid = Guid.NewGuid(), // Assign unique Guid for this instance on field
                        CardId = "0", // Placeholder - Requires actual card ID from definition
                        Name = "Placeholder Unit", // Placeholder - Requires actual name
                        Type = CardType.Follower.ToString(), // Placeholder - Requires actual type
                        CurrentAttack = 0, // Placeholder - Requires actual stats
                        MaxAttack = 0, // Placeholder
                        CurrentHealth = 0, // Placeholder
                        MaxHealth = 0, // Placeholder
                        CanAttack = false, // Default
                        IsEvolved = false, // Default
                        AttackCountThisTurn = 0,
                        Keywords = new List<string>() // Initialize empty
                    },
                    SourceCardUid = actionDto.CardUid // Corrected to CardUid
                };
                string groupName = $"Game_{gameId}";
                _logger.LogInformation("[GameService] Broadcasting UnitPlayed event to group {GroupName}", groupName);
                await _hubContext.Clients.Group(groupName).UnitPlayed(unitPlayedEvent);

                // Simulate a PP update event
                // Corrected constructor call using positional arguments and removing Timestamp
                // Create PpUpdateEventDto using object initializer
                // TODO M4+: This placeholder needs access to the actual game state to populate real data.
                var ppUpdateEvent = new ShadowverseBeyond.Server.Dtos.SignalR.Events.PpUpdateEventDto // Fully qualified name
                {
                    PlayerId = playerId,
                    CurrentPp = 5, // Placeholder - Requires actual CurrentPP from GameState
                    MaxPp = 7      // Placeholder - Requires actual MaxPP from GameState
                };
                _logger.LogInformation("[GameService] Broadcasting PpUpdate event to group {GroupName}", groupName);
                await _hubContext.Clients.Group(groupName).PpUpdate(ppUpdateEvent);

                _logger.LogInformation("[GameService] Placeholder card play logic completed for Player {PlayerId} in Game {GameId}.", playerId, gameId);

                // Placeholder return to satisfy interface
                return await Task.FromResult(new List<GameEventDto> { unitPlayedEvent, ppUpdateEvent }); // Return the generated placeholder events
            }
            catch (GameLogicException ex)
            {
                // Logged in the Hub already, re-throw for Hub to catch and notify caller.
                _logger.LogWarning("[GameService] Game Logic Error during PlayCard for Player {PlayerId} in Game {GameId}: {Message}", playerId, gameId, ex.Message);
                // return await Task.FromResult(new List<GameEventDto>()); // Option: return empty list on error
                throw; // Re-throw GameLogicException for the Hub to handle
            }
            catch (Exception ex)
            {
                // Log unexpected errors here. The Hub will catch this and send a generic error.
                _logger.LogError(ex, "[GameService] Unexpected error during PlayCard for Player {PlayerId} in Game {GameId}", playerId, gameId);
                // return await Task.FromResult(new List<GameEventDto>()); // Option: return empty list on error
                throw new GameLogicException("An unexpected error occurred in the game service.", ex); // Wrap in GameLogicException
            }
        }

        // Implement EndTurnAsync
        // Implement EndTurnAsync (Refactored based on M7 Design Doc - Step 1: Refactor method signature and core calls)
        // Note: Helper methods ProcessEndOfTurn and ProcessStartOfTurn will be added in subsequent steps.
        public async Task<List<GameEventDto>> EndTurnAsync(string gameId, string playerId) // Renamed back to match IGameService interface
        {
            _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} attempting EndTurn.", gameId, playerId);
            var events = new List<GameEventDto>();

            try
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
                if (gameState.CurrentPlayerId != playerId)
                {
                    throw new GameLogicException($"It's not player {playerId}'s turn (Current: {gameState.CurrentPlayerId}).");
                }
                if (gameState.Phase == GamePhase.Mulligan) // Can't end turn during mulligan
                {
                    throw new GameLogicException("Cannot end turn during Mulligan phase.");
                }


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

                // 4. Call ProcessEndOfTurn for the current player (Helper method to be added later)
                events.AddRange(ProcessEndOfTurn(currentPlayerState, gameState)); // Call commented out until helper exists
                _logger.LogInformation("[GameService:{GameId}] Placeholder: Call to ProcessEndOfTurn for {PlayerId}.", gameId, playerId);


                // 5. Determine if it's the second player's very first turn
                bool isSecondPlayerFirstTurn = gameState.TurnNumber == 0 && !opponentPlayerState.IsFirst;
                _logger.LogDebug("[GameService:{GameId}] Checking if next turn is second player's first: TurnNumber={TurnBeforeStart}, OpponentIsFirst={OpponentIsFirst}, Result={IsSecondFirst}",
                    gameId, gameState.TurnNumber, opponentPlayerState.IsFirst, isSecondPlayerFirstTurn);


                // 6. Call ProcessStartOfTurn for the opponent (Helper method to be added later)
                var startTurnEvents = ProcessStartOfTurn(opponentPlayerState, gameState, isSecondPlayerFirstTurn);
                events.AddRange(startTurnEvents);
                _logger.LogInformation("[GameService:{GameId}] Placeholder: Call to ProcessStartOfTurn for next player {OpponentId}.", gameId, opponentPlayerState.PlayerId);


                // 7. Return combined event list (will be empty until helpers are implemented and called)
                _logger.LogInformation("[GameService:{GameId}] EndTurn processing structure updated for {PlayerId}. Returning placeholder event list.", gameId, playerId);
                // Return empty list for now, as helpers are not yet implemented/called.
                return await Task.FromResult(events);
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("[GameService:{GameId}] Game Logic Error during EndTurn for Player {PlayerId}: {Message}", gameId, playerId, ex.Message);
                throw; // Re-throw GameLogicException for the Hub to handle
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService:{GameId}] Unexpected error during EndTurn for Player {PlayerId}", gameId, playerId);
                throw new GameLogicException("An unexpected error occurred in the game service during end turn.", ex); // Wrap in GameLogicException
            }
        } // This closes HandleEndTurnAsync

        // Implement SubmitMulliganAsync from IGameService
        // Removed duplicate signature inserted by previous diff
        public async Task SubmitMulliganAsync(string gameId, string playerId, List<string> cardUidsToReplace)
        {
            _logger.LogInformation("[GameService] Player {PlayerId} in Game {GameId} attempting SubmitMulligan with {CardCount} cards.",
                playerId, gameId, cardUidsToReplace?.Count ?? 0);
            _logger.LogDebug("[GameService:SubmitMulliganAsync] Entered method for Game {GameId}, Player {PlayerId}.", gameId, playerId);

            try
            {
                // --- Game State Retrieval & Validation ---
                if (!_gameStates.TryGetValue(gameId, out var gameState))
                {
                    throw new GameLogicException($"Game not found: {gameId}");
                }

                if (gameState.Phase != GamePhase.Mulligan)
                {
                    throw new GameLogicException($"Invalid game phase for mulligan: {gameState.Phase}");
                }

                var playerState = gameState.Players.FirstOrDefault(p => p.PlayerId == playerId);
                if (playerState == null)
                {
                    throw new GameLogicException($"Player not found in game: {playerId}");
                }

                if (playerState.HasCompletedMulligan)
                {
                    throw new GameLogicException($"Player {playerId} has already completed mulligan.");
                }

                // --- Card Replacement Logic ---
                _logger.LogInformation("[GameService:{GameId}] Processing mulligan for {PlayerId}. Replacing {Count} cards.", gameId, playerId, cardUidsToReplace?.Count ?? 0);

                if (cardUidsToReplace != null && cardUidsToReplace.Any())
                {
                    // --- Actual Card Replacement Logic ---

                    // 1. Validate UIDs are in hand
                    var currentHandUids = playerState.Hand.Select(c => c.Uid).ToHashSet();
                    var invalidUids = cardUidsToReplace.Where(uid => !currentHandUids.Contains(uid)).ToList();
                    if (invalidUids.Any())
                    {
                        throw new GameLogicException($"Invalid card UIDs submitted for mulligan: {string.Join(", ", invalidUids)}");
                    }

                    // 2. Separate cards
                    var cardsToReplace = playerState.Hand.Where(c => cardUidsToReplace.Contains(c.Uid)).ToList();
                    var cardsToKeep = playerState.Hand.Where(c => !cardUidsToReplace.Contains(c.Uid)).ToList();
                    _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} keeping {KeepCount}, replacing {ReplaceCount} cards.", gameId, playerId, cardsToKeep.Count, cardsToReplace.Count);

                    // 3. Return cards to deck
                    playerState.Deck.AddRange(cardsToReplace);
                    _logger.LogDebug("[GameService:{GameId}] Returned {ReplaceCount} cards to deck. Deck size now {DeckSize}.", gameId, cardsToReplace.Count, playerState.Deck.Count);


                    // 4. Update hand to kept cards
                    playerState.Hand = cardsToKeep;


                    // 5. Shuffle deck
                    ShuffleDeck(playerState.Deck);
                    _logger.LogDebug("[GameService:{GameId}] Deck shuffled.", gameId);


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


                    // 7. Add newly drawn cards to hand
                    playerState.Hand.AddRange(drawnCards);
                    _logger.LogDebug("[GameService:{GameId}] Added drawn cards to hand. New hand size: {HandSize}", gameId, playerState.Hand.Count);


                    // 8. Update deck count
                    playerState.DeckCount = playerState.Deck.Count;
                    _logger.LogDebug("[GameService:{GameId}] Final deck count: {DeckCount}", gameId, playerState.DeckCount);

                    // TODO: Broadcast hand update event specifically to this player
                    _logger.LogDebug("[GameService:{GameId}] TODO: Broadcast HandUpdate event for Player {PlayerId}", gameId, playerId);

                }
                else
                {
                    _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} chose to replace 0 cards.", gameId, playerId);
                    // No card replacement needed if list is null or empty
                }

                // --- Mark Mulligan Complete ---
                playerState.HasCompletedMulligan = true;
                _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} marked as completed mulligan.", gameId, playerId);

                // --- Check Opponent and Trigger AI Mulligan if needed ---
                var opponentState = gameState.Players.FirstOrDefault(p => p.PlayerId != playerId);
                if (opponentState == null)
                {
                    // This shouldn't happen in a 2-player game after validation
                    throw new GameLogicException($"Opponent not found for player {playerId} in game {gameId}.");
                }
                // REMOVED PREMATURE CLOSING BRACE FROM Line 244

                // ---> START AI MULLIGAN SIMULATION <---
                if (opponentState.IsAI && !opponentState.HasCompletedMulligan)
                {
                    _logger.LogInformation("[GameService:{GameId}] Opponent is AI ({OpponentId}). Simulating AI mulligan.", gameId, opponentState.PlayerId);
                    // Simulate AI replacing 0 cards for simplicity
                    opponentState.HasCompletedMulligan = true; // Mark AI as done
                    _logger.LogInformation("[GameService:{GameId}] AI Player {OpponentId} marked as completed mulligan (simulated, replaced 0 cards).", gameId, opponentState.PlayerId);
                    _logger.LogWarning(">>>>>>>>>> [DEBUG] AI Mulligan Simulation block EXECUTED for Opponent {OpponentId}. HasCompletedMulligan should now be true.", opponentState.PlayerId); // <-- ADDED WARNING LOG
                    // TODO: Implement actual AI mulligan decision logic later
                }
                // ---> END AI MULLIGAN SIMULATION <---

                // --- Check if Both Players Completed Mulligan (Now includes AI auto-completion) ---
                if (playerState.HasCompletedMulligan && opponentState.HasCompletedMulligan)
                {
                    _logger.LogWarning(">>>>>>>>>> [DEBUG] Both players completed mulligan block ENTERED. P1: {P1Done}, P2: {P2Done}. Starting game.", playerState.HasCompletedMulligan, opponentState.HasCompletedMulligan); // <-- ADDED WARNING LOG
                    _logger.LogInformation("[GameService:{GameId}] Both players have completed mulligan. Starting game.", gameId);
                    // --- Transition to First Turn ---
                    gameState.Phase = GamePhase.TurnStart; // Or Main? TurnStart seems better for drawing etc.
                    gameState.TurnNumber = 1;
                    gameState.CurrentPlayerId = gameState.FirstPlayerId; // Already determined in GetOrCreate
                    gameState.GameMessage = $"Turn 1 - {gameState.CurrentPlayerId}'s turn.";

                    // TODO: Implement Start Turn Logic (PP, Draw etc.)
                    // Maybe refactor this into a StartTurn(gameState, startingPlayerId) helper method?

                    var startingPlayerState = gameState.Players.First(p => p.PlayerId == gameState.CurrentPlayerId);
                    var secondPlayerState = gameState.Players.First(p => p.PlayerId != gameState.CurrentPlayerId);

                    // Set Initial PP
                    startingPlayerState.MaxPP = 1;
                    startingPlayerState.CurrentPP = 1;
                    secondPlayerState.MaxPP = 1; // Both players get 1 max PP on turn 1
                    secondPlayerState.CurrentPP = 0; // Opponent starts with 0 current PP

                    // Draw Cards
                    // TODO: Create DrawCard(playerState, count) helper
                    _logger.LogWarning("[GameService:{GameId}] Placeholder: Draw card logic not yet implemented.", gameId);
                    // Draw for starting player
                    // DrawCard(startingPlayerState, 1);
                    // Draw extra card for second player at game start
                    // DrawCard(secondPlayerState, 1);


                    _logger.LogInformation("[GameService:{GameId}] Broadcasting updated game state after mulligan completion.", gameId);
                    string groupName = $"Game_{gameId}";
                    // Broadcast the entire state to sync both clients
                    await _hubContext.Clients.Group(groupName).ReceiveGameState(gameState);
                }
                else
                {
                    _logger.LogInformation("[GameService:{GameId}] Player {PlayerId} completed mulligan. Waiting for opponent.", gameId, playerId);
                    // Optionally: Send a specific event indicating one player is done?
                    // await _hubContext.Clients.Client(opponentConnectionId).PlayerMulliganComplete(playerId);
                }

                // --- Update Game State in Memory ---
                // The gameState object is modified directly, and since it's a reference type
                // stored in the ConcurrentDictionary, the dictionary holds the updated state.
                // No explicit _gameStates[gameId] = gameState needed unless replacing the object instance.

                await Task.CompletedTask; // Placeholder if no other async calls needed yet
            }
            catch (GameLogicException ex)
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
                            Uid = Guid.NewGuid().ToString(),
                            CardId = cardDef.Id,
                            Name = cardDef.Name,
                            Cost = cardDef.Cost,
                            Type = cardDef.Type,
                            BaseAttack = cardDef.Attack,
                            BaseHealth = cardDef.Health,
                            Keywords = cardDef.Keywords ?? new List<string>()
                        });

                        player2Deck.Add(new CardDto
                        {
                            Uid = Guid.NewGuid().ToString(),
                            CardId = cardDef.Id,
                            Name = cardDef.Name,
                            Cost = cardDef.Cost,
                            Type = cardDef.Type,
                            BaseAttack = cardDef.Attack,
                            BaseHealth = cardDef.Health,
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
                                                 // Graveyard = new List<CardDto>() // Initialize if needed
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
                    PlayedCardCountThisTurn = 0 // Reset counter
                                                // Graveyard = new List<CardDto>()
                };

                // Player decks are now stored directly in PlayerStateDto.Deck

                var newGameState = new GameStateDto
                {
                    GameId = gameId,
                    CurrentPlayerId = firstPlayerId, // First player takes the first turn
                    FirstPlayerId = firstPlayerId,
                    TurnNumber = 0, // Turn 0 for setup, increments to 1 on first StartTurn
                    Phase = GamePhase.Mulligan, // Start in Mulligan phase (using Enum)
                    Players = new List<PlayerStateDto> { player1State, player2State },
                    IsGameOver = false,
                    WinnerId = null,
                    GameStatus = GameStatus.InProgress, // Use enum
                    GameMessage = "Game created. Waiting for players." // Initial message
                };

                // _logger.LogInformation("[GameService:{GameId}] >>> Attempting to add newly created game state to dictionary...", gameId); // REMOVED DIAGNOSTIC LOG
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
                attacker.CanAttack = false; // Mark attacker as having attacked

                // Phase 1: Attack Timing Event
                // Add null-forgiving operator (!)
                if (targetEntity is UnitStateDto targetUnitP1)
                {
                    events.Add(new UnitAttackedEventDto { AttackerUid = attacker.Uid.ToString(), TargetUid = targetUnitP1.Uid.ToString() });
                    _logger.LogDebug("[GameService:{GameId}] Generated UnitAttackedEvent ({AttackerUid} -> {TargetUid})", gameId, attacker.Uid, targetUnitP1.Uid);
                }
                // Add null-forgiving operator (!)
                else if (targetEntity is PlayerStateDto targetPlayerP1)
                {
                    events.Add(new PlayerAttackedEventDto { AttackerUid = attacker.Uid.ToString(), TargetPlayerId = targetPlayerP1.PlayerId });
                    _logger.LogDebug("[GameService:{GameId}] Generated PlayerAttackedEvent ({AttackerUid} -> {TargetPlayerId})", gameId, attacker.Uid, targetPlayerP1.PlayerId);
                }

                // Phase 2: Clash Timing (Placeholder)
                // TODO: Implement Clash effects if target is Unit

                // Phase 3: Damage Timing
                _logger.LogDebug("[GameService:{GameId}] Entering Damage Phase.", gameId);
                int attackerAttack = attacker.CurrentAttack ?? 0;
                // Add null-forgiving operator (!) to satisfy non-nullable parameter type
                ApplyDamage(targetEntity, attackerAttack, events); // CALL HELPER

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
                        ApplyDamage((IBoardEntityState)attacker, defenderAttack, events); // CALL HELPER
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
                return events;
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
                if (gameState.TurnNumber < requiredTurn)
                {
                    throw new GameLogicException($"Cannot evolve yet. Player {(playerState.IsFirst ? 1 : 2)} can evolve starting turn {requiredTurn} (Current turn: {gameState.TurnNumber}).");
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
                var epEvent = new EpUpdateEventDto { PlayerId = playerId, CurrentEp = playerState.CurrentEP, MaxEp = playerState.MaxEP };
                events.Add(epEvent);
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
                    AttackCountThisTurn = 0, // Base reset
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
                var statEvent = new StatChangeEventDto
                {
                    TargetEntityUid = evolvedUnitState.Uid.ToString(), // Required property, corrected name
                    NewAttack = evolvedUnitState.CurrentAttack,        // Correct property name
                    NewCurrentHealth = evolvedUnitState.CurrentHealth, // Correct property name
                    NewMaxHealth = evolvedUnitState.MaxHealth          // Correct property name
                };
                events.Add(statEvent);

                // Corrected UnitEvolvedEventDto initialization based on actual definition
                var evolvedEvent = new UnitEvolvedEventDto
                {
                    EvolvingUnitUid = evolvedUnitState.Uid.ToString(), // Required property, corrected name
                    EvolvedUnitState = evolvedUnitState                // Required property, pass the whole object
                };
                events.Add(evolvedEvent);
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
        /// Draws a specified number of cards from the player's deck.
        /// Handles empty deck scenario. Updates player's DeckCount and Hand.
        /// </summary>
        /// <param name="playerState">The player drawing the cards.</param>
        /// <param name="drawCount">The number of cards to draw.</param>
        /// <returns>A list of cards drawn.</returns>
        private List<CardDto> DrawCards(PlayerStateDto playerState, int drawCount)
        {
            // Assuming GameId is accessible via playerState or context if needed for detailed logging
            _logger.LogDebug("[GameService] Player {PlayerId} attempting to draw {DrawCount} cards. Deck size: {DeckSize}",
                playerState.PlayerId, drawCount, playerState.Deck.Count);

            List<CardDto> drawnCards = new List<CardDto>();
            for (int i = 0; i < drawCount; i++)
            {
                if (playerState.Deck.Any())
                {
                    CardDto drawnCard = playerState.Deck[0];
                    drawnCards.Add(drawnCard);
                    playerState.Deck.RemoveAt(0);
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
        private List<GameEventDto> ProcessEndOfTurn(PlayerStateDto currentPlayerState, GameStateDto gameState)
        {
            _logger.LogInformation("[GameService:{GameId}] Processing EndOfTurn for Player {PlayerId}.", gameState.GameId, currentPlayerState.PlayerId);
            var events = new List<GameEventDto>();

            // Mark that the player has taken their first turn (relevant for draw 2 logic)
            if (!currentPlayerState.HasTakenFirstTurn)
            {
                currentPlayerState.HasTakenFirstTurn = true;
                _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} marked as having taken their first turn.", gameState.GameId, currentPlayerState.PlayerId);
            }

            // TODO M7+: Implement actual end-of-turn effects (e.g., card effects, resetting attack counts)

            _logger.LogDebug("[GameService:{GameId}] EndOfTurn processing complete for Player {PlayerId}.", gameState.GameId, currentPlayerState.PlayerId);
            return events;
        } // End of ProcessEndOfTurn

        /// <summary>
        /// Processes start-of-turn effects for the player whose turn is beginning.
        /// Handles PP update, card draw (including P2 first turn bonus), and other start-of-turn triggers.
        /// </summary>
        /// <param name="startingPlayerState">The state of the player whose turn is starting.</param>
        /// <param name="gameState">The current game state.</param>
        /// <param name="isSecondPlayerFirstTurn">Indicates if this is the second player's very first turn (for draw 2 rule).</param>
        /// <returns>A list of game events generated during start-of-turn processing.</returns>
        private List<GameEventDto> ProcessStartOfTurn(PlayerStateDto startingPlayerState, GameStateDto gameState, bool isSecondPlayerFirstTurn)
        {
            _logger.LogInformation("[GameService:{GameId}] Processing StartOfTurn for Player {PlayerId}. Is Second Player First Turn: {IsSecondFirst}",
                gameState.GameId, startingPlayerState.PlayerId, isSecondPlayerFirstTurn);
            var events = new List<GameEventDto>();

            // 1. Update PP
            if (startingPlayerState.MaxPP < 10)
            {
                startingPlayerState.MaxPP++;
            }
            startingPlayerState.CurrentPP = startingPlayerState.MaxPP; // Refill PP
            _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} PP updated: {CurrentPP}/{MaxPP}",
                gameState.GameId, startingPlayerState.PlayerId, startingPlayerState.CurrentPP, startingPlayerState.MaxPP);
            events.Add(new PpUpdateEventDto
            {
                PlayerId = startingPlayerState.PlayerId,
                CurrentPp = startingPlayerState.CurrentPP,
                MaxPp = startingPlayerState.MaxPP
            });

            // 2. Draw Card(s)
            int drawCount = isSecondPlayerFirstTurn ? 2 : 1;
            _logger.LogDebug("[GameService:{GameId}] Player {PlayerId} drawing {DrawCount} cards.", gameState.GameId, startingPlayerState.PlayerId, drawCount);
            List<CardDto> drawnCards = DrawCards(startingPlayerState, drawCount); // Use the existing helper

            // Generate Hand/Deck Update Events (DrawCards helper updates playerState.Hand and playerState.DeckCount)
            events.Add(new HandUpdateEventDto
            {
                PlayerId = startingPlayerState.PlayerId,
                HandCount = startingPlayerState.Hand.Count, // Use the hand count after drawing
                DrawnCards = drawnCards // Assign the list returned by DrawCards helper
            });
            events.Add(new DeckUpdateEventDto
            {
                PlayerId = startingPlayerState.PlayerId,
                DeckCount = startingPlayerState.DeckCount
            });


            // 3. Update Turn Number and Current Player in GameState
            gameState.TurnNumber++; // Increment turn number *after* processing start-of-turn for the player
            gameState.CurrentPlayerId = startingPlayerState.PlayerId;
            gameState.Phase = GamePhase.Main; // Transition to Main phase
            gameState.GameMessage = $"Turn {gameState.TurnNumber} - {gameState.CurrentPlayerId}'s turn.";
            _logger.LogInformation("[GameService:{GameId}] Turn advanced to {TurnNumber}. Current Player: {CurrentPlayerId}. Phase: {Phase}.",
               gameState.GameId, gameState.TurnNumber, gameState.CurrentPlayerId, gameState.Phase);


            // 4. Reset turn-specific flags/counters
            startingPlayerState.PlayedCardCountThisTurn = 0;
            // TODO M7+: Reset EP gain flag if applicable (e.g., CanGainEpThisTurn = true)
            // TODO M7+: Reset attack counts for followers on the field
            // foreach (var unit in startingPlayerState.Field) { unit.AttackCountThisTurn = 0; }


            // 5. TODO M7+: Trigger any "start of turn" card effects


            _logger.LogDebug("[GameService:{GameId}] StartOfTurn processing complete for Player {PlayerId}. Generated {EventCount} events.",
                gameState.GameId, startingPlayerState.PlayerId, events.Count);
            return events;
        } // End of ProcessStartOfTurn

        private void ShuffleDeck(List<CardDto> deck) // Start of ShuffleDeck
        {
            _logger.LogWarning("[GameService] Placeholder ShuffleDeck called."); // Original line 725 content
            // Placeholder: Simple Fisher-Yates shuffle
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

        private void ApplyDamage(IBoardEntityState targetEntity, int damage, List<GameEventDto> events)
        {
            // Check for null or non-positive damage first
            if (targetEntity == null || damage <= 0) return; // Corrected <=

            _logger.LogDebug("[GameService] Applying {Damage} damage to Entity {TargetUid} (Current Health: {CurrentHealth})",
                damage, targetEntity.Uid, targetEntity.Health);

            int originalHealth = targetEntity.Health;
            targetEntity.Health -= damage;
            int newHealth = targetEntity.Health;
            int changeAmount = newHealth - originalHealth;

            // Generate Damage Dealt event
            // Note: Checking DTO later if 'Amount' still causes issues
            events.Add(new DamageDealtEventDto { TargetUid = targetEntity.Uid /*, Amount = damage */ });

            // Generate Health Changed event
            events.Add(new HealthChangedEventDto
            {
                TargetUid = targetEntity.Uid,
                NewHealth = newHealth,
                ChangeAmount = changeAmount
            });

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
                        events.Add(new UnitDestroyedEventDto { UnitUid = unit.Uid.ToString() });
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

                    // Note: Checking GameOverEventDto structure later if issues persist
                    events.Add(new GameOverEventDto { /* WinnerPlayerId = opponent.PlayerId */ });
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
    }
}
