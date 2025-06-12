// File: Server/Domain/Entities/PlayerState.cs
using System; // Added for Guid
using System.Collections.Generic;
using System.Linq; // Added for FirstOrDefault
using ShadowverseBeyond.Server.Dtos; // Added for DTO types
using ShadowverseBeyond.Server.Dtos.Enums; // Added for CardType enum
using ShadowverseBeyond.Server.Domain.Events; // Added for IGameEvent

namespace ShadowverseBeyond.Server.Domain.Entities
{
    // Note: CardInstance is now defined in CardInstance.cs
    // This class has been updated to use CardInstance instead of CardState
    // Leader health management has been moved to LeaderState in GameState

    /// <summary>
    /// Represents the state of a single player within a game.
    /// Leader health is now managed through the associated LeaderState in GameState.
    /// </summary>
    public class PlayerState
    {
        public Guid PlayerId { get; set; } // Changed from string to Guid

        // Core Stats (Leader health removed - now managed via LeaderState)
        public int MaxPlayPoints { get; set; } = 0; // Starts at 0, increases per turn
        public int CurrentPlayPoints { get; set; } = 0;
        public int MaxEvolutionPoints { get; set; } = 0; // Determined by going first/second
        public int CurrentEvolutionPoints { get; set; } = 0;

        // Player Status & Turn Info
        public bool IsAI { get; set; } = false;
        public bool IsFirst { get; set; } = false;
        public bool HasCompletedMulligan { get; set; } = false;
        // Removed CanEvolve - evolution eligibility is determined by game rules and EP availability
        public int TurnNumber { get; set; } = 0; // Player's individual turn count
        public int PlayedCardCountThisTurn { get; set; } = 0;
        public bool HasTakenFirstTurn { get; set; } = false; // Added based on GameService logic

        /// <summary>
        /// Current health of the player's leader.
        /// Gets the value from the Leader CardInstance.
        /// </summary>
        public int CurrentHealth => Leader?.CurrentHealth ?? 0;

        /// <summary>
        /// Maximum health of the player's leader.
        /// Gets the value from the Leader CardInstance.
        /// </summary>
        public int MaxHealth => Leader?.MaxHealth ?? 0;

        // Card Zones (Using CardInstance instead of CardState)
        public CardInstance Leader { get; set; } = null!; // Must be initialized externally (e.g., in GameService)
        public List<CardInstance> Hand { get; set; } = new List<CardInstance>();
        public List<CardInstance> Deck { get; set; } = new List<CardInstance>();
        public List<CardInstance> Field { get; set; } = new List<CardInstance>();
        public List<CardInstance> Graveyard { get; set; } = new List<CardInstance>();
        public List<CardInstance> Extra { get; set; } = new List<CardInstance>();

        // Calculated property for Deck Count
        public int DeckCount => Deck?.Count ?? 0;

        // Note: Field units are now managed separately in GameState as Player1Units/Player2Units
        // This removes the redundant Field property from PlayerState

        // Player Statuses/Effects (Consider a dedicated class/struct later)
        // public List<string> ActiveEffects { get; set; } = new List<string>();

        // Note: IsVengeanceActive calculation moved to service layer since it depends on LeaderState

        // Constructor
        public PlayerState(Guid playerId) // Changed parameter type to Guid
        {
            PlayerId = playerId;
            // Initialization logic (e.g., loading deck, setting initial PP/EP based on turn order)
            // would typically happen elsewhere, perhaps in GameState creation or IGameStateManager.
            // Initialize lists to prevent null reference issues.
            Hand = new List<CardInstance>();
            Deck = new List<CardInstance>();
            Field = new List<CardInstance>();
            Graveyard = new List<CardInstance>();
            Extra = new List<CardInstance>();
        }

        // Parameterless constructor often needed for ORMs or deserialization
        private PlayerState()
        {
            PlayerId = Guid.Empty; // Use Guid.Empty for default
            Hand = new List<CardInstance>();
            Deck = new List<CardInstance>();
            Field = new List<CardInstance>();
            Graveyard = new List<CardInstance>();
            Extra = new List<CardInstance>();
        }

