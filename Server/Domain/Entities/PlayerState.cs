// File: Server/Domain/Entities/PlayerState.cs
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Exceptions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    [DslExposable("玩家状态", Description = "代表游戏中一个玩家的完整状态信息")]
    public class PlayerState
    {
        public Guid PlayerId { get; set; }

        [DslExposable("最大PP", Description = "玩家的最大PP值")]
        public int MaxPlayPoints { get; set; } = 0;

        [DslExposable("当前PP", Description = "玩家当前可用的PP值")]
        public int CurrentPlayPoints { get; set; } = 0;
        public int PpBoostUsesAvailable { get; set; } = 0;
        public bool PpBoostResetApplied { get; set; } = false;

        [DslExposable("最大EP", Description = "玩家的最大进化点数")]
        public int MaxEvolutionPoints { get; set; } = 0;

        [DslExposable("当前EP", Description = "玩家当前可用的进化点数")]
        public int CurrentEvolutionPoints { get; set; } = 0;

        [DslExposable("最大SEP", Description = "玩家的最大超进化点数")]
        public int MaxSuperEvolutionPoints { get; set; } = 0;

        [DslExposable("当前SEP", Description = "玩家当前可用的超进化点数")]
        public int CurrentSuperEvolutionPoints { get; set; } = 0;
        public bool IsAI { get; set; } = false;

        [DslExposable("是否先手", Description = "玩家是否为先手")]
        public bool IsFirst { get; set; } = false;
        public bool HasCompletedMulligan { get; set; } = false;

        [DslExposable("回合数", Description = "当前回合数")]
        public int TurnNumber { get; set; } = 0;

        [DslExposable("本回合出牌数", Description = "本回合已出牌的数量")]
        public int PlayedCardCountThisTurn { get; set; } = 0;

        public bool HasTakenFirstTurn { get; set; } = false;

        [DslExposable("当前生命值", Description = "玩家当前的生命值")]
        public int CurrentHealth => Leader?.CurrentHealth ?? 0;

        [DslExposable("最大生命值", Description = "玩家的最大生命值")]
        public int MaxHealth => Leader?.MaxHealth ?? 0;

        [DslExposable("主战者", Description = "玩家的主战者卡牌")]
        public CardInstance Leader { get; set; } = null!;

        [DslExposable("手牌", Description = "玩家手中的卡牌列表")]
        public List<CardInstance> Hand { get; set; } = new List<CardInstance>();

        [DslExposable("牌库", Description = "玩家牌库中的卡牌列表")]
        public List<CardInstance> Deck { get; set; } = new List<CardInstance>();

        [DslExposable("战场", Description = "玩家战场上的卡牌列表")]
        public List<CardInstance> Field { get; set; } = new List<CardInstance>();

        [DslExposable("墓地", Description = "玩家墓地中的卡牌列表")]
        public List<CardInstance> Graveyard { get; set; } = new List<CardInstance>();

        [DslExposable("墓场数", Description = "玩家墓场计数（用于死灵术等）")]
        public int GraveyardCount { get; set; } = 0;

        [DslExposable("血池", Description = "玩家血池计数（用于血祭等）")]
        public int BloodPool { get; set; } = 0;

        public List<CardInstance> Extra { get; set; } = new List<CardInstance>();
        [DslExposable("纹章区", Description = "玩家纹章区域中的卡牌列表")]
        public List<CardInstance> Emblem { get; set; } = new List<CardInstance>();
        public int DeckCount => Deck?.Count ?? 0;
        public bool CanEvolve { get; set; } = false;
        public bool CanSuperEvolve { get; set; } = false;

        public PlayerState(Guid playerId)
        {
            PlayerId = playerId;
            Hand = new List<CardInstance>();
            Deck = new List<CardInstance>();
            Field = new List<CardInstance>();
            Graveyard = new List<CardInstance>();
            Extra = new List<CardInstance>();
            Emblem = new List<CardInstance>();
        }

        private PlayerState()
        {
            PlayerId = Guid.Empty;
            Hand = new List<CardInstance>();
            Deck = new List<CardInstance>();
            Field = new List<CardInstance>();
            Graveyard = new List<CardInstance>();
            Extra = new List<CardInstance>();
            Emblem = new List<CardInstance>();
        }

        public DomainOperationResult ModifyPlayPoints(int amount)
        {
            var result = new DomainOperationResult();
            // Do not mutate here; just emit an event with the intended target values.
            int targetCurrent = Math.Max(0, Math.Min(this.CurrentPlayPoints + amount, this.MaxPlayPoints));
            result.GeneratedEvents.Add(new PpUpdateEvent(this, targetCurrent));
            result.Success = true;
            return result;
        }

        public DomainOperationResult ModifyMaxPlayPoints(int amount)
        {
            var result = new DomainOperationResult();
            // Emit target values; Commit will apply state.
            int targetMax = Math.Max(0, Math.Min(this.MaxPlayPoints + amount, GameConstants.PlayPoints.MaxPlayPoints));
            result.GeneratedEvents.Add(new PpMaxUpdateEvent(this, targetMax));
            result.Success = true;
            return result;
        }

        private List<CardInstance>? GetZoneList(CardZone zone)
        {
            return zone switch
            {
                CardZone.Hand => this.Hand,
                CardZone.Deck => this.Deck,
                CardZone.Field => this.Field,
                CardZone.Graveyard => this.Graveyard,
                CardZone.Extra => this.Extra,
                CardZone.Emblem => this.Emblem,
                _ => null,
            };
        }

        private void UpdateZoneIndices(CardZone zone)
        {
            var zoneList = GetZoneList(zone);
            if (zoneList == null) return;

            for (int i = 0; i < zoneList.Count; i++)
            {
                zoneList[i].ZoneIndex = i;
            }
        }

        private bool RemoveCardFromCurrentZone(CardInstance card)
        {
            var zoneList = GetZoneList(card.CurrentZone);
            if (zoneList == null)
            {
                return card.CurrentZone == CardZone.Playing
                    || card.CurrentZone == CardZone.None
                    || card.CurrentZone == CardZone.Removed;
            }

            if (zoneList.Remove(card))
            {
                UpdateZoneIndices(card.CurrentZone);
                return true;
            }

            return false;
        }

        #region Internal State Mutations
        internal bool ApplyCardZoneChange(CardInstance card, CardZone from, CardZone to, int? index)
        {
            if (card.CurrentZone != from)
            {
                // If source mismatch, try to reconcile by removing from actual zone
                RemoveCardFromCurrentZone(card);
            }

            var sourceZone = card.CurrentZone;
            if (sourceZone == to)
                return true;

            // Remove from current zone
            if (!RemoveCardFromCurrentZone(card))
                return false;

            var destinationList = GetZoneList(to);
            if (destinationList == null && to != CardZone.Extra && to != CardZone.None && to != CardZone.Removed)
            {
                // Revert to original zone
                GetZoneList(sourceZone)?.Add(card);
                UpdateZoneIndices(sourceZone);
                return false;
            }

            card.CurrentZone = to;
            if (destinationList != null)
            {
                if (index.HasValue && index.Value >= 0 && index.Value <= destinationList.Count)
                    destinationList.Insert(index.Value, card);
                else
                    destinationList.Add(card);
                UpdateZoneIndices(to);
            }

            return true;
        }
        #endregion

        private DomainOperationResult MoveCard(CardInstance card, CardZone destination, int? index = null, CardMoveReason moveReason = CardMoveReason.Unknown)
        {
            var result = new DomainOperationResult();
            var sourceZone = card.CurrentZone;

            if (sourceZone == destination)
            {
                result.Success = true;
                return result;
            }

            // Emit only the atomic move; notifications will be derived in CardMovedEvent.Commit
            result.GeneratedEvents.Add(new CardMovedEvent(card, sourceZone, destination, moveReason));
            result.Success = true;
            return result;
        }

        private static bool HasMoveToZone(DomainOperationResult result, CardZone destination)
        {
            return result.GeneratedEvents.OfType<CardMovedEvent>().Any(e => e.ToZone == destination);
        }

        #region Internal State Mutations (Evolution)
        /// <summary>
        /// 内部方法：标记本回合已使用进化权。
        /// </summary>
        internal void ApplyEvolutionRightUsed()
        {
            this.CanEvolve = false;
        }

        internal void ApplySuperEvolutionRightUsed()
        {
            this.CanSuperEvolve = false;
        }
        #endregion

        // Internal State Mutations for resources and flags
        internal void ApplyEvolutionPointsMaxChange(int maxEp)
        {
            MaxEvolutionPoints = Math.Max(0, maxEp);
            if (CurrentEvolutionPoints > MaxEvolutionPoints)
            {
                CurrentEvolutionPoints = MaxEvolutionPoints;
            }
        }

        internal void ApplyEvolutionPointsCurrentChange(int currentEp)
        {
            CurrentEvolutionPoints = Math.Max(0, Math.Min(currentEp, MaxEvolutionPoints));
        }

        internal void ApplySuperEvolutionPointsMaxChange(int maxSep)
        {
            MaxSuperEvolutionPoints = Math.Max(0, maxSep);
            if (CurrentSuperEvolutionPoints > MaxSuperEvolutionPoints)
            {
                CurrentSuperEvolutionPoints = MaxSuperEvolutionPoints;
            }
        }

        internal void ApplySuperEvolutionPointsCurrentChange(int currentSep)
        {
            CurrentSuperEvolutionPoints = Math.Max(0, Math.Min(currentSep, MaxSuperEvolutionPoints));
        }

        internal void ApplyPlayPointsMaxChange(int maxPp)
        {
            MaxPlayPoints = Math.Max(0, maxPp);
            var allowedMax = MaxPlayPoints + GameConstants.PlayPoints.PpBoostOverflowAmount;
            if (CurrentPlayPoints > allowedMax)
            {
                CurrentPlayPoints = allowedMax;
            }
        }

        internal void ApplyPlayPointsCurrentChange(int currentPp, bool allowOverflow = false)
        {
            var allowedMax = allowOverflow
                ? MaxPlayPoints + GameConstants.PlayPoints.PpBoostOverflowAmount
                : MaxPlayPoints;
            CurrentPlayPoints = Math.Max(0, Math.Min(currentPp, allowedMax));
        }

        internal void ApplyPpBoostUsesChange(int usesAvailable)
        {
            PpBoostUsesAvailable = Math.Max(0, usesAvailable);
        }

        internal void ApplyMulliganCompleted()
        {
            HasCompletedMulligan = true;
        }

        internal void ApplyPlayedCardCountChange(int increment)
        {
            PlayedCardCountThisTurn += increment;
            if (PlayedCardCountThisTurn < 0) PlayedCardCountThisTurn = 0;
        }

        public DomainOperationResult SummonCardToField(CardInstance card, int? index = null)
        {
            if (Field.Count >= GameConstants.Field.MaxFieldSize)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Field is full." };
            }
            // Only generate atomic move; SummonEvent command should not be re-enqueued here
            var result = MoveCard(card, CardZone.Field, index, CardMoveReason.Summoned);
            return result;
        }

        public DomainOperationResult MoveCardToGraveyard(CardInstance card, CardMoveReason moveReason = CardMoveReason.Unknown)
        {
            return MoveCard(card, CardZone.Graveyard, null, moveReason);
        }

        public DomainOperationResult DestroyCard(CardInstance cardToDestroy, CardInstance cause, string reason = "Effect", CardMoveReason moveReason = CardMoveReason.Destroyed)
        {
            var moveResult = MoveCardToGraveyard(cardToDestroy, moveReason);
            // As per two-method pattern, DestroyEvent is a high-level command and should not be re-enqueued here.
            // MoveCardToGraveyard already generates the atomic CardMovedEvent; no further command events are needed.
            return moveResult;
        }

        public DomainOperationResult PlayCard(CardInstance cardInHand, List<Guid> targetIds, int? paidCost = null)
        {
            var result = new DomainOperationResult();
            var effectiveCost = paidCost ?? cardInHand.CurrentCost;
            var ppResult = ModifyPlayPoints(-effectiveCost);
            result.GeneratedEvents.AddRange(ppResult.GeneratedEvents);
            ApplyPlayedCardCountChange(1);
            return result;
        }

        public DomainOperationResult ConsumeEvolutionPoints(int amount)
        {
            if (amount <= 0)
            {
                return new DomainOperationResult { Success = true };
            }
            var result = new DomainOperationResult();
            int targetCurrent = Math.Max(0, Math.Min(this.CurrentEvolutionPoints - amount, this.MaxEvolutionPoints));
            result.GeneratedEvents.Add(new EpUpdateEvent(this, targetCurrent));
            result.Success = true;
            return result;
        }

        public DomainOperationResult ConsumeSuperEvolutionPoints(int amount)
        {
            if (amount <= 0)
            {
                return new DomainOperationResult { Success = true };
            }
            var result = new DomainOperationResult();
            int targetCurrent = Math.Max(0, Math.Min(this.CurrentSuperEvolutionPoints - amount, this.MaxSuperEvolutionPoints));
            result.GeneratedEvents.Add(new SepUpdateEvent(this, targetCurrent));
            result.Success = true;
            return result;
        }

        public DomainOperationResult EvolveCard(CardInstance cardToEvolve)
        {
            var result = new DomainOperationResult();

            // This method now only generates the events that describe the evolution process.
            // It no longer changes state directly.

            if (cardToEvolve.HasKeyword(CardKeyword.BanEvolve))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Card cannot evolve due to BanEvolve." };
            }

            // 1. Generate event for consuming an Evolution Point
            if (!cardToEvolve.HasKeyword(CardKeyword.FreeEvolve))
            {
                var epResult = ConsumeEvolutionPoints(GameConstants.Evolution.EvolutionPointCost);
                if (!epResult.Success)
                {
                    // This check is a safeguard; the caller should have already verified EP.
                    return epResult;
                }
                result.GeneratedEvents.AddRange(epResult.GeneratedEvents);
            }

            // 2. Generate the event for the card's transformation
            var evolveResult = cardToEvolve.ApplyEvolution();
            result.GeneratedEvents.AddRange(evolveResult.GeneratedEvents);

            result.Success = true;
            return result;
        }

        private DomainOperationResult CheckAndUnlockEvolutionPoints()
        {
            var result = new DomainOperationResult();

            // Don't do anything if EP is already unlocked.
            if (this.MaxEvolutionPoints > 0)
            {
                result.Success = true;
                return result;
            }

            bool isEligibleForUnlock =
                (this.IsFirst && this.TurnNumber == GameConstants.Evolution.FirstPlayerEvolutionStartTurn) ||
                (!this.IsFirst && this.TurnNumber == GameConstants.Evolution.SecondPlayerEvolutionStartTurn);

            if (isEligibleForUnlock)
            {
                int newEp = this.IsFirst
                    ? GameConstants.Evolution.FirstPlayerEvolutionPoints
                    : GameConstants.Evolution.SecondPlayerEvolutionPoints;

                this.MaxEvolutionPoints = newEp;
                this.CurrentEvolutionPoints = newEp;
                this.CanEvolve = true; // Grant evolution right for the turn
                result.GeneratedEvents.Add(new EpMaxUpdateEvent(this, this.MaxEvolutionPoints));
                result.GeneratedEvents.Add(new EpUpdateEvent(this, this.CurrentEvolutionPoints));
            }

            result.Success = true;
            return result;
        }

        public DomainOperationResult SuperEvolveCard(CardInstance cardToEvolve)
        {
            var result = new DomainOperationResult();

            if (cardToEvolve.HasKeyword(CardKeyword.BanEvolve))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Card cannot super evolve due to BanEvolve." };
            }

            var sepResult = ConsumeSuperEvolutionPoints(GameConstants.SuperEvolution.SuperEvolutionPointCost);
            if (!sepResult.Success)
            {
                return sepResult;
            }
            result.GeneratedEvents.AddRange(sepResult.GeneratedEvents);

            var evolveResult = cardToEvolve.ApplySuperEvolution();
            result.GeneratedEvents.AddRange(evolveResult.GeneratedEvents);

            result.Success = true;
            return result;
        }

        private DomainOperationResult CheckAndUnlockSuperEvolutionPoints()
        {
            var result = new DomainOperationResult();

            if (this.MaxSuperEvolutionPoints > 0)
            {
                result.Success = true;
                return result;
            }

            bool isEligibleForUnlock =
                (this.IsFirst && this.TurnNumber == GameConstants.SuperEvolution.FirstPlayerSuperEvolutionStartTurn) ||
                (!this.IsFirst && this.TurnNumber == GameConstants.SuperEvolution.SecondPlayerSuperEvolutionStartTurn);

            if (isEligibleForUnlock)
            {
                int newSep = this.IsFirst
                    ? GameConstants.SuperEvolution.FirstPlayerSuperEvolutionPoints
                    : GameConstants.SuperEvolution.SecondPlayerSuperEvolutionPoints;

                this.MaxSuperEvolutionPoints = newSep;
                this.CurrentSuperEvolutionPoints = newSep;
                this.CanSuperEvolve = true;
                result.GeneratedEvents.Add(new SepMaxUpdateEvent(this, this.MaxSuperEvolutionPoints));
                result.GeneratedEvents.Add(new SepUpdateEvent(this, this.CurrentSuperEvolutionPoints));
            }

            result.Success = true;
            return result;
        }
        internal void ApplyTurnEnd()
        {
            PlayedCardCountThisTurn = 0;
        }


        public DomainOperationResult EndTurn()
        {
            var result = new DomainOperationResult();
            ApplyTurnEnd();
            result.Success = true;
            return result;
        }
        internal void ApplyTurnStart(GameState gameState)
        {
            CanEvolve = CurrentEvolutionPoints > 0;
            CanSuperEvolve = CurrentSuperEvolutionPoints > 0;
            TurnNumber += 1;
            HasTakenFirstTurn = true;
            gameState.CurrentPlayerId = PlayerId;
        }


        public DomainOperationResult PerformMulligan(List<Guid> cardUidsToReplace, GameState gameState)
        {
            // Validation remains the same.
            if (gameState.Phase != GamePhase.Mulligan)
                throw new GameLogicException("Mulligan can only be performed during the mulligan phase.");
            if (this.HasCompletedMulligan)
                throw new GameLogicException($"Player {this.PlayerId} has already completed mulligan.");
            if (cardUidsToReplace.Count > this.Hand.Count)
                throw new GameLogicException("Cannot replace more cards than are in hand.");

            var result = new DomainOperationResult();
            var cardsToReplace = this.Hand.Where(c => cardUidsToReplace.Contains(c.InstanceId)).ToList();

            // Per user instruction, perform direct state mutation as Mulligan is a special case.

            // 1. Move cards from Hand to Deck
            foreach (var card in cardsToReplace)
            {
                this.Hand.Remove(card);
                this.Deck.Add(card);
                card.CurrentZone = CardZone.Deck;
            }

            // 2. Shuffle the deck
            ShuffleDeck();

            // 3. Draw new cards from Deck to Hand
            int cardsToDrawCount = cardsToReplace.Count;
            for (int i = 0; i < cardsToDrawCount && this.Deck.Count > 0; i++)
            {
                if (this.Hand.Count >= GameConstants.Hand.MaxHandSize) break;

                var drawnCard = this.Deck[0];
                this.Deck.RemoveAt(0);
                this.Hand.Add(drawnCard);
                drawnCard.CurrentZone = CardZone.Hand;
            }

            // 4. Update zone indices for consistency
            UpdateZoneIndices(CardZone.Hand);
            UpdateZoneIndices(CardZone.Deck);

            // 5. Apply state change and generate final events
            this.HasCompletedMulligan = true;
            result.GeneratedEvents.Add(new HandUpdatedEvent(this, this.Hand.ToList())); // Send the NEW hand state
            result.GeneratedEvents.Add(new MulliganCompletedEvent(this, cardsToReplace));

            result.Success = true;
            return result;
        }

        private void ShuffleDeck()
        {
            var random = new Random();
            for (int i = Deck.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (Deck[i], Deck[j]) = (Deck[j], Deck[i]);
            }
        }

        public DomainOperationResult StartTurn(GameState gameState)
        {
            if (gameState.IsGameOver)
                throw new GameLogicException("Game is already over.");
            if (gameState.Phase == GamePhase.Mulligan)
                throw new GameLogicException("Cannot start turn during mulligan phase.");
            // Internal apply for turn-start state changes
            ApplyTurnStart(gameState);

            var result = new DomainOperationResult();

            if (!IsFirst && !PpBoostResetApplied && TurnNumber >= GameConstants.PlayPoints.PpBoostResetTurn)
            {
                PpBoostUsesAvailable = GameConstants.PlayPoints.PpBoostUsesPerPhase;
                PpBoostResetApplied = true;
                result.GeneratedEvents.Add(new PpBoostStateUpdatedEvent(this, PpBoostUsesAvailable));
            }

            foreach (var amulet in this.Field.Where(c => c.CurrentCountdown.HasValue && c.CurrentCountdown.Value > 0))
            {
                result.GeneratedEvents.Add(new CountdownTickEvent(amulet, -1));
            }

            foreach (var crest in this.Emblem.Where(c => c.CurrentCountdown.HasValue && c.CurrentCountdown.Value > 0))
            {
                result.GeneratedEvents.Add(new CountdownTickEvent(crest, -1));
            }

            var epResult = CheckAndUnlockEvolutionPoints();
            result.GeneratedEvents.AddRange(epResult.GeneratedEvents);

            var sepResult = CheckAndUnlockSuperEvolutionPoints();
            result.GeneratedEvents.AddRange(sepResult.GeneratedEvents);

            var ppResult = RestorePlayPoints();
            result.GeneratedEvents.AddRange(ppResult.GeneratedEvents);

            int cardsToDraw = GameConstants.Hand.CardsPerTurn;
            var drawResult = DrawCards(cardsToDraw);
            result.GeneratedEvents.AddRange(drawResult.GeneratedEvents);

            var unitResetResult = ResetCardsForTurnStart();
            result.GeneratedEvents.AddRange(unitResetResult.GeneratedEvents);

            result.Success = true;
            return result;
        }

        public DomainOperationResult RestorePlayPoints()
        {
            var result = new DomainOperationResult();
            int targetMax = Math.Min(GameConstants.PlayPoints.MaxPlayPoints, this.MaxPlayPoints + 1);
            int targetCurrent = targetMax; // restore to full
            result.GeneratedEvents.Add(new PpMaxUpdateEvent(this, targetMax));
            result.GeneratedEvents.Add(new PpUpdateEvent(this, targetCurrent));
            return result;
        }

        public DomainOperationResult DrawCards(int drawCount)
        {
            var result = new DomainOperationResult();
            var drawnCardIds = new List<Guid>();

            if (drawCount <= 0)
            {
                result.Success = true;
                return result;
            }

            // 仅选择要抽取的卡牌实例ID，不在此处进行状态移动；移动逻辑在 DrawEvent.Commit -> DrawCards(List<Guid>) 中执行
            var drawnCards = new List<CardInstance>();
            int available = Math.Min(drawCount, this.Deck.Count);
            for (int i = 0; i < available; i++)
            {
                var topCard = this.Deck[i];
                if (topCard != null)
                {
                    drawnCardIds.Add(topCard.InstanceId);
                    drawnCards.Add(topCard);
                }
            }

            if (available < drawCount)
            {
                // 牌库不足时，记录一次 DeckEmptyOnDrawEvent 提示
                result.GeneratedEvents.Add(new DeckEmptyOnDrawEvent(this));
            }

            if (drawnCards.Any())
            {
                result.GeneratedEvents.Add(new DrawEvent(this, drawnCards));
            }

            result.Success = true;
            return result;
        }

        public DomainOperationResult DrawCards(List<Guid> cardInstanceIds)
        {
            var result = new DomainOperationResult();
            var successfullyDrawnIds = new List<Guid>();

            foreach (var cardId in cardInstanceIds)
            {
                var cardToDraw = this.Deck.FirstOrDefault(c => c.InstanceId == cardId);

                if (cardToDraw == null)
                {
                    // This is a critical error, the card specified in the event does not exist in the deck.
                    // This might indicate a state desync or a bug in event creation.
                    result.Success = false;
                    result.ErrorMessage = $"Card with ID {cardId} not found in deck for player {this.PlayerId}.";
                    return result; // Stop processing immediately.
                }

                if (this.Hand.Count >= GameConstants.Hand.MaxHandSize)
                {
                    var burnOpResult = MoveCard(cardToDraw, CardZone.Graveyard, null, CardMoveReason.Burned);
                    result.GeneratedEvents.AddRange(burnOpResult.GeneratedEvents);
                    if (burnOpResult.Success)
                    {
                        result.GeneratedEvents.Add(new CardBurnedEvent(cardToDraw, "HandFull"));
                    }
                }
                else
                {
                    var addOpResult = MoveCard(cardToDraw, CardZone.Hand, null, CardMoveReason.Drawn);
                    if (addOpResult.Success && HasMoveToZone(addOpResult, CardZone.Hand))
                    {
                        successfullyDrawnIds.Add(cardToDraw.InstanceId);
                    }
                    result.GeneratedEvents.AddRange(addOpResult.GeneratedEvents);
                }
            }

            result.Success = true;
            return result;
        }

        public DomainOperationResult AddCardsToHand(List<CardInstance> cardInstancesToAdd)
        {
            var result = new DomainOperationResult();
            if (cardInstancesToAdd == null)
            {
                result.Success = false;
                result.ErrorMessage = "cardInstancesToAdd cannot be null.";
                return result;
            }
            var generatedExpectedMoveEvent = false;
            foreach (var cardInstance in cardInstancesToAdd)
            {
                if (this.Hand.Count >= GameConstants.Hand.MaxHandSize)
                {
                    break;
                }
                if (cardInstance.CurrentZone == CardZone.Hand)
                {
                    continue;
                }
                var addResult = MoveCard(cardInstance, CardZone.Hand, null, CardMoveReason.AddedToHand);
                generatedExpectedMoveEvent = generatedExpectedMoveEvent || HasMoveToZone(addResult, CardZone.Hand);
                result.GeneratedEvents.AddRange(addResult.GeneratedEvents);
            }
            if (generatedExpectedMoveEvent)
            {
                result.GeneratedEvents.Add(new HandUpdatedEvent(this, Hand.ToList()));
            }
            result.Success = true;
            return result;
        }

        public DomainOperationResult AddCardsToDeck(List<CardInstance> cardInstancesToAdd)
        {
            var result = new DomainOperationResult();
            if (cardInstancesToAdd == null)
            {
                result.Success = false;
                result.ErrorMessage = "cardInstancesToAdd cannot be null.";
                return result;
            }

            var generatedExpectedMoveEvent = false;
            foreach (var cardInstance in cardInstancesToAdd)
            {
                if (cardInstance.CurrentZone == CardZone.Deck)
                {
                    continue;
                }
                var addResult = MoveCard(cardInstance, CardZone.Deck, null, CardMoveReason.AddedToDeck);
                generatedExpectedMoveEvent = generatedExpectedMoveEvent || HasMoveToZone(addResult, CardZone.Deck);
                result.GeneratedEvents.AddRange(addResult.GeneratedEvents);
            }

            if (generatedExpectedMoveEvent)
            {
                result.GeneratedEvents.Add(new DeckCountChangedEvent(this, Deck.Count));
            }
            result.Success = true;
            return result;
        }

        public DomainOperationResult AddCardsToEmblem(List<CardInstance> cardInstancesToAdd)
        {
            var result = new DomainOperationResult();
            if (cardInstancesToAdd == null)
            {
                result.Success = false;
                result.ErrorMessage = "cardInstancesToAdd cannot be null.";
                return result;
            }

            foreach (var cardInstance in cardInstancesToAdd)
            {
                var addResult = MoveCard(cardInstance, CardZone.Emblem, null);
                result.GeneratedEvents.AddRange(addResult.GeneratedEvents);
            }

            result.Success = true;
            return result;
        }

        private DomainOperationResult ResetCardsForTurnStart()
        {
            var result = new DomainOperationResult();
            foreach (var unit in Field)
            {
                var oldCanAttack = unit.CanAttack;
                unit.AttacksThisTurn = 0;
                unit.ActsThisTurn = 0;
                unit.FieldTurn++;
                if (!oldCanAttack && unit.CanAttack)
                {
                    result.GeneratedEvents.Add(new AttackStateUpdatedEvent(unit, true));
                }
            }
            result.Success = true;
            return result;
        }

        public DomainOperationResult PreparePlayCard(CardInstance cardInHand, PlayMode playMode = PlayMode.Base, int? paidCost = null)
        {
            var result = new DomainOperationResult();
            if (!this.Hand.Contains(cardInHand))
            {
                result.Success = false;
                result.ErrorMessage = $"Card {cardInHand.InstanceId} not found in hand.";
                return result;
            }
            var effectiveCost = paidCost ?? cardInHand.CurrentCost;
            if (this.CurrentPlayPoints < effectiveCost)
            {
                result.Success = false;
                result.ErrorMessage = $"Not enough PP. Required: {effectiveCost}, Available: {this.CurrentPlayPoints}";
                return result;
            }

            // The cost is now paid when the PlayCardEvent is committed, not when it's requested.
            // This prevents PP loss if the player cancels target selection.
            result.GeneratedEvents.Add(new PlayCardEvent(this, cardInHand, playMode, effectiveCost));
            return result;
        }

        public DomainOperationResult ExecutePlayCard(CardInstance cardInHand, int? paidCost = null)
        {
            var result = new DomainOperationResult();

            // Pay PP cost and increment play count now that the action is confirmed.
            var effectiveCost = paidCost ?? cardInHand.CurrentCost;
            var ppResult = ModifyPlayPoints(-effectiveCost);
            result.GeneratedEvents.AddRange(ppResult.GeneratedEvents);
            ApplyPlayedCardCountChange(1);

            switch (cardInHand.CardType)
            {
                case CardType.Follower:
                case CardType.Amulet:
                    cardInHand.FieldTurn = 0; // Initialize field turn count upon summon
                    result.GeneratedEvents.Add(new SummonEvent(this, cardInHand));
                    break;
                case CardType.Spell:
                    var graveyardResult = MoveCardToGraveyard(cardInHand, CardMoveReason.SpellPlayed);
                    result.GeneratedEvents.AddRange(graveyardResult.GeneratedEvents);
                    var spellboostTargets = Hand
                        .Where(c => c.InstanceId != cardInHand.InstanceId && c.HasAbility(EffectKeyword.SpellBoost))
                        .ToList();
                    foreach (var target in spellboostTargets)
                    {
                        result.GeneratedEvents.Add(new SpellBoostEvent(this, cardInHand, target));
                    }
                    break;
                default:
                    result.Success = false;
                    result.ErrorMessage = $"Unknown card type: {cardInHand.CardType}";
                    return result;
            }
            result.Success = true;
            return result;
        }

        public DomainOperationResult DiscardCard(CardInstance cardToDiscard, string reason)
        {
            if (!this.Hand.Contains(cardToDiscard))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Card {cardToDiscard.InstanceId} not found in hand." };
            }
            var result = MoveCardToGraveyard(cardToDiscard, CardMoveReason.Discarded);
            if (result.Success)
            {
                result.GeneratedEvents.Add(new CardDiscardedEvent(this, cardToDiscard, reason));
            }
            return result;
        }

        public DomainOperationResult ReturnCardToHand(CardInstance cardToReturn)
        {
            if (cardToReturn.CurrentZone != CardZone.Field)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Card {cardToReturn.InstanceId} is not on the field." };
            }
            if (this.Hand.Count >= GameConstants.Hand.MaxHandSize)
            {
                var graveyardResult = MoveCardToGraveyard(cardToReturn, CardMoveReason.Burned);
                graveyardResult.ErrorMessage = "Hand is full, card was moved to graveyard instead.";
                return graveyardResult;
            }
            var result = MoveCard(cardToReturn, CardZone.Hand, null, CardMoveReason.ReturnedToHand);
            if (result.Success && HasMoveToZone(result, CardZone.Hand))
            {
                result.GeneratedEvents.Add(new ReturnEvent(cardToReturn));
            }
            return result;
        }

        public DomainOperationResult SummonTokens(List<CardInstance> tokenInstancesToSummon)
        {
            var result = new DomainOperationResult { Success = true };
            if (tokenInstancesToSummon == null)
            {
                result.Success = false;
                result.ErrorMessage = "tokenInstancesToSummon cannot be null.";
                return result;
            }
            foreach (var tokenInstance in tokenInstancesToSummon)
            {
                if (this.Field.Count >= GameConstants.Field.MaxFieldSize)
                {
                    break;
                }
                var summonResult = SummonCardToField(tokenInstance);
                result.GeneratedEvents.AddRange(summonResult.GeneratedEvents);
            }
            return result;
        }

        public DomainOperationResult VanishCard(CardInstance cardToVanish, string vanishReason = "Effect")
        {
            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.Add(new CardVanishedEvent(cardToVanish, cardToVanish.CurrentZone, vanishReason));
            return result;
        }

        public List<CardInstance> GetCardsInZone(CardZone zone)
        {
            var zoneList = GetZoneList(zone);
            return zoneList != null ? new List<CardInstance>(zoneList) : new List<CardInstance>();
        }

    }
}
