// File: Server/Domain/Entities/GameState.cs
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos; // Added for DTO types
using ShadowverseBeyond.Server.Dtos.Enums; // For GameStatus, GamePhase
using System.Collections.Generic;
using System.Linq; // Added for FirstOrDefault

namespace ShadowverseBeyond.Server.Domain.Entities
{
    // Note: PlayerState and UnitState are now defined in separate files within this namespace.
    // Removed the placeholder records for PlayerStatePlaceholder and UnitStatePlaceholder.


    /// <summary>
    /// Represents the complete state of a single game instance.
    /// This is the core domain entity used for game logic processing.
    /// </summary>
    public class GameState
    {
        public string GameId { get; set; }
        public GameStatus Status { get; set; } = GameStatus.Starting;
        public GamePhase Phase { get; set; } = GamePhase.Mulligan;

        // TODO: 移除全局回合数，使用PlayerState.TurnNumber
        public int TurnNumber { get; set; } = 0; // Global turn counter (consider if needed vs player-specific turns)
        public Guid? CurrentPlayerId { get; set; } // Changed from string? to Guid?
        public Guid? FirstPlayerId { get; set; } // Changed from string? to Guid?
        public Guid? WinnerId { get; set; } // Changed from string? to Guid?
        public string? GameMessage { get; set; } // Added

        // Keep direct references to players for easier access in game logic
        public PlayerState Player1 { get; set; }
        public PlayerState Player2 { get; set; }

        // Derived property for convenience, especially for DTO mapping
        public bool IsGameOver => Status == GameStatus.Finished;

        /// <summary>
        /// Constructor requiring essential game setup information.
        /// </summary>
        public GameState(string gameId, PlayerState player1, PlayerState player2, Guid firstPlayerId) // Changed firstPlayerId type to Guid
        {
            GameId = gameId;
            Player1 = player1 ?? throw new ArgumentNullException(nameof(player1));
            Player2 = player2 ?? throw new ArgumentNullException(nameof(player2));
            FirstPlayerId = firstPlayerId; // Assign Guid directly
            CurrentPlayerId = firstPlayerId; // Assign Guid directly
            Status = GameStatus.Starting; // Initial status
            Phase = GamePhase.Mulligan;   // Initial phase
            TurnNumber = 0;               // Initial turn
        }

        // Parameterless constructor might be needed for ORMs or specific deserialization scenarios.
        // Ensure it initializes properties appropriately or mark required properties.
        private GameState()
        {
            GameId = string.Empty;
            // Initialize Player1 and Player2 to avoid null reference issues if this constructor is used.
            // Consider if a default/empty PlayerState is appropriate or if this constructor should be avoided.
            // Use the public constructor with new Guids for default initialization.
            Player1 = new PlayerState(Guid.NewGuid()); // Use Guid constructor
            Player2 = new PlayerState(Guid.NewGuid()); // Use Guid constructor
        }


        // Helper Methods

        /// <summary>
        /// Gets the PlayerState object for the specified player ID.
        /// </summary>
        /// <param name="playerId">The ID of the player.</param>
        /// <returns>The PlayerState if found; otherwise, null.</returns>
        public PlayerState? GetPlayerState(Guid playerId)
        {
            if (Player1?.PlayerId == playerId) return Player1;
            if (Player2?.PlayerId == playerId) return Player2;
            return null;
        }

        /// <summary>
        /// Gets the PlayerState object for the opponent of the specified player ID.
        /// </summary>
        /// <param name="playerId">The ID of the player whose opponent is requested.</param>
        /// <returns>The opponent's PlayerState if found; otherwise, null.</returns>
        public PlayerState? GetOpponentPlayerState(Guid playerId)
        {
            if (Player1?.PlayerId == playerId) return Player2;
            if (Player2?.PlayerId == playerId) return Player1;
            return null; // Or throw if playerId doesn't match either player
        }

        /// <summary>
        /// 执行回合结束操作
        /// </summary>
        /// <param name="endingPlayerId">结束回合的玩家ID</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> EndTurn(Guid endingPlayerId)
        {
            var events = new List<IGameEvent>();

            // 1. 获取当前玩家
            var currentPlayer = GetPlayerState(endingPlayerId);
            if (currentPlayer == null)
            {
                throw new ArgumentException($"Player not found: {endingPlayerId}");
            }

            // 2. 执行当前玩家的回合结束操作
            var playerEndTurnEvents = currentPlayer.EndTurn();
            events.AddRange(playerEndTurnEvents);

            // 3. 切换到下一个玩家
            var nextPlayer = GetOpponentPlayerState(endingPlayerId);
            if (nextPlayer != null)
            {
                CurrentPlayerId = nextPlayer.PlayerId;

                // 4. 增加回合数
                TurnNumber++;

                // 5. 执行下一个玩家的回合开始操作
                var nextPlayerStartTurnEvents = StartTurn(nextPlayer);
                events.AddRange(nextPlayerStartTurnEvents);
            }

            return events;
        }

        /// <summary>
        /// 执行回合开始操作
        /// </summary>
        /// <param name="startingPlayer">开始回合的玩家</param>
        /// <returns>生成的事件列表</returns>
        private List<IGameEvent> StartTurn(PlayerState startingPlayer)
        {
            var events = new List<IGameEvent>();

            // 使用PlayerState的领域方法执行回合开始
            var turnStartEvents = startingPlayer.StartTurn(TurnNumber);
            events.AddRange(turnStartEvents);

            return events;
        }

        /// <summary>
        /// 执行指定玩家的回合开始操作（公共方法）
        /// </summary>
        /// <param name="startingPlayerId">开始回合的玩家ID</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> StartPlayerTurn(Guid startingPlayerId)
        {
            var events = new List<IGameEvent>();

            // 获取开始回合的玩家
            var startingPlayer = GetPlayerState(startingPlayerId);
            if (startingPlayer == null)
            {
                throw new ArgumentException($"Player not found: {startingPlayerId}");
            }

            // 设置当前玩家
            CurrentPlayerId = startingPlayerId;

            // 执行回合开始操作
            var turnStartEvents = StartTurn(startingPlayer);
            events.AddRange(turnStartEvents);

            return events;
        }

        // Removed ToDto() method as mapping is now handled by AutoMapper (GameStateProfile)
    } // End of GameState class
} // End of namespace