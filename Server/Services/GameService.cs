// Server/Services/GameService.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Hubs; // GameHub
using ShadowverseBeyond.Server.Hubs.ClientInterfaces; // IGameClient
using ShadowverseBeyond.Server.Dtos.SignalR;        // DTOs
using ShadowverseBeyond.Server.Dtos.Enums; // Add using for newly created Enums
using ShadowverseBeyond.Server.Exceptions; // GameLogicException
using Server.Domain.Interfaces; // Corrected for ICardRepository
using ShadowverseBeyond.Server.Dtos;             // Add for GameStateDto etc.
using Server.Domain.Entities; // Correct using for CardDefinition
using System;
using System.Collections.Concurrent; // For ConcurrentDictionary
using System.Collections.Generic; // Add for List<>
using System.Linq; // For helper methods potentially
using System.Threading.Tasks;
// Updated Namespace
namespace ShadowverseBeyond.Server.Services
{
    public class GameService : IGameService
    {
        private readonly IHubContext<GameHub, IGameClient> _hubContext;
        private readonly ILogger<GameService> _logger;
        private readonly ICardRepository _cardRepository; // Add Card Repository Field

        // Inject HubContext, Logger and CardRepository
        // Central Game State Storage
        private readonly ConcurrentDictionary<string, GameStateDto> _gameStates = new();
        public GameService(IHubContext<GameHub, IGameClient> hubContext, ILogger<GameService> logger, ICardRepository cardRepository) // Add ICardRepository parameter
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository)); // Assign Card Repository
        }

        // Updated PlayCardAsync implementation
        public async Task PlayCardAsync(string gameId, string playerId, PlayCardActionDto actionDto)
        {
            var targetDisplay = actionDto.TargetUniqueId.ToString() ?? "None"; // Extract TargetUniqueId to variable and use ToString() for ?? operator
            _logger.LogInformation("[GameService] Player {PlayerId} in Game {GameId} attempting PlayCard: CardUID={CardUID}, Target={Target}",
                playerId, gameId, actionDto.CardUidInHand, targetDisplay); // Use variable in log

            try
            {
                // --- Basic Validation ---
                if (string.IsNullOrEmpty(actionDto.CardUidInHand))
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
                _logger.LogInformation("[GameService] Placeholder: Simulating successful card play for {CardUID} by {PlayerId}.", actionDto.CardUidInHand, playerId);

                // Simulate a unit being played event
                // Corrected constructor call based on actual UnitPlayedEventDto definition
                var unitPlayedEvent = new UnitPlayedEventDto(
                    unitUid: Guid.NewGuid(),          // Placeholder unique ID for the unit instance
                    cardDefinitionId: 0,             // Placeholder card definition ID (TODO: Get actual ID)
                    playerId: playerId,              // Player who played the card
                    fieldIndex: 0                    // Placeholder field index (TODO: Determine actual position)
                ); // Base class constructor handles Timestamp and EventType
                string groupName = $"Game_{gameId}";
                _logger.LogInformation("[GameService] Broadcasting UnitPlayed event to group {GroupName}", groupName);
                await _hubContext.Clients.Group(groupName).UnitPlayed(unitPlayedEvent);

                // Simulate a PP update event
                // Corrected constructor call using positional arguments and removing Timestamp
                var ppUpdateEvent = new PpUpdateEventDto(
                    playerId,
                    5, // TODO: Get actual PP value from GameState
                    7  // TODO: Get actual Max PP value from GameState
                ); // Base class constructor handles Timestamp and EventType
                _logger.LogInformation("[GameService] Broadcasting PpUpdate event to group {GroupName}", groupName);
                await _hubContext.Clients.Group(groupName).PpUpdate(ppUpdateEvent);

                _logger.LogInformation("[GameService] Placeholder card play logic completed for Player {PlayerId} in Game {GameId}.", playerId, gameId);

            }
            catch (GameLogicException ex)
            {
                // Logged in the Hub already, re-throw for Hub to catch and notify caller.
                _logger.LogWarning("[GameService] Game Logic Error during PlayCard for Player {PlayerId} in Game {GameId}: {Message}", playerId, gameId, ex.Message);
                throw; // Re-throw GameLogicException for the Hub to handle
            }
            catch (Exception ex)
            {
                // Log unexpected errors here. The Hub will catch this and send a generic error.
                _logger.LogError(ex, "[GameService] Unexpected error during PlayCard for Player {PlayerId} in Game {GameId}", playerId, gameId);
                throw new GameLogicException("An unexpected error occurred in the game service.", ex); // Wrap in GameLogicException
            }
        }

        // Implement EndTurnAsync
        public async Task EndTurnAsync(string gameId, string playerId)
        {
            _logger.LogInformation("[GameService] Player {PlayerId} in Game {GameId} attempting EndTurn.", playerId, gameId);

            try
            {
                // TODO M4+: Implement actual end turn logic:
                // 1. Validate gameId and playerId.
                // 2. Load GameState.
                // 3. Validate it's the player's turn.
                // 4. Perform end-of-turn effects (if any).
                // 5. Update GameState (active player, PP reset for next player, draw card for next player etc.).
                // 6. Determine resulting events (TurnChanged, PpUpdate, CardDrawn etc.).
                // 7. Save updated GameState.
                // 8. Broadcast generated events.

                // --- Placeholder Logic & Broadcasting ---
                _logger.LogInformation("[GameService] Placeholder: Simulating successful turn end for {PlayerId}.", playerId);

                // Simulate a simple "Turn Ended" notification (replace with specific DTOs later)
                // For now, maybe reuse an existing DTO or just log. A specific "TurnChangedEventDto" would be ideal.
                string groupName = $"Game_{gameId}";
                // Example: await _hubContext.Clients.Group(groupName).TurnChanged(new TurnChangedEventDto(...));
                _logger.LogInformation("[GameService] Placeholder: Turn ended logic complete. Events would be broadcast to group {GroupName}.", groupName);

                // Simulate the next player starting their turn (e.g., PP update, card draw) - requires opponent player ID
                // string opponentPlayerId = GetOpponentPlayerId(gameId, playerId); // Placeholder
                // await _hubContext.Clients.Group(groupName).PpUpdate(new PpUpdateEventDto(...));
                // await _hubContext.Clients.Group(groupName).CardDrawn(new CardDrawnEventDto(...));


                await Task.CompletedTask; // Placeholder for async operations
            }
            catch (GameLogicException ex)
            {
                _logger.LogWarning("[GameService] Game Logic Error during EndTurn for Player {PlayerId} in Game {GameId}: {Message}", playerId, gameId, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameService] Unexpected error during EndTurn for Player {PlayerId} in Game {GameId}", playerId, gameId);
                throw new GameLogicException("An unexpected error occurred in the game service during end turn.", ex);
            }
        }
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

                // Fetch default card definitions (e.g., Goblin - ID F001 as placeholder)
                var defaultFollowerDef = await _cardRepository.GetCardByIdAsync("F001"); // Goblin
                                                                                         // TODO: Need robust default deck logic instead of just Goblins.
                                                                                         // Example: Fetch a list of card IDs for a default deck.

                List<CardDto> player1Deck = await CreateDeckFromDefinitionAsync(defaultFollowerDef, 40); // Helper needed
                List<CardDto> player2Deck = await CreateDeckFromDefinitionAsync(defaultFollowerDef, 40); // Helper needed

                ShuffleDeck(player1Deck); // Helper needed
                ShuffleDeck(player2Deck); // Helper needed

                int initialHandSize = 3;
                List<CardDto> player1Hand = DrawInitialHand(player1Deck, initialHandSize); // Helper needed
                List<CardDto> player2Hand = DrawInitialHand(player2Deck, initialHandSize); // Helper needed

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
                    CurrentEP = player1IsFirst ? 0 : 1, // Assuming second player gets 1 EP start bonus - adjust in turn logic maybe?
                    MaxEP = player1IsFirst ? 2 : 3,    // Assuming T4/T5 evo points (P1/P2) - adjust later?
                    CanEvolve = false,
                    Hand = player1Hand,
                    DeckCount = player1Deck.Count, // Actual count after draw
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
                    CurrentEP = player1IsFirst ? 1 : 0, // EP bonus
                    MaxEP = player1IsFirst ? 3 : 2,    // EP Max
                    CanEvolve = false,
                    Hand = player2Hand,
                    DeckCount = player2Deck.Count,
                    Field = new List<UnitStateDto>(),
                    IsFirst = !player1IsFirst,
                    PlayedCardCountThisTurn = 0 // Reset counter
                                                // Graveyard = new List<CardDto>()
                };

                // TODO: Need to store the full deck information somewhere associated with PlayerStateDto,
                // not just the DeckCount, so cards can be drawn later.
                // Maybe PlayerStateDto needs a private List<CardDto> _deck field, or GameService keeps track.
                // For now, DeckCount is a simplification. We also need to store the actual deck lists.

                var newGameState = new GameStateDto
                {
                    GameId = gameId,
                    CurrentPlayerId = firstPlayerId, // First player takes the first turn
                    FirstPlayerId = firstPlayerId,
                    TurnNumber = 0, // Turn 0 for setup, increments to 1 on first StartTurn
                    Phase = GamePhase.Initialization, // Start in Init phase (using Enum)
                    Players = new List<PlayerStateDto> { player1State, player2State },
                    IsGameOver = false,
                    WinnerId = null,
                    GameStatus = GameStatus.InProgress, // Use enum
                    GameMessage = "Game created. Waiting for players." // Initial message
                };

                if (_gameStates.TryAdd(gameId, newGameState))
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
                _logger.LogError(ex, "[GameService] Unexpected error during game state creation for Game {GameId}", gameId);
                // Return null to indicate failure.
                return null;
            }
        } // This closes the GetOrCreateGameStateAsync method body

        // TODO: Implement helper methods used above - Placeholder Implementations
        private async Task<List<CardDto>> CreateDeckFromDefinitionAsync(CardDefinition? cardDef, int count) // Reverted to simple name, should be resolved by using statement
        {
            _logger.LogWarning("[GameService] Placeholder CreateDeckFromDefinitionAsync called.");
            // Placeholder: Create a deck of 'count' cards based on the definition
            List<CardDto> deck = new List<CardDto>();
            if (cardDef != null)
            {
                for (int i = 0; i < count; i++)
                {
                    deck.Add(new CardDto { Uid = Guid.NewGuid(), CardId = cardDef.Id.ToString(), Name = cardDef.Name, Cost = cardDef.Cost, Type = cardDef.Type, BaseAttack = cardDef.Attack, BaseHealth = cardDef.Health });
                }
            }
            else
            {
                _logger.LogError("[GameService] Cannot create deck, card definition was null.");
            }
            return await Task.FromResult(deck); // Return completed task
        }

        private void ShuffleDeck(List<CardDto> deck)
        {
            _logger.LogWarning("[GameService] Placeholder ShuffleDeck called.");
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
    }
}