        // --- State Manipulation Methods ---

        /// <summary>
        /// 修改玩家的当前PP值
        /// </summary>
        /// <param name="amount">修改量（可以为负数）</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> ModifyPlayPoints(int amount)
        {
            var events = new List<IGameEvent>();
            int originalPP = CurrentPlayPoints;

            // 限制PP在0到最大值之间
            CurrentPlayPoints = Math.Max(0, Math.Min(CurrentPlayPoints + amount, MaxPlayPoints));

            // TODO: 生成PP更新事件
            // var ppUpdateEvent = new PpUpdateEvent(PlayerId, CurrentPlayPoints, MaxPlayPoints);
            // events.Add(ppUpdateEvent);

            return events;
        }

        /// <summary>
        /// 从手牌中移除指定卡牌
        /// </summary>
        /// <param name="card">要移除的卡牌</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> RemoveCardFromHand(CardInstance card)
        {
            var events = new List<IGameEvent>();

            if (Hand.Remove(card))
            {
                // TODO: 生成手牌移除事件
                // var cardRemovedEvent = new CardRemovedFromHandEvent(PlayerId, card);
                // events.Add(cardRemovedEvent);
            }

            return events;
        }

        /// <summary>
        /// 将卡牌召唤到场地
        /// </summary>
        /// <param name="card">要召唤的卡牌</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> SummonCardToField(CardInstance card)
        {
            var events = new List<IGameEvent>();

            // 设置卡牌区域和位置
            card.CurrentZone = CardZone.Field;
            card.FieldPosition = Field.Count;

            // 添加到场地
            Field.Add(card);

            // TODO: 生成召唤事件
            // var summonEvent = new CardSummonedEvent(PlayerId, card);
            // events.Add(summonEvent);

            return events;
        }

        /// <summary>
        /// 将卡牌移动到墓地
        /// </summary>
        /// <param name="card">要移动的卡牌</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> MoveCardToGraveyard(CardInstance card)
        {
            var events = new List<IGameEvent>();

            // 从当前区域移除
            RemoveCardFromCurrentZone(card);

            // 设置新区域并添加到墓地
            card.CurrentZone = CardZone.Graveyard;
            Graveyard.Add(card);

            // TODO: 生成墓地移动事件
            // var graveyardEvent = new CardMovedToGraveyardEvent(PlayerId, card);
            // events.Add(graveyardEvent);

            return events;
        }

        /// <summary>
        /// 执行出牌操作的完整流程
        /// </summary>
        /// <param name="cardInHand">手牌中的卡牌</param>
        /// <param name="cardDefinition">卡牌定义</param>
        /// <param name="cardType">卡牌类型</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> PlayCard(CardInstance cardInHand, CardDefinition cardDefinition, CardType cardType)
        {
            var events = new List<IGameEvent>();

            // 1. 扣除费用
            var ppEvents = ModifyPlayPoints(-cardDefinition.Cost);
            events.AddRange(ppEvents);

            // 2. 从手牌移除
            var removeEvents = RemoveCardFromHand(cardInHand);
            events.AddRange(removeEvents);

            // 3. 根据卡牌类型处理
            switch (cardType)
            {
                case CardType.Follower:
                case CardType.Amulet:
                    // 创建场地卡牌并召唤
                    var fieldCard = CreateFieldCardInstance(cardInHand, cardDefinition);
                    var summonEvents = SummonCardToField(fieldCard);
                    events.AddRange(summonEvents);
                    break;

                case CardType.Spell:
                    // 法术直接进入墓地
                    var spellEvents = MoveCardToGraveyard(cardInHand);
                    events.AddRange(spellEvents);
                    break;
            }

            return events;
        }

