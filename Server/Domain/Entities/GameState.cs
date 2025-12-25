// File: Server/Domain/Entities/GameState.cs
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos; // Added for DTO types
using ShadowverseBeyond.Server.Dtos.Enums; // For GameStatus, GamePhase
using System.Collections.Generic;
using System.Linq; // Added for FirstOrDefault
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For DomainOperationResult
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;

using System.Collections.Concurrent; // Added for ConcurrentDictionary

namespace ShadowverseBeyond.Server.Domain.Entities
{
    // Note: PlayerState and UnitState are now defined in separate files within this namespace.
    // Removed the placeholder records for PlayerStatePlaceholder and UnitStatePlaceholder.


    /// <summary>
    /// Represents the complete state of a single game instance.
    /// This is the core domain entity used for game logic processing.
    /// </summary>
    [DslExposable("游戏状态", Description = "代表整个游戏实例的完整状态")]
    public class GameState
    {
        private readonly ConcurrentDictionary<Guid, CardInstance> _allCardInstancesById = new();

        public InGameIdRegistry InGameIds { get; } = new();

        public string GameId { get; set; }

        public GameStatus Status { get; set; } = GameStatus.Starting;

        public GamePhase Phase { get; set; } = GamePhase.Mulligan;

        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

        public long StateRevision { get; private set; } = 0;
        public long BuffRevision { get; private set; } = 0;
        public long RefreshCycleId { get; private set; } = 0;

        // 【架构重构】已删除全局TurnNumber，使用PlayerState.TurnNumber管理每个玩家的回合数
        public Guid? CurrentPlayerId { get; set; } // Changed from string? to Guid?

        public Guid? FirstPlayerId { get; set; } // Changed from string? to Guid?

        public Guid? WinnerId { get; set; } // Changed from string? to Guid?

        public string? GameMessage { get; set; } // Added

        // Keep direct references to players for easier access in game logic
        [DslExposable("玩家1", Description = "游戏中的第一个玩家")]
        public PlayerState Player1 { get; set; }

        [DslExposable("玩家2", Description = "游戏中的第二个玩家")]
        public PlayerState Player2 { get; set; }

        [DslExposable("先手玩家", Description = "游戏中先行动的玩家")]
        public PlayerState FirstPlayer => Player1.IsFirst ? Player1 : Player2;

        [DslExposable("当前玩家", Description = "目前进行回合的玩家")]
        public PlayerState CurrentPlayer => GetPlayerState(CurrentPlayerId.Value);

        // --- Core Game State Managers ---
        // These are initialized externally by the GameService to keep the domain model clean.
        public IAbilityManager AbilityManager { get; set; } = null!;
        public IBuffManager BuffManager { get; set; } = null!;

        // Derived property for convenience, especially for DTO mapping
        public bool IsGameOver => Status == GameStatus.Finished;

        // --- Aggregate Properties for DSL Queries ---
        [DslExposable("所有战场", Description = "双方玩家战场上的所有卡牌")]
        public List<CardInstance> AllField => Player1.Field.Concat(Player2.Field).ToList();

        [DslExposable("所有手牌", Description = "双方玩家手中的所有卡牌")]
        public List<CardInstance> AllHand => Player1.Hand.Concat(Player2.Hand).ToList();

        [DslExposable("所有牌库", Description = "双方玩家牌库中的所有卡牌")]
        public List<CardInstance> AllDeck => Player1.Deck.Concat(Player2.Deck).ToList();

        [DslExposable("所有墓地", Description = "双方玩家墓地中的所有卡牌")]
        public List<CardInstance> AllGraveyard => Player1.Graveyard.Concat(Player2.Graveyard).ToList();

        [DslExposable("所有额外区", Description = "双方玩家额外区域中的所有卡牌")]
        public List<CardInstance> AllExtra => Player1.Extra.Concat(Player2.Extra).ToList();

        [DslExposable("所有纹章区", Description = "双方玩家纹章区域中的所有卡牌")]
        public List<CardInstance> AllEmblem => Player1.Emblem.Concat(Player2.Emblem).ToList();

        [DslExposable("所有主战者", Description = "双方玩家的主战者卡牌")]
        public List<CardInstance> AllLeaders
        {
            get
            {
                var leaders = new List<CardInstance>();
                if (Player1.Leader != null) leaders.Add(Player1.Leader);
                if (Player2.Leader != null) leaders.Add(Player2.Leader);
                return leaders;
            }
        }

        [DslExposable("所有卡牌", Description = "游戏中的所有卡牌（包括所有区域）")]
        public List<CardInstance> AllCards => AllField
            .Concat(AllHand)
            .Concat(AllDeck)
            .Concat(AllGraveyard)
            .Concat(AllExtra)
            .Concat(AllEmblem)
            .ToList();

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
            StartedAtUtc = DateTime.UtcNow;
            // 【架构重构】不再初始化全局TurnNumber，使用PlayerState.TurnNumber

            // Assign readable ids early for debugging/logs.
            InGameIds.GetOrCreate(InGameIdKind.Player, Player1.PlayerId);
            InGameIds.GetOrCreate(InGameIdKind.Player, Player2.PlayerId);
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
            StartedAtUtc = DateTime.UtcNow;
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
        /// Finds a card instance by its ID within a specific zone or across player leaders.
        /// </summary>
        /// <param name="cardId">The instance ID of the card to find.</param>
        /// <param name="zone">The zone to search in. Defaults to CardZone.Field.</param>
        /// <returns>The found CardInstance, or null if not found.</returns>
        public CardInstance? FindCardInstance(Guid cardId, CardZone zone = CardZone.Field)
        {
            if (cardId == Guid.Empty) return null;


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
        public CardInstance? FindCardInstance(Guid instanceId)
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
                InGameIds.GetOrCreate(InGameIdKind.Card, card.InstanceId);
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

        internal void IncrementStateRevision()
        {
            StateRevision++;
        }

        internal void IncrementBuffRevision()
        {
            BuffRevision++;
        }

        internal long AdvanceRefreshCycle()
        {
            RefreshCycleId++;
            return RefreshCycleId;
        }

        // Removed ToDto() method as mapping is now handled by AutoMapper (GameStateProfile)
    } // End of GameState class
} // End of namespace
