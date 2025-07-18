// File: Server/Domain/Entities/GameState.cs
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos; // Added for DTO types
using ShadowverseBeyond.Server.Dtos.Enums; // For GameStatus, GamePhase
using System.Collections.Generic;
using System.Linq; // Added for FirstOrDefault
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For DomainOperationResult

using System.Collections.Concurrent; // Added for ConcurrentDictionary

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
        private readonly ConcurrentDictionary<Guid, CardInstance> _allCardInstancesById = new();

        public string GameId { get; set; }
        public GameStatus Status { get; set; } = GameStatus.Starting;
        public GamePhase Phase { get; set; } = GamePhase.Mulligan;

        // 【架构重构】已删除全局TurnNumber，使用PlayerState.TurnNumber管理每个玩家的回合数
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
            // 【架构重构】不再初始化全局TurnNumber，使用PlayerState.TurnNumber
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
        /// &lt;summary&gt;
        /// Finds a card instance by its ID within a specific zone or across player leaders.
        /// &lt;/summary&gt;
        /// &lt;param name="cardId"&gt;The instance ID of the card to find.&lt;/param&gt;
        /// &lt;param name="zone"&gt;The zone to search in. Defaults to CardZone.Field.&lt;/param&gt;
        /// &lt;returns&gt;The found CardInstance, or null if not found.&lt;/returns&gt;
        public CardInstance? FindCardInstance(Guid cardId, CardZone zone = CardZone.Field)
        {
            if (cardId == Guid.Empty) return null;

            if (zone == CardZone.Leader)
            {
                if (Player1?.Leader?.InstanceId == cardId) return Player1.Leader;
                if (Player2?.Leader?.InstanceId == cardId) return Player2.Leader;
                return null;
            }

            // Search in Player 1's specified zone
            var p1ZoneCards = Player1?.GetCardsInZone(zone);
            var card = p1ZoneCards?.Find(c => c.InstanceId == cardId);
            if (card != null) return card;

            // Search in Player 2's specified zone
            var p2ZoneCards = Player2?.GetCardsInZone(zone);
            card = p2ZoneCards?.Find(c => c.InstanceId == cardId);
            if (card != null) return card;

            // If specifically searching field and not found yet, also check leaders (as per original logic if zone was just field)
            // However, the user's request was to make it zone-specific. If they want a "field or leader" search,
            // the caller should make two calls or we need a different method/parameter.
            // For now, strictly adhering to the specified zone. If zone is Field, it won't check leaders unless explicitly asked.

            return null; // Card not found in the specified zone for either player
        }

        /// <summary>
        /// 根据实例ID高效查找游戏中的任意卡牌实例。
        /// </summary>
        public CardInstance? FindCardInstanceById(Guid instanceId)
        {
            _allCardInstancesById.TryGetValue(instanceId, out var card);
            return card;
        }

        /// <summary>
        /// 注册一个新的卡牌实例到全局查找字典中。
        /// 应在卡牌实例被创建并加入游戏（任何区域）时调用。
        /// </summary>
        public void RegisterCardInstance(CardInstance card)
        {
            if (card != null)
            {
                _allCardInstancesById[card.InstanceId] = card;
            }
        }

        /// <summary>
        /// 从全局查找字典中注销一个卡牌实例。
        /// 应在卡牌实例彻底离开游戏（例如，被放逐且无法返回，或游戏结束清理时）调用。
        /// 注意：仅仅移动到墓地的卡牌通常不应立即注销，因为它们可能仍是某些效果的目标或触发源。
        /// </summary>
        public void UnregisterCardInstance(Guid instanceId)
        {
            _allCardInstancesById.TryRemove(instanceId, out _);
        }

        public void UnregisterCardInstance(CardInstance card)
        {
            if (card != null) UnregisterCardInstance(card.InstanceId);
        }

        /// 应该移动到PlayerState中
        /// <summary>
        /// 执行回合结束操作
        /// </summary>
        /// <param name="endingPlayerId">结束回合的玩家ID</param>
        /// <returns>生成的事件列表</returns>
        public DomainOperationResult EndTurn(Guid endingPlayerId)
        {
            var result = new DomainOperationResult();

            // 1. 获取当前玩家
            var currentPlayer = GetPlayerState(endingPlayerId);
            if (currentPlayer == null)
            {
                throw new ArgumentException($"Player not found: {endingPlayerId}");
            }

            // 2. 执行当前玩家的回合结束操作
            var playerEndTurnResult = currentPlayer.EndTurn();
            result.GeneratedEvents.AddRange(playerEndTurnResult.GeneratedEvents);

            // 3. 切换到下一个玩家
            var nextPlayer = GetOpponentPlayerState(endingPlayerId);
            if (nextPlayer != null)
            {
                CurrentPlayerId = nextPlayer.PlayerId;
                nextPlayer.TurnNumber++;
            }

            return result;
        }


        /// <summary>
        /// 检查双方Mulligan是否都已完成
        /// </summary>
        /// <returns>如果双方都完成Mulligan返回true，否则返回false</returns>
        public bool AreBothMulligansCompleted()
        {
            return Phase == GamePhase.Mulligan &&
                   Player1 != null && Player1.HasCompletedMulligan &&
                   Player2 != null && Player2.HasCompletedMulligan;
        }

        /// <summary>
        /// 执行从Mulligan阶段到Main阶段的转换
        /// 【架构修正】领域实体只负责状态转换，不直接创建事件或效果请求
        /// 效果请求的创建应该由调用方（如PlayerState.PerformMulligan）处理
        /// </summary>
        /// <returns>空的事件列表，状态转换完成的标识</returns>
        public List<IGameEvent> TransitionFromMulliganToMain()
        {
            var events = new List<IGameEvent>();

            if (!AreBothMulligansCompleted())
            {
                throw new InvalidOperationException("Cannot transition to Main phase: not all players have completed mulligan");
            }

            if (!FirstPlayerId.HasValue)
            {
                throw new InvalidOperationException("Cannot transition to Main phase: FirstPlayerId is null");
            }

            // 【架构修正】只负责状态转换，不创建事件或效果请求
            Phase = GamePhase.Main;
            CurrentPlayerId = FirstPlayerId.Value;

            // 【架构修正】不在这里创建TurnStartEvent或TurnStartEffectRequest
            // 这些应该由调用方（PlayerState.PerformMulligan）根据需要创建并入队

            return events;
        }

        /// <summary>
        /// Clears all registered card instances from the global lookup dictionary.
        /// Should be called when the game ends and state is being cleaned up.
        /// </summary>
        public void ClearAllRegisteredCards()
        {
            _allCardInstancesById.Clear();
        }

        // Removed ToDto() method as mapping is now handled by AutoMapper (GameStateProfile)
    } // End of GameState class
} // End of namespace