        /// <summary>
        /// 创建场地上的卡牌实例
        /// </summary>
        /// <param name="handCard">手牌中的卡牌</param>
        /// <param name="cardDefinition">卡牌定义</param>
        /// <returns>场地卡牌实例</returns>
        private CardInstance CreateFieldCardInstance(CardInstance handCard, CardDefinition cardDefinition)
        {
            return new CardInstance(handCard.InstanceId, cardDefinition.Id, PlayerId)
            {
                CurrentZone = CardZone.Field,
                CardType = cardDefinition.Type ?? string.Empty,
                BaseAttack = cardDefinition.Attack ?? 0,
                CurrentAttack = cardDefinition.Attack ?? 0,
                BaseHealth = cardDefinition.Health ?? 0,
                CurrentHealth = cardDefinition.Health ?? 0,
                MaxHealth = cardDefinition.Health ?? 0,
                BaseCost = cardDefinition.Cost,
                CurrentCost = cardDefinition.Cost,
                IsEvolved = false,
                CanAttack = false, // 新召唤的单位通常不能立即攻击
                AttacksThisTurn = 0,
                FieldPosition = null // 将在添加到场地时设置
            };
        }

        /// <summary>
        /// 消耗进化点数
        /// </summary>
        /// <param name="amount">消耗的EP数量</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> ConsumeEvolutionPoints(int amount)
        {
            var events = new List<IGameEvent>();

            CurrentEvolutionPoints = Math.Max(0, CurrentEvolutionPoints - amount);

            // TODO: 生成EP更新事件
            // var epUpdateEvent = new EpUpdateEvent(PlayerId, CurrentEvolutionPoints, MaxEvolutionPoints);
            // events.Add(epUpdateEvent);

            return events;
        }

        /// <summary>
        /// 执行卡牌进化操作
        /// </summary>
        /// <param name="cardToEvolve">要进化的卡牌</param>
        /// <param name="evolvedDefinition">进化后的卡牌定义</param>
        /// <param name="evolutionPointCost">进化消耗的EP</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> EvolveCard(CardInstance cardToEvolve, CardDefinition evolvedDefinition, int evolutionPointCost)
        {
            var events = new List<IGameEvent>();

            // 1. 消耗进化点数
            var epEvents = ConsumeEvolutionPoints(evolutionPointCost);
            events.AddRange(epEvents);

            // 2. 执行卡牌进化
            var evolveEvents = cardToEvolve.Evolve(evolvedDefinition);
            events.AddRange(evolveEvents);

            return events;
        }

        /// <summary>
        /// 执行回合结束操作
        /// </summary>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> EndTurn()
        {
            var events = new List<IGameEvent>();

            // 1. 重置单位攻击次数和攻击能力
            var fieldResetEvents = ResetFieldUnitsForTurnEnd();
            events.AddRange(fieldResetEvents);

            // 2. 处理回合结束时的状态更新
            var statusEvents = ProcessTurnEndStatusUpdates();
            events.AddRange(statusEvents);

            // TODO: 生成回合结束事件
            // var turnEndEvent = new TurnEndEvent(PlayerId);
            // events.Add(turnEndEvent);

            return events;
        }

        /// <summary>
        /// 重置场地单位的回合结束状态
        /// </summary>
        /// <returns>生成的事件列表</returns>
        private List<IGameEvent> ResetFieldUnitsForTurnEnd()
        {
            var events = new List<IGameEvent>();

            foreach (var unit in Field)
            {
                // 重置攻击次数
                unit.AttacksThisTurn = 0;

                // 如果单位本回合无法攻击，下回合可以攻击
                if (!unit.CanAttack)
                {
                    unit.CanAttack = true;
                }

                // TODO: 处理其他回合结束时的单位状态重置
                // 例如：某些临时效果的清除等
            }

            return events;
        }

        /// <summary>
        /// 处理回合结束时的状态更新
        /// </summary>
        /// <returns>生成的事件列表</returns>
        private List<IGameEvent> ProcessTurnEndStatusUpdates()
        {
            var events = new List<IGameEvent>();

            // TODO: 处理回合结束时的各种状态更新
            // 例如：
            // - Buff持续时间减少
            // - 临时效果清除
            // - 特殊状态处理等

            return events;
        }

