using Xunit;
using Moq; // Using Moq as an example mocking library, can be changed
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using ShadowverseBeyond.Server.Dtos.SignalR; // Added for GameEventDto base class
using ShadowverseBeyond.Server.Models; // Added for GameActionContext
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions; // Added for IClientMethodMapper
using ShadowverseBeyond.Server.Exceptions; // Added for GameLogicException
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Dtos.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent; // For accessing internal state via reflection if needed, or mocking interactions

namespace ShadowverseBeyond.Server.Tests
{
    public class GameServiceTests
    {
        // --- Mock Setup Helper ---
        // Helper to setup mocks for GameService dependencies
        private (GameService service,
                 Mock<IHubContext<GameHub, IGameClient>> mockHubContext,
                 Mock<ILogger<GameService>> mockLogger,
                 Mock<IServiceScopeFactory> mockScopeFactory,
                 Mock<ICardRepository> mockCardRepository, // Keep CardRepository mock for scope setup
                 Mock<IAiService> mockAiService,
                 Mock<IServiceProvider> mockServiceProvider, // Change AiTurnExecutor to IServiceProvider
                 Mock<IClientMethodMapper> mockClientMethodMapper) // Add ClientMethodMapper mock
                 SetupMocksAndService()
        {
            var mockHubContext = new Mock<IHubContext<GameHub, IGameClient>>();
            var mockLogger = new Mock<ILogger<GameService>>();
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            var mockScope = new Mock<IServiceScope>();
            var mockCardRepository = new Mock<ICardRepository>();

            var mockAiService = new Mock<IAiService>();
            // Change variable name and type - This line is removed as mockServiceProvider is already available from the helper method parameters/setup
            // var mockServiceProvider = new Mock<IServiceProvider>();
            var mockClientMethodMapper = new Mock<IClientMethodMapper>(); // Add mock creation

            // Setup Scope Factory to return a scope that provides the mock Card Repository
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
            mockServiceProvider.Setup(sp => sp.GetService(typeof(ICardRepository)))
                              .Returns(mockCardRepository.Object);
            // GetRequiredService setup removed as Moq doesn't support setting up extension methods.
            // The setup for GetService should be sufficient.


            var gameService = new GameService(
                mockHubContext.Object,
                mockLogger.Object,
                mockScopeFactory.Object,
                mockAiService.Object,     // Pass IAiService mock
                mockServiceProvider.Object, // Pass IServiceProvider mock instead of AiTurnExecutor
                mockClientMethodMapper.Object // Pass ClientMethodMapper mock
    );

            // Update return tuple
            return (gameService, mockHubContext, mockLogger, mockScopeFactory, mockCardRepository,
                    mockAiService, mockServiceProvider, // Return IServiceProvider mock
                    mockClientMethodMapper); // Return ClientMethodMapper mock
        }

