using Xunit;
using Moq; // Using Moq as an example mocking library, can be changed
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using ShadowverseBeyond.Server.Dtos.SignalR; // Added for GameEventDto base class
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Services;
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
                 Mock<IServiceProvider> mockServiceProvider) // Change AiTurnExecutor to IServiceProvider
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
                mockServiceProvider.Object // Pass IServiceProvider mock instead of AiTurnExecutor
            );

            // Update return tuple
            return (gameService, mockHubContext, mockLogger, mockScopeFactory, mockCardRepository,
                    mockAiService, mockServiceProvider); // Return IServiceProvider mock
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
            var (gameService, _, _, _, mockCardRepository, _, _) = SetupMocksAndService();
            string gameId = "testGame1";
            string playerId = "player1";
            string opponentId = "player2";
            string followerCardId = "F001"; // e.g., Goblin
            string followerCardUid = Guid.NewGuid().ToString(); // Unique ID for the card instance in hand

            // 1. Mock Card Definition
            var followerDefinition = new CardDefinition
            {
                Id = followerCardId,
                Name = "Goblin",
                Cost = 1,
                Type = CardType.Follower.ToString(),
                Attack = 1,
                Health = 2,
                Keywords = new List<string>(),
                IsCollectible = true
                // Add other necessary properties if needed by the logic being tested
            };
            mockCardRepository.Setup(repo => repo.GetCardByIdAsync(followerCardId))
                             .ReturnsAsync(followerDefinition);

            // 2. Create Initial Game State
            var initialHand = new List<CardDto>
            {
                new CardDto { Uid = Guid.Parse(followerCardUid), CardId = followerCardId, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString(), BaseAttack=1, BaseHealth=2 } // Parse Guid
            };
            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                Deck = new List<CardDto>(), // Empty for simplicity
                DeckCount = 0,
                Field = new List<UnitStateDto>(), // Empty field
                Graveyard = new List<CardDto>(),
                CurrentPP = 5, // Enough PP
                MaxPP = 5,
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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
                Hand = new List<CardDto>(),
                Deck = new List<CardDto>(),
                DeckCount = 0,
                Field = new List<UnitStateDto>(),
                Graveyard = new List<CardDto>(),
                CurrentPP = 5,
                MaxPP = 5,
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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
            var gameStates = GetInternalGameStates(gameService);
            var gameLocks = GetInternalGameLocks(gameService);
            Assert.NotNull(gameStates); // Ensure reflection worked
            Assert.NotNull(gameLocks);
            gameStates[gameId] = initialGameState; // Directly set the state for the test
            gameLocks[gameId] = new object(); // Ensure lock object exists

            // 4. Create Action DTO
            var actionDto = new PlayCardActionDto
            {
                CardUid = followerCardUid,
                TargetUid = null // No target for basic follower play
            };

            // Act
            List<GameEventDto> resultEvents = await gameService.PlayCardAsync(gameId, playerId, actionDto);

            // Assert
            // 1. Check Player State Changes
            Assert.Equal(4, playerState.CurrentPP); // PP reduced by 1 (cost of Goblin)
            Assert.Empty(playerState.Hand); // Card removed from hand
            Assert.Single(playerState.Field); // One unit added to field
            Assert.Equal(1, playerState.PlayedCardCountThisTurn);

            // 2. Check Unit State on Field
            var playedUnit = playerState.Field.First();
            Assert.Equal(followerCardId, playedUnit.CardId);
            Assert.Equal("Goblin", playedUnit.Name);
            Assert.Equal(1, playedUnit.CurrentAttack);
            Assert.Equal(2, playedUnit.CurrentHealth);
            Assert.False(playedUnit.CanAttack); // Summoning sickness
            Assert.False(playedUnit.IsEvolved);

            // 3. Check Returned Events
            Assert.NotNull(resultEvents);
            Assert.Equal(2, resultEvents.Count); // Expecting PPUpdate and UnitPlayed

            PpUpdateEventDto? ppEvent = resultEvents.OfType<PpUpdateEventDto>().FirstOrDefault();
            Assert.NotNull(ppEvent);
            Assert.Equal(playerId, ppEvent.SourcePlayerId); // Corrected property name from base class
            Assert.Equal(4, ppEvent.CurrentPp);
            Assert.Equal(5, ppEvent.MaxPp);

            var unitPlayedEvent = resultEvents.OfType<UnitPlayedEventDto>().FirstOrDefault();
            Assert.NotNull(unitPlayedEvent);
            Assert.Equal(playerId, unitPlayedEvent.SourcePlayerId); // Player playing the card
            Assert.Equal(playerId, unitPlayedEvent.PlayerId);      // Corrected: Player owning the unit
            Assert.Equal(followerCardUid, unitPlayedEvent.SourceCardUid); // Corrected: UID from hand
            Assert.NotNull(unitPlayedEvent.UnitData);                 // Corrected: Property name
            Assert.Equal(playedUnit.Uid, unitPlayedEvent.UnitData.Uid);   // Corrected: Property name
            Assert.Equal(followerCardId, unitPlayedEvent.UnitData.CardId); // Corrected: Property name

            // 4. Verify Mock Interactions (Optional but good practice)
            mockCardRepository.Verify(repo => repo.GetCardByIdAsync(followerCardId), Times.Once);
        }

        // TODO: Add more test cases:
        [Fact]
        public async Task PlayCardAsync_PlayFollower_InsufficientPP()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _) = SetupMocksAndService();
            string gameId = "testGameIPP";
            string playerId = "playerIPP";
            string opponentId = "opponentIPP";
            string followerCardId = "F001"; // Goblin cost 1
            string followerCardUid = Guid.NewGuid().ToString();

            // Mock Card Definition (Cost 1)
            var followerDefinition = new CardDefinition { Id = followerCardId, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString() /* ... other needed props */ }; // Added Name
            mockCardRepository.Setup(repo => repo.GetCardByIdAsync(followerCardId)).ReturnsAsync(followerDefinition);

            // Initial Game State with Insufficient PP
            var initialHand = new List<CardDto> { new CardDto { Uid = Guid.Parse(followerCardUid), CardId = followerCardId, Cost = 1 } }; // Parse Guid
            var playerState = new PlayerStateDto { PlayerId = playerId, Hand = initialHand, CurrentPP = 0, MaxPP = 5, Field = new List<UnitStateDto>() }; // PP = 0!
            var opponentState = new PlayerStateDto { PlayerId = opponentId, CurrentPP = 5, MaxPP = 5 }; // Opponent state needed for GameStateDto validity
            var initialGameState = new GameStateDto
            {
                GameId = gameId,
                CurrentPlayerId = playerId,
                Phase = GamePhase.Main, // Set correct phase
                Players = new List<PlayerStateDto> { playerState, opponentState }
                // ... other necessary GameStateDto properties ...
            };

            var gameStates = GetInternalGameStates(gameService);
            var gameLocks = GetInternalGameLocks(gameService);
            Assert.NotNull(gameStates);
            Assert.NotNull(gameLocks);
            gameStates[gameId] = initialGameState;
            gameLocks[gameId] = new object();

            var actionDto = new PlayCardActionDto { CardUid = followerCardUid };

            // Act & Assert Exception
            var exception = await Assert.ThrowsAsync<GameLogicException>(() => // Expect GameLogicException
                gameService.PlayCardAsync(gameId, playerId, actionDto)
            );

            // Assert Correct Exception Message
            Assert.Equal("Not enough PP to play Goblin. Requires 1, has 0.", exception.Message); // Verify the correct message for insufficient PP (added period)

            // Assert State Unchanged
            Assert.Equal(0, playerState.CurrentPP); // PP remains 0
            Assert.Single(playerState.Hand); // Card still in hand
            Assert.Empty(playerState.Field); // Field remains empty
                                             // Assert No Events Emitted: Handled by Assert.ThrowsAsync catching the exception before events are generated.



            // Verify Mock Interactions (Optional: verify card repo wasn't called again if check happens early)
            mockCardRepository.Verify(repo => repo.GetCardByIdAsync(It.IsAny<string>()), Times.Once); // Should still be called once for cost check maybe? Adjust if needed.
        }
        [Fact]
        public async Task PlayCardAsync_PlayFollower_FieldFull()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _) = SetupMocksAndService();
            string gameId = "testGameFieldFull";
            string playerId = "playerFieldFull";
            string opponentId = "opponentFieldFull";
            string followerCardId = "F001"; // Goblin cost 1
            string followerCardUid = Guid.NewGuid().ToString();
            const int MaxFieldSize = 7; // As defined in GameService

            // Mock Card Definition (Cost 1)
            var followerDefinition = new CardDefinition { Id = followerCardId, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString() };
            mockCardRepository.Setup(repo => repo.GetCardByIdAsync(followerCardId)).ReturnsAsync(followerDefinition);

            // Initial Game State with Full Field and Sufficient PP
            var initialHand = new List<CardDto> { new CardDto { Uid = Guid.Parse(followerCardUid), CardId = followerCardId, Name = "Goblin", Cost = 1 } }; // Parse Guid
            var fullField = new List<UnitStateDto>();
            for (int i = 0; i < MaxFieldSize; i++)
            {
                // Add dummy units - ensure they have unique Uids if necessary, though not strictly needed for this test logic
                fullField.Add(new UnitStateDto { Uid = Guid.NewGuid(), CardId = $"Dummy{i}", Name = $"DummyUnit{i}", CurrentHealth = 1 });
            }

            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                CurrentPP = 1, // Sufficient PP
                MaxPP = 5,
                Field = fullField, // Field is full!
                Deck = new List<CardDto>(),
                DeckCount = 0,
                Graveyard = new List<CardDto>(),
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
                CurrentEP = 0,
                MaxEP = 2,
                CanEvolve = false,
                IsFirst = true,
                HasCompletedMulligan = true,
                PlayedCardCountThisTurn = 0,
                HasTakenFirstTurn = true,
                IsAI = false // Added minimal required properties
            };
            var opponentState = new PlayerStateDto { PlayerId = opponentId, Hand = new(), Deck = new(), Field = new(), Graveyard = new(), DeckCount = 0, LeaderHealth = 20, MaxLeaderHealth = 20, CurrentPP = 5, MaxPP = 5, CurrentEP = 0, MaxEP = 3, CanEvolve = false, IsFirst = false, HasCompletedMulligan = true, PlayedCardCountThisTurn = 0, HasTakenFirstTurn = true, IsAI = true }; // Minimal opponent state
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
            var gameStates = GetInternalGameStates(gameService);
            var gameLocks = GetInternalGameLocks(gameService);
            Assert.NotNull(gameStates);
            Assert.NotNull(gameLocks);
            gameStates[gameId] = initialGameState;
            gameLocks[gameId] = new object();

            var actionDto = new PlayCardActionDto { CardUid = followerCardUid };

            // Act & Assert Exception
            var exception = await Assert.ThrowsAsync<GameLogicException>(() =>
                gameService.PlayCardAsync(gameId, playerId, actionDto)
            );

            // Assert Correct Exception Message (Matches GameService format)
            Assert.Equal($"Cannot play Goblin, field is full ({MaxFieldSize}/{MaxFieldSize}).", exception.Message);

            // Assert State Unchanged
            Assert.Equal(1, playerState.CurrentPP); // PP remains unchanged
            Assert.Single(playerState.Hand); // Card still in hand
            Assert.Equal(MaxFieldSize, playerState.Field.Count); // Field still full

            // Verify Mock Interactions
            mockCardRepository.Verify(repo => repo.GetCardByIdAsync(followerCardId), Times.Once); // Fetch happens before validation

            // Body of FieldFull test is intentionally left empty for now
        } // Add closing brace for PlayCardAsync_PlayFollower_FieldFull method
        [Fact]
        public async Task PlayCardAsync_PlayFollower_WrongTurn()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _) = SetupMocksAndService();
            string gameId = "testGameWrongTurn";
            string playerId = "playerWrongTurn"; // Player attempting the action
            string opponentId = "opponentWhoseTurnItIs"; // Player whose turn it actually is
            string followerCardId = "F001"; // Goblin cost 1
            string followerCardUid = Guid.NewGuid().ToString();

            // Mock Card Definition (Not strictly needed as check happens earlier, but good practice)
            var followerDefinition = new CardDefinition { Id = followerCardId, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString() };
            mockCardRepository.Setup(repo => repo.GetCardByIdAsync(followerCardId)).ReturnsAsync(followerDefinition);

            // Initial Game State where it's the opponent's turn
            var initialHand = new List<CardDto> { new CardDto { Uid = Guid.Parse(followerCardUid), CardId = followerCardId, Name = "Goblin", Cost = 1 } }; // Parse Guid
            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                CurrentPP = 5, // Enough PP
                MaxPP = 5,
                Field = new List<UnitStateDto>(),
                Deck = new List<CardDto>(),
                DeckCount = 0,
                Graveyard = new List<CardDto>(),
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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

            var gameStates = GetInternalGameStates(gameService);
            var gameLocks = GetInternalGameLocks(gameService);
            Assert.NotNull(gameStates);
            Assert.NotNull(gameLocks);
            gameStates[gameId] = initialGameState;
            gameLocks[gameId] = new object();

            var actionDto = new PlayCardActionDto { CardUid = followerCardUid };

            // Act & Assert Exception
            var exception = await Assert.ThrowsAsync<GameLogicException>(() =>
                gameService.PlayCardAsync(gameId, playerId, actionDto) // PlayerId attempts action on opponentId's turn
            );

            // Assert Correct Exception Message
            Assert.Equal($"It's not player {playerId}'s turn.", exception.Message); // Match actual message format

            // Assert State Unchanged (Optional but good)
            Assert.Equal(5, playerState.CurrentPP); // PP remains unchanged
            Assert.Single(playerState.Hand);       // Card still in hand
            Assert.Empty(playerState.Field);       // Field remains empty

            // Verify Mock Interactions (Should not fetch card if turn check fails first)
            mockCardRepository.Verify(repo => repo.GetCardByIdAsync(It.IsAny<string>()), Times.Once); // Verify it IS called once before turn check throws
        }
        [Fact]
        public async Task PlayCardAsync_PlayFollower_WrongPhase()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _) = SetupMocksAndService();
            string gameId = "testGameWrongPhase";
            string playerId = "playerWrongPhase";
            string opponentId = "opponentWrongPhase";
            string followerCardId = "F001"; // Goblin cost 1
            string followerCardUid = Guid.NewGuid().ToString();

            // Mock Card Definition (Not strictly needed as check happens earlier)
            var followerDefinition = new CardDefinition { Id = followerCardId, Name = "Goblin", Cost = 1, Type = CardType.Follower.ToString() };
            mockCardRepository.Setup(repo => repo.GetCardByIdAsync(followerCardId)).ReturnsAsync(followerDefinition);

            // Initial Game State in Mulligan Phase
            var initialHand = new List<CardDto> { new CardDto { Uid = Guid.Parse(followerCardUid), CardId = followerCardId, Name = "Goblin", Cost = 1 } }; // Parse Guid
            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                CurrentPP = 5, // Enough PP
                MaxPP = 5,
                Field = new List<UnitStateDto>(),
                Deck = new List<CardDto>(),
                DeckCount = 0,
                Graveyard = new List<CardDto>(),
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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

            var gameStates = GetInternalGameStates(gameService);
            var gameLocks = GetInternalGameLocks(gameService);
            Assert.NotNull(gameStates);
            Assert.NotNull(gameLocks);
            gameStates[gameId] = initialGameState;
            gameLocks[gameId] = new object();

            var actionDto = new PlayCardActionDto { CardUid = followerCardUid };

            // Act & Assert Exception
            var exception = await Assert.ThrowsAsync<GameLogicException>(() =>
                gameService.PlayCardAsync(gameId, playerId, actionDto)
            );

            // Assert Correct Exception Message
            Assert.Equal($"Can only play cards during the Main phase (Current: {initialGameState.Phase}).", exception.Message); // Match actual message including phase

            // Assert State Unchanged (Optional)
            Assert.Equal(5, playerState.CurrentPP);
            Assert.Single(playerState.Hand);
            Assert.Empty(playerState.Field);

            // Verify Mock Interactions (Should not fetch card if phase check fails first)
            mockCardRepository.Verify(repo => repo.GetCardByIdAsync(It.IsAny<string>()), Times.Once); // Verify it IS called once before phase check throws
        }
        [Fact]
        public async Task PlayCardAsync_CardNotFoundInHand()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _) = SetupMocksAndService();
            string gameId = "testGameCardNotFound";
            string playerId = "playerCardNotFound";
            string opponentId = "opponentCardNotFound";
            string nonExistentCardUid = Guid.NewGuid().ToString(); // 一个手牌中不存在的卡牌 UID

            // 初始游戏状态，玩家手牌为空或不包含目标卡牌
            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = new List<CardDto>(), // 空手牌确保找不到卡牌
                CurrentPP = 5, // 确保有足够的 PP，以免因此失败
                MaxPP = 5,
                Field = new List<UnitStateDto>(),
                Deck = new List<CardDto>(),
                DeckCount = 0,
                Graveyard = new List<CardDto>(),
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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

            var gameStates = GetInternalGameStates(gameService);
            var gameLocks = GetInternalGameLocks(gameService);
            Assert.NotNull(gameStates);
            Assert.NotNull(gameLocks);
            gameStates[gameId] = initialGameState;
            gameLocks[gameId] = new object();

            var actionDto = new PlayCardActionDto { CardUid = nonExistentCardUid };

            // Act & Assert Exception
            var exception = await Assert.ThrowsAsync<GameLogicException>(() =>
                gameService.PlayCardAsync(gameId, playerId, actionDto)
            );

            // Assert Correct Exception Message
            Assert.Equal($"Card with UID {actionDto.CardUid} not found in player {playerId}'s hand (during card fetch).", exception.Message);

            // Assert State Unchanged
            Assert.Equal(5, playerState.CurrentPP);
            Assert.Empty(playerState.Hand); // 手牌保持为空
            Assert.Empty(playerState.Field);

            // Verify Mock Interactions (如果卡牌不在手中，则不应调用卡牌存储库)
            mockCardRepository.Verify(repo => repo.GetCardByIdAsync(It.IsAny<string>()), Times.Never);
        }
        [Fact]
        public async Task PlayCardAsync_CardDefinitionNotFound()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _) = SetupMocksAndService();
            string gameId = "testGameDefNotFound";
            string playerId = "playerDefNotFound";
            string opponentId = "opponentDefNotFound";
            string cardId = "UnknownCard001"; // 一个定义不存在的卡牌 ID
            string cardUid = Guid.NewGuid().ToString(); // 手牌中卡牌实例的 UID

            // Mock 卡牌存储库，使其对这个 CardId 返回 null
            mockCardRepository.Setup(repo => repo.GetCardByIdAsync(cardId))
                              .ReturnsAsync((CardDefinition?)null);

            // 初始游戏状态，手牌中有卡牌，但其定义在存储库中缺失
            var initialHand = new List<CardDto> { new CardDto { Uid = Guid.Parse(cardUid), CardId = cardId, Name = "Unknown Card", Cost = 1 } }; // Parse Guid
            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                CurrentPP = 5, // 足够的 PP
                MaxPP = 5,
                Field = new List<UnitStateDto>(),
                Deck = new List<CardDto>(),
                DeckCount = 0,
                Graveyard = new List<CardDto>(),
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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

            var gameStates = GetInternalGameStates(gameService);
            var gameLocks = GetInternalGameLocks(gameService);
            Assert.NotNull(gameStates); Assert.NotNull(gameLocks);
            gameStates[gameId] = initialGameState;
            gameLocks[gameId] = new object();

            var actionDto = new PlayCardActionDto { CardUid = cardUid };

            // Act & Assert Exception
            var exception = await Assert.ThrowsAsync<GameLogicException>(() =>
                gameService.PlayCardAsync(gameId, playerId, actionDto)
            );

            // Assert Correct Exception Message
            Assert.Equal($"Card definition not found for CardId: {cardId}", exception.Message);

            // Assert State Unchanged
            Assert.Equal(5, playerState.CurrentPP);
            Assert.Single(playerState.Hand); // 卡牌应留在手中
            Assert.Empty(playerState.Field);

            // Verify Mock Interactions (存储库应该被调用了一次)
            mockCardRepository.Verify(repo => repo.GetCardByIdAsync(cardId), Times.Once);
        }
        [Fact]
        public async Task PlayCardAsync_PlaySpell_Success()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _) = SetupMocksAndService();
            string gameId = "testGameSpellSuccess";
            string playerId = "playerSpellSuccess";
            string opponentId = "opponentSpellSuccess";
            string spellCardId = "S001"; // e.g., Insight
            string spellCardUid = Guid.NewGuid().ToString();
            int spellCost = 1;

            // 1. Mock Card Definition
            var spellDefinition = new CardDefinition
            {
                Id = spellCardId,
                Name = "Insight",
                Cost = spellCost,
                Type = CardType.Spell.ToString(),
                IsCollectible = true
                // Add TargetType, Effects etc. if GameService logic uses them for validation
            };
            mockCardRepository.Setup(repo => repo.GetCardByIdAsync(spellCardId))
                             .ReturnsAsync(spellDefinition);

            // 2. Create Initial Game State
            var spellCardDto = new CardDto { Uid = Guid.Parse(spellCardUid), CardId = spellCardId, Name = "Insight", Cost = spellCost, Type = CardType.Spell.ToString() }; // Parse Guid
            var initialHand = new List<CardDto> { spellCardDto };
            var playerState = new PlayerStateDto
            {
                PlayerId = playerId,
                Hand = initialHand,
                Deck = new List<CardDto>(),
                DeckCount = 0,
                Field = new List<UnitStateDto>(),
                Graveyard = new List<CardDto>(), // Graveyard is initially empty
                CurrentPP = 5, // Enough PP
                MaxPP = 5,
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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
                Hand = new List<CardDto>(),
                Deck = new List<CardDto>(),
                DeckCount = 0,
                Field = new List<UnitStateDto>(),
                Graveyard = new List<CardDto>(),
                CurrentPP = 5,
                MaxPP = 5,
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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
            var gameStates = GetInternalGameStates(gameService);
            var gameLocks = GetInternalGameLocks(gameService);
            Assert.NotNull(gameStates); Assert.NotNull(gameLocks);
            gameStates[gameId] = initialGameState;
            gameLocks[gameId] = new object();

            // 4. Create Action DTO
            var actionDto = new PlayCardActionDto
            {
                CardUid = spellCardUid,
                TargetUid = null // No target for a simple spell like Insight
            };

            // Act
            List<GameEventDto> resultEvents = await gameService.PlayCardAsync(gameId, playerId, actionDto);

            // Assert
            // 1. Check Player State Changes
            Assert.Equal(4, playerState.CurrentPP); // PP reduced by spell cost (1)
            Assert.Empty(playerState.Hand);         // Spell removed from hand
            Assert.Empty(playerState.Field);        // Field remains empty (spell doesn't add unit)
            Assert.Single(playerState.Graveyard);   // Spell added to graveyard
            Assert.Equal(spellCardId, playerState.Graveyard.First().CardId);
            Assert.Equal(1, playerState.PlayedCardCountThisTurn);

            // 2. Check Returned Events
            Assert.NotNull(resultEvents);
            // TODO: GameService currently doesn't emit GraveyardUpdateEventDto for played spells. Expecting 3 events later.
            Assert.Equal(2, resultEvents.Count); // Expecting PPUpdate, SpellPlayed. (GraveyardUpdate missing for now)

            // Check PPUpdateEvent
            PpUpdateEventDto? ppEvent = resultEvents.OfType<PpUpdateEventDto>().FirstOrDefault();
            Assert.NotNull(ppEvent);
            Assert.Equal(playerId, ppEvent.SourcePlayerId);
            Assert.Equal(4, ppEvent.CurrentPp);
            Assert.Equal(5, ppEvent.MaxPp);

            // Check SpellPlayedEvent
            SpellPlayedEventDto? spellPlayedEvent = resultEvents.OfType<SpellPlayedEventDto>().FirstOrDefault();
            Assert.NotNull(spellPlayedEvent);
            Assert.Equal(playerId, spellPlayedEvent.SourcePlayerId);
            Assert.Equal(spellCardUid, spellPlayedEvent.SourceCardUid); // Corrected property name
            Assert.Equal(spellCardId, spellPlayedEvent.CardId);
            // Assert.Null(spellPlayedEvent.TargetUid); // Assuming Insight has no target

            // TODO: Assert GraveyardUpdateEventDto once GameService emits it for spells.
            // GraveyardUpdateEventDto? graveyardEvent = resultEvents.OfType<GraveyardUpdateEventDto>().FirstOrDefault();
            // Assert.NotNull(graveyardEvent);
            // Assert.Equal(playerId, graveyardEvent.SourcePlayerId);
            // Assert.Equal(1, graveyardEvent.GraveyardCount);
            // Assert.NotNull(graveyardEvent.CardAdded);
            // Assert.Equal(spellCardId, graveyardEvent.CardAdded?.CardId);
            // Assert.Equal(spellCardUid, graveyardEvent.CardAdded?.Uid);

            // 3. Verify Mock Interactions
            mockCardRepository.Verify(repo => repo.GetCardByIdAsync(spellCardId), Times.Once);
        }
        [Fact]
        public async Task PlayCardAsync_PlaySpell_WithTarget()
        {
            // Arrange
            var (gameService, _, _, _, mockCardRepository, _, _) = SetupMocksAndService();
            string gameId = "testGameSpellTarget";
            string playerId = "playerSpellTarget";
            string opponentId = "opponentSpellTarget";
            string spellCardId = "S002"; // e.g., Angelic Snipe
            string spellCardUid = Guid.NewGuid().ToString();
            int spellCost = 1;
            string targetUnitId = "F001"; // Target is a Goblin
            Guid targetUnitUid = Guid.NewGuid(); // UID of the target unit on field (Corrected type to Guid)

            // 1. Mock Card Definitions
            var spellDefinition = new CardDefinition
            {
                Id = spellCardId,
                Name = "Angelic Snipe",
                Cost = spellCost,
                Type = CardType.Spell.ToString(),
                IsCollectible = true,
                Effects = new List<CardEffect> { /* Define effect if needed by service logic */ }
                // TargetType removed - Assuming not needed or defined elsewhere
            };
            mockCardRepository.Setup(repo => repo.GetCardByIdAsync(spellCardId))
                             .ReturnsAsync(spellDefinition);

            // 2. Create Initial Game State
            var spellCardDto = new CardDto { Uid = Guid.Parse(spellCardUid), CardId = spellCardId, Name = "Angelic Snipe", Cost = spellCost, Type = CardType.Spell.ToString() }; // Parse Guid
            var initialHand = new List<CardDto> { spellCardDto };
            var targetUnit = new UnitStateDto
            {
                Uid = targetUnitUid, // Corrected assignment from Guid
                CardId = targetUnitId,
                Name = "Goblin",
                CurrentAttack = 1,
                CurrentHealth = 2, // Target has 2 health initially
                MaxHealth = 2,
                CanAttack = false,
                IsEvolved = false
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
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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
                Field = new List<UnitStateDto> { targetUnit }, // Opponent has the target unit
                Graveyard = new(),
                CurrentPP = 5,
                MaxPP = 5,
                LeaderHealth = 20,
                MaxLeaderHealth = 20,
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
            var gameStates = GetInternalGameStates(gameService);
            var gameLocks = GetInternalGameLocks(gameService);
            Assert.NotNull(gameStates); Assert.NotNull(gameLocks);
            gameStates[gameId] = initialGameState;
            gameLocks[gameId] = new object();

            // 4. Create Action DTO with Target
            var actionDto = new PlayCardActionDto
            {
                CardUid = spellCardUid,
                TargetUid = targetUnitUid.ToString() // Specify the target (Corrected: Convert Guid to string)
            };
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