        /// <summary>
        /// 执行换牌操作
        /// </summary>
        /// <param name="cardUidsToReplace">要替换的卡牌UID列表</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> PerformMulligan(List<Guid> cardUidsToReplace)
        {
            var events = new List<IGameEvent>();

            if (HasCompletedMulligan)
            {
                throw new InvalidOperationException($"Player {PlayerId} has already completed mulligan.");
            }

            // 1. 找到要替换的卡牌
            var cardsToReplace = new List<CardInstance>();
            foreach (var cardUid in cardUidsToReplace)
            {
                var cardToReplace = Hand.FirstOrDefault(c => c.InstanceId == cardUid);
                if (cardToReplace != null)
                {
                    cardsToReplace.Add(cardToReplace);
                }
            }

            // 2. 将要替换的卡牌放回牌库
            foreach (var card in cardsToReplace)
            {
                Hand.Remove(card);
                card.CurrentZone = CardZone.Deck;
                Deck.Add(card);
            }

            // 3. 洗牌
            ShuffleDeck();

            // 4. 抽取新卡牌
            var drawnCards = new List<CardInstance>();
            int cardsToDrawCount = cardsToReplace.Count;
            for (int i = 0; i < cardsToDrawCount && Deck.Count > 0; i++)
            {
                var drawnCard = Deck[0];
                Deck.RemoveAt(0);
                drawnCard.CurrentZone = CardZone.Hand;
                Hand.Add(drawnCard);
                drawnCards.Add(drawnCard);
            }

            // 5. 标记换牌完成
            HasCompletedMulligan = true;

            // TODO: 生成换牌相关事件
            // var mulliganEvent = new MulliganCompletedEvent(PlayerId, cardsToReplace, drawnCards);
            // events.Add(mulliganEvent);

            return events;
        }

        /// <summary>
        /// 洗牌操作
        /// </summary>
        private void ShuffleDeck()
        {
            var random = new Random();
            for (int i = Deck.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (Deck[i], Deck[j]) = (Deck[j], Deck[i]);
            }
        }

        /// <summary>
        /// 执行回合开始操作
        /// </summary>
        /// <param name="turnNumber">当前回合数</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> StartTurn(int turnNumber)
        {
            var events = new List<IGameEvent>();

            // 1. 恢复PP（Play Point）
            var ppRestoreEvents = RestorePlayPoints(turnNumber);
            events.AddRange(ppRestoreEvents);

            // 2. 抽卡
            var drawEvents = DrawCardForTurnStart();
            events.AddRange(drawEvents);

            // 3. 重置单位状态
            var unitResetEvents = ResetUnitsForTurnStart();
            events.AddRange(unitResetEvents);

            // 4. 处理回合开始时的状态更新
            var statusEvents = ProcessTurnStartStatusUpdates();
            events.AddRange(statusEvents);

            // TODO: 生成回合开始事件
            // var turnStartEvent = new TurnStartEvent(PlayerId, turnNumber);
            // events.Add(turnStartEvent);

            return events;
        }

        /// <summary>
        /// 恢复PP（Play Point）
        /// </summary>
        /// <param name="turnNumber">当前回合数</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> RestorePlayPoints(int turnNumber)
        {
            var events = new List<IGameEvent>();

            // 根据回合数增加最大PP（最多10点）
            if (turnNumber <= 10)
            {
                MaxPlayPoints = Math.Min(10, turnNumber);
            }

            // 恢复当前PP到最大值
            CurrentPlayPoints = MaxPlayPoints;

            // TODO: 生成PP更新事件
            // var ppUpdateEvent = new PlayPointUpdateEvent(PlayerId, CurrentPlayPoints, MaxPlayPoints);
            // events.Add(ppUpdateEvent);

            return events;
        }