        // --- Helper to access internal game states (Use with caution!) ---
        private ConcurrentDictionary<string, GameStateDto>? GetInternalGameStates(GameService service)
        {
            // Use reflection to access the private _gameStates field for testing setup/assertion
            var fieldInfo = typeof(GameService).GetField("_gameStates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return fieldInfo?.GetValue(service) as ConcurrentDictionary<string, GameStateDto>;
        }
        private ConcurrentDictionary<string, object>? GetInternalGameLocks(GameService service)
        {
            var fieldInfo = typeof(GameService).GetField("_gameLocks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return fieldInfo?.GetValue(service) as ConcurrentDictionary<string, object>;
        }


        [Fact]
        public async Task PlayCardAsync_PlayFollower_Success()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _, _) = SetupMocksAndService();
            string gameId = "testGame1";
            string playerId = "player1";
            string opponentId = "player2";
            int followerCardId = 1; // Changed type to int and value
            string followerCardUid = Guid.NewGuid().ToString(); // Unique ID for the card instance in hand

            // 1. Mock Card Definition
            var followerDefinition = new CardDefinition
            {
                Id = followerCardId, // Now uses int followerCardId
                Name = "Goblin",
                Cost = 1,
                Type = CardType.Follower.ToString(),
                Attack = 1,
                Health = 2,
                Keywords = new List<string>(),
                IsCollectible = true
                // Add other necessary properties if needed by the logic being tested
            };
            // Fix: Setup mock with the string representation of the integer CardId (1)
            // Modify mock to return a new instance inline to isolate potential issues
            mockCardRepository.Setup(repo => repo.GetCardDefinitionByIdAsync(1)) // Changed "1" to 1
                             .ReturnsAsync(() => new CardDefinition
                             {
                                 Id = 1, // Match the lookup key (Changed "1" to 1)
                                 Name = "Goblin",
                                 Cost = 1,
                                 Type = CardType.Follower.ToString(),
                                 Attack = 1, // Explicitly set Attack = 1 here
                                 Health = 2,
                                 Keywords = new List<string>(),
                                 IsCollectible = true
                             });

            // 2. Create Initial Game State
            var initialHand = new List<CardInstanceDto> // Changed CardDto to CardInstanceDto
            {
                new CardInstanceDto { InstanceId = Guid.Parse(followerCardUid), CardId = followerCardId, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString(), Attack=1, Health=2, CurrentAttack=1, CurrentHealth=2, MaxHealth=2 } // Use followerCardId variable
            };
            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                Deck = new List<CardInstanceDto>(), // Changed CardDto to CardInstanceDto
                DeckCount = 0,
                Field = new List<CardInstanceDto>(), // Empty field
                Graveyard = new List<CardInstanceDto>(), // Changed CardDto to CardInstanceDto
                CurrentPP = 5, // Enough PP
                MaxPP = 5,
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = playerId // Set controller ID using the variable from the test scope
                },
                CurrentEP = 0,
                MaxEP = 2,
                CanEvolve = false,
                IsFirst = true,
                HasCompletedMulligan = true,
                PlayedCardCountThisTurn = 0,
                HasTakenFirstTurn = true,
                IsAI = false
            };
            var opponentState = new PlayerStateDto
            {
                PlayerId = opponentId, // Ensure opponent exists
                Hand = new List<CardInstanceDto>(), // Changed CardDto to CardInstanceDto
                Deck = new List<CardInstanceDto>(), // Changed CardDto to CardInstanceDto
                DeckCount = 0,
                Field = new List<CardInstanceDto>(),
                Graveyard = new List<CardInstanceDto>(), // Changed CardDto to CardInstanceDto
                CurrentPP = 5,
                MaxPP = 5,
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = opponentId // Set controller ID using the variable from the test scope
                },
                CurrentEP = 0,
                MaxEP = 3,
                CanEvolve = false,
                IsFirst = false,
                HasCompletedMulligan = true,
                PlayedCardCountThisTurn = 0,
                HasTakenFirstTurn = true,
                IsAI = true
            };
            var initialGameState = new GameStateDto
            {
                GameId = gameId,
                CurrentPlayerId = playerId, // Player's turn
                FirstPlayerId = playerId,
                // TurnNumber removed from GameStateDto init
                Phase = GamePhase.Main, // Correct phase
                Players = new List<PlayerStateDto> { playerState, opponentState },
                IsGameOver = false
            };

            // 3. Add initial state to GameService (using reflection helper)
            ConcurrentDictionary<string, GameStateDto>? gameStates = GetInternalGameStates(gameService); // Make nullable
            ConcurrentDictionary<string, object>? gameLocks = GetInternalGameLocks(gameService); // Make nullable
            Assert.NotNull(gameStates); // Ensure reflection worked
            Assert.NotNull(gameLocks);
            gameStates![gameId] = initialGameState; // Use null-forgiving operator
            gameLocks![gameId] = new object(); // Use null-forgiving operator

            // 4. Create Action DTO
            var actionDto = new PlayCardActionDto
            {
                CardUid = followerCardUid,
                TargetUid = null // No target for basic follower play
            };

            // Act
            var context = new GameActionContext(); // M8 Test Fix
            await gameService.PlayCardAsync(gameId, playerId, actionDto, context); // M8 Test Fix: Pass context, removed result assignment

            // Assert
            // M8 Test Fix: Assertions below need to be updated to check context.Events instead of context.Events
            // 1. Check Player State Changes
            Assert.Equal(4, playerState.CurrentPP); // PP reduced by 1 (cost of Goblin)
            Assert.Empty(playerState.Hand); // Card removed from hand
            Assert.Single(playerState.Field); // One unit added to field
            Assert.Equal(1, playerState.PlayedCardCountThisTurn);

            // 2. Check Unit State on Field
            var playedUnit = playerState.Field.First();
            Assert.Equal(followerCardId, playedUnit.CardId); // Compare with followerCardId variable
            Assert.Equal("Goblin", playedUnit.Name);
            Assert.Equal(1, playedUnit.CurrentAttack);
            Assert.Equal(2, playedUnit.CurrentHealth);
            Assert.False(playedUnit.CanAttack); // Summoning sickness
            Assert.False(playedUnit.IsEvolved);

            // 3. Check Returned Events
            Assert.NotNull(context.Events);
            Assert.Equal(3, context.Events.Count); // Expecting PPUpdate, CardPlayed, CardSummoned (HandUpdate is deprecated)

            // Refactor event checking for clarity
            bool ppEventFound = false;
            bool cardPlayedEventFound = false;
            bool cardSummonedEventFound = false; // Changed from unitPlayedEventFound
            CardSummonedEventDto? foundCardSummonedEvent = null; // Changed type and name
            PpUpdateEventDto? foundPpEvent = null;
            CardPlayedEventDto? foundCardPlayedEvent = null;


            foreach (var ev in context.Events)
            {
                if (ev is PpUpdateEventDto ppEvent)
                {
                    foundPpEvent = ppEvent;
                    ppEventFound = true;
                }
                else if (ev is CardPlayedEventDto cardPlayedEvent)
                {
                    foundCardPlayedEvent = cardPlayedEvent;
                    cardPlayedEventFound = true;
                }
                else if (ev is CardSummonedEventDto cardSummonedEvent) // Changed type check
                {
                    foundCardSummonedEvent = cardSummonedEvent; // Changed variable assignment
                    cardSummonedEventFound = true; // Changed flag assignment
                }
            }

            // Assert that all expected event types were found
            Assert.True(ppEventFound, "PpUpdateEventDto not found.");
            Assert.True(cardPlayedEventFound, "CardPlayedEventDto not found.");
            Assert.True(cardSummonedEventFound, "CardSummonedEventDto not found."); // Changed assertion target and message

            // Now assert the content of the found events
            Assert.NotNull(foundPpEvent);
            Assert.Equal(playerId, foundPpEvent.SourcePlayerId);
            Assert.Equal(4, foundPpEvent.CurrentPp);
            Assert.Equal(5, foundPpEvent.MaxPp);

            Assert.NotNull(foundCardPlayedEvent); // Basic check is enough for now

            Assert.NotNull(foundCardSummonedEvent); // Changed variable name
            Assert.Equal(playerId, foundCardSummonedEvent.SourcePlayerId); // Changed variable name
            // Assert.Equal(playerId, foundCardSummonedEvent.PlayerId); // CardSummonedEventDto might not have PlayerId directly
            // Assert.Equal(followerCardUid, foundCardSummonedEvent.SourceCardUid); // CardSummonedEventDto might not have SourceCardUid
            Assert.NotNull(foundCardSummonedEvent.SummonedCard); // <<< Critical check (Corrected property name)
            Assert.Equal(playedUnit.InstanceId, foundCardSummonedEvent.SummonedCard.InstanceId); // Corrected property name
            Assert.Equal(followerCardId, foundCardSummonedEvent.SummonedCard.CardId); // Use followerCardId variable

            // 4. Verify Mock Interactions (Optional but good practice)
            mockCardRepository.Verify(repo => repo.GetCardDefinitionByIdAsync(1), Times.Once); // Verify with 1
        }

        // 实用测试：快速游戏设置
        [Fact]
        public async Task QuickGameSetup_ShouldCreatePlayableGame()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _, _) = SetupMocksAndService();
            string gameId = "quick-test-game";
            // string player1Id = "player1"; // Placeholder - Removed unused variable
            // string player2Id = "player2"; // Placeholder - Removed unused variable

            // Mock card definitions for a basic game
            var testCards = new List<CardDefinition>
            {
                new CardDefinition { Id = 0, Name = "Leader", Cost = 0, Attack = 0, Health = 20, Type = "Leader", IsCollectible = false },
                new CardDefinition { Id = 1, Name = "Test Follower", Cost = 2, Attack = 2, Health = 2, Type = "Follower", IsCollectible = true },
                new CardDefinition { Id = 2, Name = "Test Spell", Cost = 1, Type = "Spell", IsCollectible = true }
            };

            mockCardRepository.Setup(repo => repo.GetAllCardDefinitionsAsync())
                .ReturnsAsync(testCards);

            // Act & Assert - 创建完整的可玩游戏
            var gameState1 = await gameService.GetOrCreateGameStateAsync(gameId);
            Assert.NotNull(gameState1);

            var gameState2 = await gameService.GetOrCreateGameStateAsync(gameId);
            Assert.NotNull(gameState2);
            Assert.Equal(2, gameState2.Players.Count);

            // 验证游戏状态
            Assert.Equal("InProgress", gameState2.GameStatus.ToString());
            Assert.True(gameState2.Players.All(p => p.Hand.Count > 0)); // 玩家应该有手牌
            Assert.True(gameState2.Players.All(p => p.LeaderCard != null)); // 玩家应该有领袖
        }

        [Fact]
        public async Task PlayCardAsync_PlayFollower_InsufficientPP()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _, _) = SetupMocksAndService();
            string gameId = "testGameIPP";
            string playerId = "playerIPP";
            string opponentId = "opponentIPP";
            int followerCardId = 1; // Changed type to int and value
            string followerCardUid = Guid.NewGuid().ToString();

            // Mock Card Definition (Cost 1)
            var followerDefinition = new CardDefinition { Id = followerCardId, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString() /* ... other needed props */ }; // Added Name
            // Fix: Setup mock with the integer CardId (1)
            mockCardRepository.Setup(repo => repo.GetCardDefinitionByIdAsync(1)).ReturnsAsync(followerDefinition); // Changed "1" to 1

            // Initial Game State with Insufficient PP
            var initialHand = new List<CardInstanceDto> { new CardInstanceDto { InstanceId = Guid.Parse(followerCardUid), CardId = 1, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString(), Attack = 1, Health = 2, CurrentAttack = 1, CurrentHealth = 2, MaxHealth = 2 } }; // Changed to CardInstanceDto, fixed ID/Props
            var playerState = new PlayerStateDto { PlayerId = playerId, Hand = initialHand, CurrentPP = 0, MaxPP = 5, Field = new List<CardInstanceDto>() }; // Fix: Change List Type // PP = 0!
            var opponentState = new PlayerStateDto { PlayerId = opponentId, CurrentPP = 5, MaxPP = 5 }; // Opponent state needed for GameStateDto validity
            var initialGameState = new GameStateDto
            {
                GameId = gameId,
                CurrentPlayerId = playerId,
                Phase = GamePhase.Main, // Set correct phase
                Players = new List<PlayerStateDto> { playerState, opponentState }
                // ... other necessary GameStateDto properties ...
            };

            ConcurrentDictionary<string, GameStateDto>? gameStates = GetInternalGameStates(gameService); // Make nullable
            ConcurrentDictionary<string, object>? gameLocks = GetInternalGameLocks(gameService); // Make nullable
            Assert.NotNull(gameStates);
            Assert.NotNull(gameLocks);
            gameStates![gameId] = initialGameState; // Use null-forgiving operator
            gameLocks![gameId] = new object(); // Use null-forgiving operator

            var actionDto = new PlayCardActionDto { CardUid = followerCardUid };

            // Act & Assert Exception
            var context = new GameActionContext(); // M8 Test Fix
            var exception = await Assert.ThrowsAsync<GameLogicException>(() => // Expect GameLogicException
                gameService.PlayCardAsync(gameId, playerId, actionDto, context) // M8 Test Fix: Pass context
            );

            // Assert Correct Exception Message
            Assert.Equal("Not enough PP to play Goblin. Requires 1, has 0.", exception.Message); // Verify the correct message for insufficient PP (added period)

            // Assert State Unchanged
            Assert.Equal(0, playerState.CurrentPP); // PP remains 0
            Assert.Single(playerState.Hand); // Card still in hand
            Assert.Empty(playerState.Field); // Field remains empty
                                             // Assert No Events Emitted: Handled by Assert.ThrowsAsync catching the exception before events are generated.



            // Verify Mock Interactions (Optional: verify card repo wasn't called again if check happens early)
            mockCardRepository.Verify(repo => repo.GetCardDefinitionByIdAsync(It.IsAny<int>()), Times.Once); // Changed It.IsAny<string>() to It.IsAny<int>()
        }
        [Fact]
        public async Task PlayCardAsync_PlayFollower_FieldFull()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _, _) = SetupMocksAndService();
            string gameId = "testGameFieldFull";
            string playerId = "playerFieldFull";
            string opponentId = "opponentFieldFull";
            int followerCardId = 1; // Changed type to int and value
            string followerCardUid = Guid.NewGuid().ToString();
            const int MaxFieldSize = 7; // As defined in GameService

            // Mock Card Definition (Cost 1)
            var followerDefinition = new CardDefinition { Id = followerCardId, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString() };
            // Fix: Setup mock with the integer CardId (1)
            mockCardRepository.Setup(repo => repo.GetCardDefinitionByIdAsync(1)).ReturnsAsync(followerDefinition); // Changed "1" to 1

            // Initial Game State with Full Field and Sufficient PP
            var initialHand = new List<CardInstanceDto> { new CardInstanceDto { InstanceId = Guid.Parse(followerCardUid), CardId = 1, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString(), Attack = 1, Health = 2, CurrentAttack = 1, CurrentHealth = 2, MaxHealth = 2 } }; // Changed to CardInstanceDto, fixed ID/Props
            var fullField = new List<CardInstanceDto>(); // Fix: Change List Type
            for (int i = 0; i < MaxFieldSize; i++)
            {
                // Add dummy units - ensure they have unique Uids if necessary, though not strictly needed for this test logic
                // Fix: Create CardInstanceDto instead of UnitStateDto
                fullField.Add(new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(),
                    CardId = i + 1000, // Use int ID
                    Name = $"DummyUnit{i}",
                    CurrentHealth = 1,
                    MaxHealth = 1, // Need MaxHealth
                    Type = CardType.Follower.ToString() // Need Type
                    // Add other required CardInstanceDto properties if needed
                });
            }

            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                CurrentPP = 1, // Sufficient PP
                MaxPP = 5,
                Field = fullField, // Fix: Remove unnecessary cast // Field is full!
                Deck = new List<CardInstanceDto>(),
                DeckCount = 0,
                Graveyard = new List<CardInstanceDto>(),
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = playerId // Set controller ID using the variable from the test scope
                },
                CurrentEP = 0,
                MaxEP = 2,
                CanEvolve = false,
                IsFirst = true,
                HasCompletedMulligan = true,
                PlayedCardCountThisTurn = 0,
                HasTakenFirstTurn = true,
                IsAI = false // Added minimal required properties
            };
            var opponentState = new PlayerStateDto
            {
                PlayerId = opponentId,
                Hand = new(),
                Deck = new(),
                Field = new(),
                Graveyard = new(),
                DeckCount = 0,
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = opponentId // Set controller ID using the variable from the test scope
                },
                CurrentPP = 5,
                MaxPP = 5,
                CurrentEP = 0,
                MaxEP = 3,
                CanEvolve = false,
                IsFirst = false,
                HasCompletedMulligan = true,
                PlayedCardCountThisTurn = 0,
                HasTakenFirstTurn = true,
                IsAI = true
            }; // Minimal opponent state
            var initialGameState = new GameStateDto
            {
                GameId = gameId,
                CurrentPlayerId = playerId,
                FirstPlayerId = playerId, // Restore
                // TurnNumber removed from GameStateDto init
                Phase = GamePhase.Main, // Restore
                Players = new List<PlayerStateDto> { playerState, opponentState }, // Restore
                IsGameOver = false // Restore
            }; // Keep semicolon
            ConcurrentDictionary<string, GameStateDto>? gameStates = GetInternalGameStates(gameService); // Make nullable
            ConcurrentDictionary<string, object>? gameLocks = GetInternalGameLocks(gameService); // Make nullable
            Assert.NotNull(gameStates);
            Assert.NotNull(gameLocks);
            gameStates![gameId] = initialGameState; // Use null-forgiving operator
            gameLocks![gameId] = new object(); // Use null-forgiving operator

            var actionDto = new PlayCardActionDto { CardUid = followerCardUid };

            // Act & Assert Exception
            var context = new GameActionContext(); // M8 Test Fix
            var exception = await Assert.ThrowsAsync<GameLogicException>(() =>
                gameService.PlayCardAsync(gameId, playerId, actionDto, context) // M8 Test Fix: Pass context
            );

            // Assert Correct Exception Message (Matches GameService format)
            Assert.Equal($"Cannot play Goblin, field is full ({MaxFieldSize}/{MaxFieldSize}).", exception.Message);

            // Assert State Unchanged
            Assert.Equal(1, playerState.CurrentPP); // PP remains unchanged
            Assert.Single(playerState.Hand); // Card still in hand
            Assert.Equal(MaxFieldSize, playerState.Field.Count); // Field still full

            // Verify Mock Interactions
            mockCardRepository.Verify(repo => repo.GetCardDefinitionByIdAsync(1), Times.Once); // Fetch happens before validation, explicitly check for 1

            // Body of FieldFull test is intentionally left empty for now
        } // Add closing brace for PlayCardAsync_PlayFollower_FieldFull method
        [Fact]
        public async Task PlayCardAsync_PlayFollower_WrongTurn()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _, _) = SetupMocksAndService();
            string gameId = "testGameWrongTurn";
            string playerId = "playerWrongTurn"; // Player attempting the action
            string opponentId = "opponentWhoseTurnItIs"; // Player whose turn it actually is
            int followerCardId = 1; // Goblin cost 1 - ID changed from F001 // Changed type to int
            string followerCardUid = Guid.NewGuid().ToString();

            // Mock Card Definition (Not strictly needed as check happens earlier, but good practice)
            var followerDefinition = new CardDefinition { Id = followerCardId, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString() }; // Uses int followerCardId now
            // Fix: Setup mock with the integer CardId (1)
            mockCardRepository.Setup(repo => repo.GetCardDefinitionByIdAsync(1)).ReturnsAsync(followerDefinition); // Changed "1" to 1

            // Initial Game State where it's the opponent's turn
            var initialHand = new List<CardInstanceDto> { new CardInstanceDto { InstanceId = Guid.Parse(followerCardUid), CardId = followerCardId, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString(), Attack = 1, Health = 2, CurrentAttack = 1, CurrentHealth = 2, MaxHealth = 2 } }; // Changed to CardInstanceDto, fixed ID/Props, use int followerCardId
            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                CurrentPP = 5, // Enough PP
                MaxPP = 5,
                Field = new List<CardInstanceDto>(), // Fix: Change List Type
                Deck = new List<CardInstanceDto>(),
                DeckCount = 0,
                Graveyard = new List<CardInstanceDto>(),
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = playerId // Set controller ID using the variable from the test scope
                },
                IsAI = false
            };
            var opponentState = new PlayerStateDto
            {
                PlayerId = opponentId, // Opponent state
                CurrentPP = 5,
                MaxPP = 5,
                Hand = new(),
                Field = new(),
                Deck = new(),
                DeckCount = 0,
                Graveyard = new(),
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = opponentId // Set controller ID using the variable from the test scope
                },
                IsAI = true
            };
            var initialGameState = new GameStateDto
            {
                GameId = gameId,
                CurrentPlayerId = opponentId, // Opponent's turn!
                Phase = GamePhase.Main,
                Players = new List<PlayerStateDto> { playerState, opponentState },
                FirstPlayerId = opponentId, // Example
                // TurnNumber removed from GameStateDto init
                IsGameOver = false
            };

            ConcurrentDictionary<string, GameStateDto>? gameStates = GetInternalGameStates(gameService); // Make nullable
            ConcurrentDictionary<string, object>? gameLocks = GetInternalGameLocks(gameService); // Make nullable
            Assert.NotNull(gameStates);
            Assert.NotNull(gameLocks);
            gameStates![gameId] = initialGameState; // Use null-forgiving operator
            gameLocks![gameId] = new object(); // Use null-forgiving operator

            var actionDto = new PlayCardActionDto { CardUid = followerCardUid };

            // Act & Assert Exception
            var context = new GameActionContext(); // M8 Test Fix
            var exception = await Assert.ThrowsAsync<GameLogicException>(() =>
                gameService.PlayCardAsync(gameId, playerId, actionDto, context) // M8 Test Fix: PlayerId attempts action on opponentId's turn
            );

            // Assert Correct Exception Message
            Assert.Equal($"It's not player {playerId}'s turn.", exception.Message); // Match actual message format

            // Assert State Unchanged (Optional but good)
            Assert.Equal(5, playerState.CurrentPP); // PP remains unchanged
            Assert.Single(playerState.Hand);       // Card still in hand
            Assert.Empty(playerState.Field);       // Field remains empty

            // Verify Mock Interactions (Should not fetch card if turn check fails first)
            mockCardRepository.Verify(repo => repo.GetCardDefinitionByIdAsync(It.IsAny<int>()), Times.Once); // Changed It.IsAny<string>() to It.IsAny<int>()
        }
        [Fact]
        public async Task PlayCardAsync_PlayFollower_WrongPhase()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _, _) = SetupMocksAndService();
            string gameId = "testGameWrongPhase";
            string playerId = "playerWrongPhase";
            string opponentId = "opponentWrongPhase";
            int followerCardId = 1; // Goblin cost 1 // Changed type to int, assuming F001 maps to 1
            string followerCardUid = Guid.NewGuid().ToString();

            // Mock Card Definition (Not strictly needed as check happens earlier)
            var followerDefinition = new CardDefinition { Id = followerCardId, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString() }; // Uses int followerCardId now
            // Fix: Setup mock with the integer CardId (1)
            mockCardRepository.Setup(repo => repo.GetCardDefinitionByIdAsync(1)).ReturnsAsync(followerDefinition); // Changed "1" to 1

            // Initial Game State in Mulligan Phase
            var initialHand = new List<CardInstanceDto> { new CardInstanceDto { InstanceId = Guid.Parse(followerCardUid), CardId = followerCardId, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString(), Attack = 1, Health = 2, CurrentAttack = 1, CurrentHealth = 2, MaxHealth = 2 } }; // Changed to CardInstanceDto, fixed ID/Props, use int followerCardId
            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                CurrentPP = 5, // Enough PP
                MaxPP = 5,
                Field = new List<CardInstanceDto>(), // Fix: Change List Type
                Deck = new List<CardInstanceDto>(),
                DeckCount = 0,
                Graveyard = new List<CardInstanceDto>(),
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = playerId // Set controller ID using the variable from the test scope
                },
                IsAI = false
            };
            var opponentState = new PlayerStateDto
            {
                PlayerId = opponentId,
                CurrentPP = 5,
                MaxPP = 5,
                Hand = new(),
                Field = new(),
                Deck = new(),
                DeckCount = 0,
                Graveyard = new(),
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = opponentId // Set controller ID using the variable from the test scope
                },
                IsAI = true
            };
            var initialGameState = new GameStateDto
            {
                GameId = gameId,
                CurrentPlayerId = playerId, // It IS player's turn
                Phase = GamePhase.Mulligan, // But wrong phase!
                Players = new List<PlayerStateDto> { playerState, opponentState },
                FirstPlayerId = playerId, // Example
                // TurnNumber removed from GameStateDto init
                IsGameOver = false
            };

            ConcurrentDictionary<string, GameStateDto>? gameStates = GetInternalGameStates(gameService); // Make nullable
            ConcurrentDictionary<string, object>? gameLocks = GetInternalGameLocks(gameService); // Make nullable
            Assert.NotNull(gameStates);
            Assert.NotNull(gameLocks);
            gameStates![gameId] = initialGameState; // Use null-forgiving operator
            gameLocks![gameId] = new object(); // Use null-forgiving operator

            var actionDto = new PlayCardActionDto { CardUid = followerCardUid };

            // Act & Assert Exception
            var context = new GameActionContext(); // M8 Test Fix
            var exception = await Assert.ThrowsAsync<GameLogicException>(() =>
                gameService.PlayCardAsync(gameId, playerId, actionDto, context) // M8 Test Fix: Pass context
            );

            // Assert Correct Exception Message
            Assert.Equal($"Can only play cards during the Main phase (Current: {initialGameState.Phase}).", exception.Message); // Match actual message including phase

            // Assert State Unchanged (Optional)
            Assert.Equal(5, playerState.CurrentPP);
            Assert.Single(playerState.Hand);
            Assert.Empty(playerState.Field);

            // Verify Mock Interactions (Should not fetch card if phase check fails first)
            mockCardRepository.Verify(repo => repo.GetCardDefinitionByIdAsync(It.IsAny<int>()), Times.Once); // Changed It.IsAny<string>() to It.IsAny<int>()
        }
        [Fact]
        public async Task PlayCardAsync_CardNotFoundInHand()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _, _) = SetupMocksAndService();
            string gameId = "testGameCardNotFound";
            string playerId = "playerCardNotFound";
            string opponentId = "opponentCardNotFound";
            string nonExistentCardUid = Guid.NewGuid().ToString(); // 一个手牌中不存在的卡牌 UID

            // 初始游戏状态，玩家手牌为空或不包含目标卡牌
            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = new List<CardInstanceDto>(), // 空手牌确保找不到卡牌
                CurrentPP = 5, // 确保有足够的 PP，以免因此失败
                MaxPP = 5,
                Field = new List<CardInstanceDto>(), // Fix: Change List Type
                Deck = new List<CardInstanceDto>(),
                DeckCount = 0,
                Graveyard = new List<CardInstanceDto>(),
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = playerId // Set controller ID using the variable from the test scope
                },
                IsAI = false
            };
            var opponentState = new PlayerStateDto
            {
                PlayerId = opponentId,
                CurrentPP = 5,
                MaxPP = 5,
                Hand = new(),
                Field = new(),
                Deck = new(),
                DeckCount = 0,
                Graveyard = new(),
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = opponentId // Set controller ID using the variable from the test scope
                },
                IsAI = true
            };
            var initialGameState = new GameStateDto
            {
                GameId = gameId,
                CurrentPlayerId = playerId, // 玩家的回合
                Phase = GamePhase.Main,     // 正确的阶段
                Players = new List<PlayerStateDto> { playerState, opponentState },
                FirstPlayerId = playerId,   // 示例
                // TurnNumber removed from GameStateDto init
                IsGameOver = false
            };

            ConcurrentDictionary<string, GameStateDto>? gameStates = GetInternalGameStates(gameService); // Make nullable
            ConcurrentDictionary<string, object>? gameLocks = GetInternalGameLocks(gameService); // Make nullable
            Assert.NotNull(gameStates);
            Assert.NotNull(gameLocks);
            gameStates![gameId] = initialGameState; // Use null-forgiving operator
            gameLocks![gameId] = new object(); // Use null-forgiving operator

            var actionDto = new PlayCardActionDto { CardUid = nonExistentCardUid };

            // Act & Assert Exception
            var context = new GameActionContext(); // M8 Test Fix
            var exception = await Assert.ThrowsAsync<GameLogicException>(() =>
                gameService.PlayCardAsync(gameId, playerId, actionDto, context) // M8 Test Fix: Pass context
            );

            // Assert Correct Exception Message
            Assert.Equal($"Card with UID {actionDto.CardUid} not found in player {playerId}'s hand (during card fetch).", exception.Message);

            // Assert State Unchanged
            Assert.Equal(5, playerState.CurrentPP);
            Assert.Empty(playerState.Hand); // 手牌保持为空
            Assert.Empty(playerState.Field);

            // Verify Mock Interactions (如果卡牌不在手中，则不应调用卡牌存储库)
            mockCardRepository.Verify(repo => repo.GetCardDefinitionByIdAsync(It.IsAny<int>()), Times.Never); // Changed It.IsAny<string>() to It.IsAny<int>()
        }
        [Fact]
        public async Task PlayCardAsync_CardDefinitionNotFound()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _, _) = SetupMocksAndService();
            string gameId = "testGameDefNotFound";
            string playerId = "playerDefNotFound";
            string opponentId = "opponentDefNotFound";
            int cardId = 999; // 一个定义不存在的卡牌 ID (changed to int)
            string cardUid = Guid.NewGuid().ToString(); // 手牌中卡牌实例的 UID

            // Mock 卡牌存储库，使其对这个 CardId 返回 null
            mockCardRepository.Setup(repo => repo.GetCardDefinitionByIdAsync(cardId)) // Use int ID directly
                              .ReturnsAsync((CardDefinition?)null);

            // 初始游戏状态，手牌中有卡牌，但其定义在存储库中缺失
            var initialHand = new List<CardInstanceDto> { new CardInstanceDto { InstanceId = Guid.Parse(cardUid), CardId = cardId, Name = "Unknown Card", Cost = 1, Type = CardType.Follower.ToString() } }; // Changed to CardInstanceDto, using int cardId
            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                CurrentPP = 5, // 足够的 PP
                MaxPP = 5,
                Field = new List<CardInstanceDto>(), // Fix: Change List Type
                Deck = new List<CardInstanceDto>(),
                DeckCount = 0,
                Graveyard = new List<CardInstanceDto>(),
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = playerId // Set controller ID using the variable from the test scope
                },
                IsAI = false
            };
            var opponentState = new PlayerStateDto
            {
                PlayerId = opponentId,
                CurrentPP = 5,
                MaxPP = 5,
                Hand = new(),
                Field = new(),
                Deck = new(),
                DeckCount = 0,
                Graveyard = new(),
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = opponentId // Set controller ID using the variable from the test scope
                },
                IsAI = true
            };
            var initialGameState = new GameStateDto
            {
                GameId = gameId,
                CurrentPlayerId = playerId, // 玩家的回合
                Phase = GamePhase.Main,     // 正确的阶段
                Players = new List<PlayerStateDto> { playerState, opponentState },
                FirstPlayerId = playerId,
                // TurnNumber removed from GameStateDto init
                IsGameOver = false
            };

            ConcurrentDictionary<string, GameStateDto>? gameStates = GetInternalGameStates(gameService); // Make nullable
            ConcurrentDictionary<string, object>? gameLocks = GetInternalGameLocks(gameService); // Make nullable
            Assert.NotNull(gameStates); Assert.NotNull(gameLocks);
            gameStates![gameId] = initialGameState; // Use null-forgiving operator
            gameLocks![gameId] = new object(); // Use null-forgiving operator

            var actionDto = new PlayCardActionDto { CardUid = cardUid };

            // Act & Assert Exception
            var context = new GameActionContext(); // M8 Test Fix
            var exception = await Assert.ThrowsAsync<GameLogicException>(() =>
                gameService.PlayCardAsync(gameId, playerId, actionDto, context) // M8 Test Fix: Pass context
            );

            // Assert Correct Exception Message
            Assert.Equal($"Card definition not found for CardId: {cardId}", exception.Message); // Keep using int cardId in message check

            // Assert State Unchanged
            Assert.Equal(5, playerState.CurrentPP);
            Assert.Single(playerState.Hand); // 卡牌应留在手中
            Assert.Empty(playerState.Field);

            // Verify Mock Interactions (存储库应该被调用了一次)
            mockCardRepository.Verify(repo => repo.GetCardDefinitionByIdAsync(cardId), Times.Once); // Use int cardId directly for Verify
        }
        [Fact]
        public async Task PlayCardAsync_PlaySpell_Success()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _, _) = SetupMocksAndService();
            string gameId = "testGameSpellSuccess";
            string playerId = "playerSpellSuccess";
            string opponentId = "opponentSpellSuccess";
            int spellCardId = 1; // e.g., Insight // Changed type to int, assuming S001 maps to 1
            string spellCardUid = Guid.NewGuid().ToString();
            int spellCost = 1;

            // 1. Mock Card Definition
            var spellDefinition = new CardDefinition
            {
                Id = spellCardId, // Uses int spellCardId now
                Name = "Insight",
                Cost = spellCost,
                Type = CardType.Spell.ToString(),
                IsCollectible = true
                // Add TargetType, Effects etc. if GameService logic uses them for validation
            };
            // Fix: Setup mock with the integer CardId (1)
            mockCardRepository.Setup(repo => repo.GetCardDefinitionByIdAsync(1)) // Changed "1" to 1
                             .ReturnsAsync(spellDefinition);

            // 2. Create Initial Game State
            // Assuming S001 maps to int ID 1
            var spellCardInstance = new CardInstanceDto { InstanceId = Guid.Parse(spellCardUid), CardId = spellCardId, Name = "Insight", Cost = spellCost, Type = CardType.Spell.ToString() }; // Use int spellCardId
            var initialHand = new List<CardInstanceDto> { spellCardInstance }; // Changed to CardInstanceDto
            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                Deck = new List<CardInstanceDto>(),
                DeckCount = 0,
                Field = new List<CardInstanceDto>(), // Fix: Change List Type
                Graveyard = new List<CardInstanceDto>(), // Graveyard is initially empty
                CurrentPP = 5, // Enough PP
                MaxPP = 5,
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = playerId // Set controller ID using the variable from the test scope
                },
                CurrentEP = 0,
                MaxEP = 2,
                CanEvolve = false,
                IsFirst = true,
                HasCompletedMulligan = true,
                PlayedCardCountThisTurn = 0,
                HasTakenFirstTurn = true,
                IsAI = false
            };
            var opponentState = new PlayerStateDto
            {
                PlayerId = opponentId,
                Hand = new List<CardInstanceDto>(),
                Deck = new List<CardInstanceDto>(),
                DeckCount = 0,
                Field = new List<CardInstanceDto>(), // Fix: Change List Type
                Graveyard = new List<CardInstanceDto>(),
                CurrentPP = 5,
                MaxPP = 5,
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = opponentId // Set controller ID using the variable from the test scope
                },
                CurrentEP = 0,
                MaxEP = 3,
                CanEvolve = false,
                IsFirst = false,
                HasCompletedMulligan = true,
                PlayedCardCountThisTurn = 0,
                HasTakenFirstTurn = true,
                IsAI = true
            };
            var initialGameState = new GameStateDto
            {
                GameId = gameId,
                CurrentPlayerId = playerId, // Player's turn
                FirstPlayerId = playerId,
                // TurnNumber removed from GameStateDto init
                Phase = GamePhase.Main, // Correct phase
                Players = new List<PlayerStateDto> { playerState, opponentState },
                IsGameOver = false
            };

            // 3. Add initial state to GameService
            ConcurrentDictionary<string, GameStateDto>? gameStates = GetInternalGameStates(gameService); // Make nullable
            ConcurrentDictionary<string, object>? gameLocks = GetInternalGameLocks(gameService); // Make nullable
            Assert.NotNull(gameStates); Assert.NotNull(gameLocks);
            gameStates![gameId] = initialGameState; // Use null-forgiving operator
            gameLocks![gameId] = new object(); // Use null-forgiving operator

            // 4. Create Action DTO
            var actionDto = new PlayCardActionDto
            {
                CardUid = spellCardUid,
                TargetUid = null // No target for a simple spell like Insight
            };

            // Act
            var context = new GameActionContext(); // M8 Test Fix
            await gameService.PlayCardAsync(gameId, playerId, actionDto, context); // M8 Test Fix: Pass context, removed result assignment

            // Assert
            // M8 Test Fix: Assertions below need to be updated to check context.Events instead of context.Events
            // 1. Check Player State Changes
            Assert.Equal(4, playerState.CurrentPP); // PP reduced by spell cost (1)
            Assert.Empty(playerState.Hand);         // Spell removed from hand
            Assert.Empty(playerState.Field);        // Field remains empty (spell doesn't add unit)
            Assert.Single(playerState.Graveyard);   // Spell added to graveyard
            Assert.Equal(1, playerState.Graveyard.First().CardId); // Compare with expected int ID (1 for S001)
            Assert.Equal(1, playerState.PlayedCardCountThisTurn);

            // 2. Check Returned Events
            Assert.NotNull(context.Events);
            // Expecting PPUpdate, CardPlayed, GraveyardUpdate (SpellPlayedEventDto is deprecated)
            Assert.Equal(3, context.Events.Count); // Updated expectation based on unified CardPlayedEventDto

            // Check PPUpdateEvent
            PpUpdateEventDto? ppEvent = context.Events.OfType<PpUpdateEventDto>().FirstOrDefault();
            Assert.NotNull(ppEvent);
            Assert.Equal(playerId, ppEvent.SourcePlayerId);
            Assert.Equal(4, ppEvent.CurrentPp);
            Assert.Equal(5, ppEvent.MaxPp);

            // Check CardPlayedEvent (unified for all card types)
            CardPlayedEventDto? cardPlayedEvent = context.Events.OfType<CardPlayedEventDto>().FirstOrDefault();
            Assert.NotNull(cardPlayedEvent);
            Assert.Equal(playerId, cardPlayedEvent.SourcePlayerId);
            Assert.Equal(spellCardUid, cardPlayedEvent.PlayedCardInstance.InstanceId.ToString());
            Assert.Equal(spellCardId, cardPlayedEvent.PlayedCardInstance.CardId);

            // Check GraveyardUpdateEvent
            GraveyardUpdateEventDto? graveyardEvent = context.Events.OfType<GraveyardUpdateEventDto>().FirstOrDefault();
            Assert.NotNull(graveyardEvent);
            Assert.Equal(playerId, graveyardEvent.SourcePlayerId);
            Assert.Equal(1, graveyardEvent.GraveyardCount); // Should be 1 after adding the spell

            // TODO: Assert GraveyardUpdateEventDto once GameService emits it for spells.
            // GraveyardUpdateEventDto? graveyardEvent = resultEvents.OfType<GraveyardUpdateEventDto>().FirstOrDefault();
            // Assert.NotNull(graveyardEvent);
            // Assert.Equal(playerId, graveyardEvent.SourcePlayerId);
            // Assert.Equal(1, graveyardEvent.GraveyardCount);
            // Assert.NotNull(graveyardEvent.CardAdded);
            // Assert.Equal(spellCardId, graveyardEvent.CardAdded?.CardId);
            // Assert.Equal(spellCardUid, graveyardEvent.CardAdded?.Uid);

            // 3. Verify Mock Interactions
            mockCardRepository.Verify(repo => repo.GetCardDefinitionByIdAsync(1), Times.Once); // Verify with the correct int ID 1
        }
        [Fact]
        public async Task PlayCardAsync_PlaySpell_WithTarget()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _, _) = SetupMocksAndService();
            string gameId = "testGameSpellTarget";
            string playerId = "playerSpellTarget";
            string opponentId = "opponentSpellTarget";
            int spellCardId = 2; // e.g., Angelic Snipe // Changed type to int, assuming S002 maps to 2
            string spellCardUid = Guid.NewGuid().ToString();
            int spellCost = 1;
            int targetUnitId = 1; // Target is a Goblin // Changed type to int, assuming F001 maps to 1
            Guid targetUnitUid = Guid.NewGuid(); // UID of the target unit on field (Corrected type to Guid)

            // 1. Mock Card Definitions
            var spellDefinition = new CardDefinition
            {
                Id = spellCardId, // Uses int spellCardId now
                Name = "Angelic Snipe",
                Cost = spellCost,
                Type = CardType.Spell.ToString(),
                IsCollectible = true,
                Effects = new List<CardEffect> { /* Define effect if needed by service logic */ }
                // TargetType removed - Assuming not needed or defined elsewhere
            };
            // Fix: Setup mock with the integer CardId (2)
            mockCardRepository.Setup(repo => repo.GetCardDefinitionByIdAsync(2)) // Changed "2" to 2
                             .ReturnsAsync(spellDefinition);

            // 2. Create Initial Game State
            // Assuming S002 maps to int ID 2
            var spellCardInstance = new CardInstanceDto { InstanceId = Guid.Parse(spellCardUid), CardId = spellCardId, Name = "Angelic Snipe", Cost = spellCost, Type = CardType.Spell.ToString() }; // Use int spellCardId
            var initialHand = new List<CardInstanceDto> { spellCardInstance }; // Changed to CardInstanceDto
            // Fix: Initialize as CardInstanceDto instead of UnitStateDto
            var targetUnit = new CardInstanceDto
            {
                InstanceId = targetUnitUid, // Fix: Use InstanceId
                CardId = targetUnitId, // Fix: Use int targetUnitId
                Name = "Goblin",
                CurrentAttack = 1,
                CurrentHealth = 2, // Target has 2 health initially
                MaxHealth = 2,
                CanAttack = false,
                IsEvolved = false,
                Cost = 1, // Add required properties
                Type = CardType.Follower.ToString() // Add required properties
            };

            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                Deck = new(),
                DeckCount = 0,
                Field = new(),
                Graveyard = new(),
                CurrentPP = 5,
                MaxPP = 5,
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = playerId // Set controller ID using the variable from the test scope
                },
                CurrentEP = 0,
                MaxEP = 2,
                IsFirst = true,
                HasCompletedMulligan = true,
                PlayedCardCountThisTurn = 0,
                HasTakenFirstTurn = true,
                IsAI = false
            };
            var opponentState = new PlayerStateDto
            {
                PlayerId = opponentId,
                Hand = new(),
                Deck = new(),
                DeckCount = 0,
                Field = new List<CardInstanceDto> { targetUnit }, // Fix: Change List Type // Opponent has the target unit
                Graveyard = new(),
                CurrentPP = 5,
                MaxPP = 5,
                LeaderCard = new CardInstanceDto
                {
                    InstanceId = Guid.NewGuid(), // Generate a unique ID for the leader instance
                    CardId = 0, // Placeholder ID for leader definition
                    Name = "Leader",
                    Type = "Leader", // Assuming "Leader" type exists or is appropriate
                    Cost = 0,
                    Attack = 0,
                    Health = 20, // Base health
                    CurrentAttack = 0,
                    CurrentHealth = 20, // Current health
                    MaxHealth = 20,     // Max health
                    Zone = CardZone.Leader,
                    ControllerPlayerId = opponentId // Set controller ID using the variable from the test scope
                },
                CurrentEP = 0,
                MaxEP = 3,
                IsFirst = false,
                HasCompletedMulligan = true,
                PlayedCardCountThisTurn = 0,
                HasTakenFirstTurn = true,
                IsAI = true
            };
            var initialGameState = new GameStateDto
            {
                GameId = gameId,
                CurrentPlayerId = playerId,
                FirstPlayerId = playerId,
                // TurnNumber removed from GameStateDto init
                Phase = GamePhase.Main,
                Players = new List<PlayerStateDto> { playerState, opponentState },
                IsGameOver = false
            };

            // 3. Add initial state to GameService
            ConcurrentDictionary<string, GameStateDto>? gameStates = GetInternalGameStates(gameService); // Make nullable
            ConcurrentDictionary<string, object>? gameLocks = GetInternalGameLocks(gameService); // Make nullable
            Assert.NotNull(gameStates); Assert.NotNull(gameLocks);
            gameStates![gameId] = initialGameState; // Use null-forgiving operator
            gameLocks![gameId] = new object(); // Use null-forgiving operator

            // 4. Create Action DTO with Target
            var actionDto = new PlayCardActionDto
            {
                CardUid = spellCardUid,
                TargetUid = targetUnitUid.ToString() // Specify the target (Corrected: Convert Guid to string)
            };

            // TODO: Add Act and Assert steps for this test.
            // Adding this to satisfy CS1998 for now.
            await Task.CompletedTask;
        } // Closing brace for PlayCardAsync_PlaySpell_WithTarget
          // Remaining TODOs:
          // - PlayCardAsync_PlayAmulet_Success (Simplified V0.1)
          // - PlayCardAsync_Targeting_InvalidTarget (e.g., spell needs enemy, targets friendly)
          // - PlayCardAsync_Targeting_NoValidTarget (e.g., spell needs enemy, none exist)
          // - PlayCardAsync_PlayCard_DuringOpponentTurn (Redundant if WrongTurn exists?)
          // - PlayCardAsync_PlayCard_DuringWrongPhase (Redundant if WrongPhase exists?)
          // - PlayCardAsync_EffectResolution (More complex spells/followers later)
          // - PlayCardAsync_Concurrency test (if needed)
    } // Closing brace for class GameServiceTests
} // Closing brace for namespace ShadowverseBeyond.Server.Tests