        /// <summary>
        /// 回合开始时抽卡
        /// </summary>
        /// <returns>生成的事件列表</returns>
        private List<IGameEvent> DrawCardForTurnStart()
        {
            var events = new List<IGameEvent>();

            // 抽一张卡
            if (Deck.Count > 0)
            {
                var drawnCard = Deck[0];
                Deck.RemoveAt(0);
                drawnCard.CurrentZone = CardZone.Hand;
                Hand.Add(drawnCard);

                // TODO: 生成抽卡事件
                // var drawEvent = new CardDrawnEvent(PlayerId, drawnCard);
                // events.Add(drawEvent);
            }

            return events;
        }

        /// <summary>
        /// 抽取指定数量的卡牌
        /// </summary>
        /// <param name="drawCount">要抽取的卡牌数量</param>
        /// <returns>抽到的卡牌列表</returns>
        public List<CardInstance> DrawCards(int drawCount)
        {
            var drawnCards = new List<CardInstance>();

            for (int i = 0; i < drawCount; i++)
            {
                if (Deck.Count > 0)
                {
                    var drawnCard = Deck[0];
                    Deck.RemoveAt(0);
                    drawnCard.CurrentZone = CardZone.Hand;
                    Hand.Add(drawnCard);
                    drawnCards.Add(drawnCard);

                    // TODO: 生成抽卡事件
                    // var drawEvent = new CardDrawnEvent(PlayerId, drawnCard);
                    // events.Add(drawEvent);
                }
                else
                {
                    // 牌库为空，无法继续抽卡
                    break;
                }
            }

            return drawnCards;
        }

        /// <summary>
        /// 将卡牌添加到场上
        /// </summary>
        /// <param name="cardInstance">要添加的卡牌实例</param>
        /// <returns>生成的事件列表</returns>
        public List<IGameEvent> AddCardToField(CardInstance cardInstance)
        {
            var events = new List<IGameEvent>();

            // 添加随从到场上末尾并分配场上位置
            Field.Add(cardInstance);
            cardInstance.FieldPosition = Field.Count - 1;
            cardInstance.CurrentZone = CardZone.Field;

            // TODO: 生成场上单位进入事件
            // var enterFieldEvent = new CardEnteredFieldEvent(PlayerId, cardInstance);
            // events.Add(enterFieldEvent);

            return events;
        }

        /// <summary>
        /// 重置单位的回合开始状态
        /// </summary>
        /// <returns>生成的事件列表</returns>
        private List<IGameEvent> ResetUnitsForTurnStart()
        {
            var events = new List<IGameEvent>();

            foreach (var unit in Field)
            {
                // 重置攻击次数
                unit.AttacksThisTurn = 0;

                // 如果单位上回合无法攻击，本回合可以攻击
                if (!unit.CanAttack)
                {
                    unit.CanAttack = true;
                }

                // TODO: 处理其他回合开始时的单位状态重置
            }

            return events;
        }

        /// <summary>
        /// 处理回合开始时的状态更新
        /// </summary>
        /// <returns>生成的事件列表</returns>
        private List<IGameEvent> ProcessTurnStartStatusUpdates()
        {
            var events = new List<IGameEvent>();

            // TODO: 处理回合开始时的各种状态更新
            // 例如：
            // - Buff持续时间处理
            // - 特殊效果触发等

            return events;
        }

        /// <summary>
        /// 从当前区域移除卡牌的辅助方法
        /// </summary>
        /// <param name="card">要移除的卡牌</param>
        private void RemoveCardFromCurrentZone(CardInstance card)
        {
            switch (card.CurrentZone)
            {
                case CardZone.Hand:
                    Hand.Remove(card);
                    break;
                case CardZone.Field:
                    Field.Remove(card);
                    break;
                case CardZone.Deck:
                    Deck.Remove(card);
                    break;
                case CardZone.Graveyard:
                    Graveyard.Remove(card);
                    break;
                case CardZone.Extra:
                    Extra.Remove(card);
                    break;
            }
        }
    }
}