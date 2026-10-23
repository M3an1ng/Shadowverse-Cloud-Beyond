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
        public const string ComboCounterKey = "combo";
        public const string GraveyardCounterKey = "graveyard";
        public const string BloodPoolCounterKey = "bloodpool";
        public const string RallyCounterKey = "rally";
        private static readonly string[] SystemCounterKeys = { ComboCounterKey, GraveyardCounterKey, BloodPoolCounterKey, RallyCounterKey };
        private readonly Counters _counters = new Counters(SystemCounterKeys);

        [DslExposable("玩家ID", Description = "玩家的唯一标识符")]
        public Guid PlayerId { get; set; }

        [DslExposable("最大PP", Description = "玩家的最大PP值")]
        public int MaxPlayPoints { get; set; } = 0;

        [DslExposable("当前PP", Description = "玩家当前可用的PP值")]
        public int CurrentPlayPoints { get; set; } = 0;
        public int PpBoostUsesAvailable { get; set; } = 0;
        public bool PpBoostResetApplied { get; set; } = false;
        public bool IsPpBoostArmed { get; set; } = false;

        [DslExposable("最大EP", Description = "玩家的最大进化点数")]
        public int MaxEvolutionPoints { get; set; } = 0;

        [DslExposable("当前EP", Description = "玩家当前可用的进化点数")]
        public int CurrentEvolutionPoints { get; set; } = 0;

        [DslExposable("最大SEP", Description = "玩家的最大超进化点数")]
        public int MaxSuperEvolutionPoints { get; set; } = 0;

        [DslExposable("当前SEP", Description = "玩家当前可用的超进化点数")]
        public int CurrentSuperEvolutionPoints { get; set; } = 0;

        [DslExposable("是否已解禁进化", Description = "玩家是否已解禁进化")]
        public bool IsEvolutionUnlocked { get; set; } = false;

        [DslExposable("是否已解禁超进化", Description = "玩家是否已解禁超进化")]
        public bool IsSuperEvolutionUnlocked { get; set; } = false;
        public bool IsAI { get; set; } = false;

        [DslExposable("是否先手", Description = "玩家是否为先手")]
        public bool IsFirst { get; set; } = false;
        [DslExposable("职业", Description = "玩家本局使用卡组的职业")]
        public CardClass Class { get; set; } = CardClass.Neutral;
        public CardClass? SecondaryClass { get; set; }
        public DeckEnvironment Environment { get; set; } = DeckEnvironment.Rotation;
        public DeckRule Rule { get; set; } = DeckRule.Standard;
        public Guid? UserDeckId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public bool HasCompletedMulligan { get; set; } = false;

        [DslExposable("回合数", Description = "当前回合数")]
        public int TurnNumber { get; set; } = 0;

        [DslExposable("连击数", Description = "本回合已出牌的数量")]
        public int Combo
        {
            get => _counters.Get(ComboCounterKey);
            set => _counters.Set(ComboCounterKey, value);
        }

        [DslExposable("本回合已用牌数", Description = "本回合已使用卡牌数（连击数别名）")]
        public int PlayedCardCountThisTurn
        {
            get => Combo;
            set => Combo = value;
        }

        public bool HasTakenFirstTurn { get; set; } = false;

        public int CurrentHealth => Leader?.CurrentHealth ?? 0;

        public int MaxHealth => Leader?.MaxHealth ?? 0;

        [DslExposable("主战者", Description = "玩家的主战者卡牌")]
        public CardInstance Leader { get; set; } = null!;

        [DslExposable("手牌", Description = "玩家手中的卡牌列表")]
        public List<CardInstance> Hand { get; set; } = new List<CardInstance>();

        [DslExposable("牌库", Description = "玩家牌库中的卡牌列表")]
        public List<CardInstance> Deck { get; set; } = new List<CardInstance>();

        [DslExposable("初始卡组", Description = "玩家进入游戏时使用的卡组（不会随对局变化）")]
        public List<CardInstance> InitialDeck { get; set; } = new List<CardInstance>();

        /// <summary>
        /// Immutable initial deck definition ids captured at game start.
        /// Used by post-match statistics to avoid runtime card mutation pollution.
        /// </summary>
        public List<int> InitialDeckDefinitionIds { get; set; } = new List<int>();

        [DslExposable("战场", Description = "玩家战场上的卡牌列表")]
        public List<CardInstance> Field { get; set; } = new List<CardInstance>();

        public int FieldOccupancyCount => Field.Count(card => card != null && !card.IsLeader);

        [DslExposable("墓地", Description = "玩家墓地中的卡牌列表")]
        public List<CardInstance> Graveyard { get; set; } = new List<CardInstance>();

        [DslExposable("墓场数", Description = "玩家墓场计数（用于死灵术等）")]
        public int GraveyardCount
        {
            get => _counters.Get(GraveyardCounterKey);
            set => _counters.Set(GraveyardCounterKey, value);
        }

        [DslExposable("血池", Description = "玩家血池计数（用于血祭等）")]
        public int BloodPool
        {
            get => _counters.Get(BloodPoolCounterKey);
            set => _counters.Set(BloodPoolCounterKey, value);
        }

        [DslExposable("连携", Description = "本次对战中自己已进入战场的从者卡张数（不包含变形）")]
        public int Rally
        {
            get => _counters.Get(RallyCounterKey);
            set => _counters.Set(RallyCounterKey, value);
        }
        public List<CardInstance> Extra { get; set; } = new List<CardInstance>();

        [DslExposable("纹章区", Description = "玩家纹章区域中的卡牌列表")]
        public List<CardInstance> Emblem { get; set; } = new List<CardInstance>();
        public int DeckCount => Deck?.Count ?? 0;

        [DslExposable("本回合已用进化权", Description = "玩家本回合是否已经消耗过手动进化权（进化与超进化共用）")]
        public bool HasUsedManualEvolutionRightThisTurn { get; private set; } = false;

        [DslExposable("本回合可进化", Description = "玩家已解锁进化且本回合尚未消耗手动进化权")]
        public bool CanEvolve
        {
            get => IsEvolutionUnlocked && !HasUsedManualEvolutionRightThisTurn;
            set => HasUsedManualEvolutionRightThisTurn = !value;
        }

        [DslExposable("本回合可超进化", Description = "玩家已解锁超进化且本回合尚未消耗手动进化权")]
        public bool CanSuperEvolve
        {
            get => IsSuperEvolutionUnlocked && !HasUsedManualEvolutionRightThisTurn;
            set => HasUsedManualEvolutionRightThisTurn = !value;
        }

        [DslExposable("变量", Description = "玩家上的系统与临时计数器")]
        public IReadOnlyDictionary<string, int> CounterMap => _counters;

        public Counters Counters => _counters;

        public PlayerState(Guid playerId)
        {
            PlayerId = playerId;
            Hand = new List<CardInstance>();
            Deck = new List<CardInstance>();
            InitialDeck = new List<CardInstance>();
            InitialDeckDefinitionIds = new List<int>();
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
            InitialDeck = new List<CardInstance>();
            InitialDeckDefinitionIds = new List<int>();
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

        public int GetSpendablePlayPoints()
        {
            return CurrentPlayPoints + (IsPpBoostArmed ? GameConstants.PlayPoints.PpBoostAmount : 0);
        }

        public bool CanAffordPlayPoints(int cost)
        {
            return Math.Max(0, cost) <= GetSpendablePlayPoints();
        }

        public DomainOperationResult SpendPlayPoints(int amount)
        {
            var requiredCost = Math.Max(0, amount);
            var result = new DomainOperationResult();

            if (requiredCost == 0)
            {
                result.Success = true;
                return result;
            }

            var spendable = GetSpendablePlayPoints();
            if (requiredCost > spendable)
            {
                result.Success = false;
                result.ErrorMessage = $"Not enough PP. Required: {requiredCost}, Available: {spendable}";
                return result;
            }

            var normalSpent = Math.Min(CurrentPlayPoints, requiredCost);
            var targetCurrent = CurrentPlayPoints - normalSpent;
            if (targetCurrent != CurrentPlayPoints)
            {
                result.GeneratedEvents.Add(new PpUpdateEvent(this, targetCurrent));
            }

            var remaining = requiredCost - normalSpent;
            if (remaining > 0)
            {
                if (!IsPpBoostArmed || remaining > GameConstants.PlayPoints.PpBoostAmount)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Not enough PP. Required: {requiredCost}, Available: {spendable}";
                    return result;
                }

                ApplyPpBoostArmedChange(false);
                ApplyPpBoostUsesChange(PpBoostUsesAvailable - 1);
                result.GeneratedEvents.Add(new PpBoostStateUpdatedEvent(this, PpBoostUsesAvailable, IsPpBoostArmed));
            }

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
                return card.CurrentZone == CardZone.Staging
                    || card.CurrentZone == CardZone.None
                    || card.CurrentZone == CardZone.Removed
                    || card.CurrentZone == CardZone.Playing;
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
            if (destinationList == null && to != CardZone.Extra && to != CardZone.None && to != CardZone.Removed && to != CardZone.Playing)
            {
                // Revert to original zone
                GetZoneList(sourceZone)?.Add(card);
                UpdateZoneIndices(sourceZone);
                return false;
            }

            card.CurrentZone = to;
            card.ZoneStayTurn = 0;
            card.MarkCombatContinuityChanged();
            if (destinationList != null)
            {
                if (index.HasValue && index.Value >= 0 && index.Value <= destinationList.Count)
                    destinationList.Insert(index.Value, card);
                else
                    destinationList.Add(card);

                UpdateZoneIndices(to);
            }
            else
            {
                card.ZoneIndex = null;
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
            result.GeneratedEvents.Add(new CardMovedEvent(card, sourceZone, destination, moveReason, destinationIndex: index));
            result.Success = true;
            return result;
        }

        private static bool HasMoveToZone(DomainOperationResult result, CardZone destination)
        {
            return result.GeneratedEvents.OfType<CardMovedEvent>().Any(e => e.ToZone == destination);
        }

        #region Internal State Mutations (Evolution)
        /// <summary>
        /// 内部方法：标记本回合已使用进化权（进化与超进化共用）。
        /// </summary>
        internal void ApplyEvolutionRightUsed()
        {
            HasUsedManualEvolutionRightThisTurn = true;
        }

        internal void ApplySuperEvolutionRightUsed()
        {
            HasUsedManualEvolutionRightThisTurn = true;
        }
        #endregion

        // Internal State Mutations for resources and flags
        internal void ApplyEvolutionPointsMaxChange(int maxEp)
        {
            MaxEvolutionPoints = Math.Max(0, maxEp);
            if (MaxEvolutionPoints > 0)
            {
                IsEvolutionUnlocked = true;
            }
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
            if (MaxSuperEvolutionPoints > 0)
            {
                IsSuperEvolutionUnlocked = true;
            }
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

        internal void ApplyPpBoostArmedChange(bool isArmed)
        {
            IsPpBoostArmed = isArmed;
        }

        internal void ApplyMulliganCompleted()
        {
            HasCompletedMulligan = true;
        }

        internal void ApplyPlayedCardCountChange(int increment)
        {
            AddCounter(ComboCounterKey, increment);
        }

        public DomainOperationResult SummonCardToField(CardInstance card, int? index = null)
        {
            if (FieldOccupancyCount >= GameConstants.Field.MaxFieldSize)
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

        public DomainOperationResult DestroyCard(CardInstance cardToDestroy, CardInstance cause, EventReasonSource reason = EventReasonSource.Ability, CardMoveReason moveReason = CardMoveReason.Destroyed)
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
            var ppResult = SpendPlayPoints(effectiveCost);
            if (!ppResult.Success)
            {
                return ppResult;
            }
            result.GeneratedEvents.AddRange(ppResult.GeneratedEvents);
            var oldCombo = Combo;
            ApplyPlayedCardCountChange(1);
            var newCombo = Combo;
            if (newCombo != oldCombo)
            {
                result.GeneratedEvents.Add(new CounterChangedEvent(this, ComboCounterKey, oldCombo, newCombo));
            }
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

        public DomainOperationResult EvolveCard(CardInstance cardToEvolve, CardInstance? evolutionSourceCard = null, bool isManualEvolution = true)
        {
            var result = new DomainOperationResult();

            // This method primarily generates events for the evolution process.
            // For manual evolution, it also marks evolution rights as used.

            if (cardToEvolve.HasKeyword(CardKeyword.BanEvolve))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Card cannot evolve due to BanEvolve." };
            }

            if (isManualEvolution)
            {
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

                // Evolution rights are updated when state-update events are committed.
                result.GeneratedEvents.Add(new EvolveStateUpdatedEvent(this, false));
                result.GeneratedEvents.Add(new SuperEvolveStateUpdatedEvent(this, false));
            }

            // 2. Generate the event for the card's transformation
            var evolveResult = cardToEvolve.ApplyEvolution(this, evolutionSourceCard, isManualEvolution);
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

                IsEvolutionUnlocked = true;
                this.MaxEvolutionPoints = newEp;
                this.CurrentEvolutionPoints = newEp;
                result.GeneratedEvents.Add(new EpMaxUpdateEvent(this, this.MaxEvolutionPoints));
                result.GeneratedEvents.Add(new EpUpdateEvent(this, this.CurrentEvolutionPoints));
            }

            result.Success = true;
            return result;
        }

        public DomainOperationResult SuperEvolveCard(CardInstance cardToEvolve, CardInstance? evolutionSourceCard = null, bool isManualEvolution = true)
        {
            var result = new DomainOperationResult();

            if (cardToEvolve.HasKeyword(CardKeyword.BanEvolve))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Card cannot super evolve due to BanEvolve." };
            }

            if (isManualEvolution)
            {
                var sepResult = ConsumeSuperEvolutionPoints(GameConstants.SuperEvolution.SuperEvolutionPointCost);
                if (!sepResult.Success)
                {
                    return sepResult;
                }
                result.GeneratedEvents.AddRange(sepResult.GeneratedEvents);

                // Evolution rights are updated when state-update events are committed.
                result.GeneratedEvents.Add(new SuperEvolveStateUpdatedEvent(this, false));

                // Super evolve counts as evolution for the turn.
                result.GeneratedEvents.Add(new EvolveStateUpdatedEvent(this, false));
            }

            var evolveResult = cardToEvolve.ApplySuperEvolution(this, evolutionSourceCard, isManualEvolution);
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

                IsSuperEvolutionUnlocked = true;
                this.MaxSuperEvolutionPoints = newSep;
                this.CurrentSuperEvolutionPoints = newSep;
                result.GeneratedEvents.Add(new SepMaxUpdateEvent(this, this.MaxSuperEvolutionPoints));
                result.GeneratedEvents.Add(new SepUpdateEvent(this, this.CurrentSuperEvolutionPoints));
            }

            result.Success = true;
            return result;
        }
        internal void ApplyTurnEnd()
        {
        }


        public DomainOperationResult EndTurn()
        {
            return new DomainOperationResult { Success = true };
        }

        public DomainOperationResult CleanupEndOfTurnState()
        {
            var result = new DomainOperationResult();
            var oldCombo = Combo;
            var wasPpBoostArmed = IsPpBoostArmed;
            SetCounter(ComboCounterKey, 0);
            IsPpBoostArmed = false;
            var newCombo = Combo;
            if (newCombo != oldCombo)
            {
                result.GeneratedEvents.Add(new CounterChangedEvent(this, ComboCounterKey, oldCombo, newCombo));
            }
            if (wasPpBoostArmed != IsPpBoostArmed)
            {
                result.GeneratedEvents.Add(new PpBoostStateUpdatedEvent(this, PpBoostUsesAvailable, IsPpBoostArmed));
            }
            result.Success = true;
            return result;
        }
        internal void ApplyTurnStart(GameState gameState)
        {
            if (MaxEvolutionPoints > 0)
            {
                IsEvolutionUnlocked = true;
            }
            if (MaxSuperEvolutionPoints > 0)
            {
                IsSuperEvolutionUnlocked = true;
            }
            HasUsedManualEvolutionRightThisTurn = false;
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
            var replacementSlots = this.Hand
                .Select((card, index) => new { Card = card, Index = index })
                .Where(slot => cardUidsToReplace.Contains(slot.Card.InstanceId))
                .OrderBy(slot => slot.Index)
                .ToList();
            var cardsToReplace = replacementSlots.Select(slot => slot.Card).ToList();

            // Per user instruction, perform direct state mutation as Mulligan is a special case.

            // 1. Move cards from Hand to Deck
            foreach (var card in cardsToReplace)
            {
                this.Hand.Remove(card);
                this.Deck.Add(card);
                card.CurrentZone = CardZone.Deck;
                card.ZoneStayTurn = 0;
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
                var insertIndex = Math.Min(replacementSlots[i].Index, this.Hand.Count);
                this.Hand.Insert(insertIndex, drawnCard);
                drawnCard.CurrentZone = CardZone.Hand;
                drawnCard.ZoneStayTurn = 0;
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

        public DomainOperationResult BuildTurnStartTransitionEvents(DelayConfig? turnStartDelayConfig = null)
        {
            var result = new DomainOperationResult();

            var ppResult = RestorePlayPoints(DelayConfig.QueueDrained());
            result.GeneratedEvents.AddRange(ppResult.GeneratedEvents);

            result.GeneratedEvents.Add(new TurnStartEvent(this, turnStartDelayConfig));
            result.Success = true;
            return result;
        }

        public DomainOperationResult StartTurn(GameContext context, DelayConfig? drawDelayConfig = null)
        {
            var gameState = context.CurrentGameState;
            if (gameState.IsGameOver)
                throw new GameLogicException("Game is already over.");
            if (gameState.Phase == GamePhase.Mulligan)
                throw new GameLogicException("Cannot start turn during mulligan phase.");
            // Internal apply for turn-start state changes
            ApplyTurnStart(gameState);

            var result = new DomainOperationResult();

            var unitResetResult = ResetCardsForTurnStart();
            result.GeneratedEvents.AddRange(unitResetResult.GeneratedEvents);

            if (context != null)
            {
                ResetAbilitiesForTurnStart(context);
            }

            if (!IsFirst && !PpBoostResetApplied && TurnNumber >= GameConstants.PlayPoints.PpBoostResetTurn)
            {
                PpBoostUsesAvailable = GameConstants.PlayPoints.PpBoostUsesPerPhase;
                PpBoostResetApplied = true;
                result.GeneratedEvents.Add(new PpBoostStateUpdatedEvent(this, PpBoostUsesAvailable, IsPpBoostArmed));
            }

            foreach (var amulet in this.Field.Where(c => c.CurrentCountdown.HasValue && c.CurrentCountdown.Value > 0))
            {
                result.GeneratedEvents.Add(new CountdownTickEvent(this, amulet, -1, drawDelayConfig));
            }

            foreach (var crest in this.Emblem.Where(c => c.CurrentCountdown.HasValue && c.CurrentCountdown.Value > 0))
            {
                result.GeneratedEvents.Add(new CountdownTickEvent(this, crest, -1, drawDelayConfig));
            }

            var epResult = CheckAndUnlockEvolutionPoints();
            result.GeneratedEvents.AddRange(epResult.GeneratedEvents);

            var sepResult = CheckAndUnlockSuperEvolutionPoints();
            result.GeneratedEvents.AddRange(sepResult.GeneratedEvents);

            int cardsToDraw = GameConstants.Hand.CardsPerTurn;
            var drawResult = DrawCards(cardsToDraw, DelayConfig.QueueDrained());
            result.GeneratedEvents.AddRange(drawResult.GeneratedEvents);

            result.Success = true;
            return result;
        }

        private void ResetAbilitiesForTurnStart(GameContext context)
        {
            var gameState = context.CurrentGameState;
            var seenAbilities = new HashSet<Guid>();

            foreach (var card in gameState.AllCards)
            {
                if (card == null || card.OwnerPlayerId != PlayerId)
                {
                    continue;
                }

                var abilities = context.AbilityManager.GetAbilitiesForCard(card.InstanceId);
                if (abilities == null || abilities.Count == 0)
                {
                    continue;
                }

                foreach (var ability in abilities)
                {
                    if (ability == null)
                    {
                        continue;
                    }

                    if (!seenAbilities.Add(ability.RuntimeId))
                    {
                        continue;
                    }

                    ability.ResetTurnState(context);
                }
            }
        }

        public DomainOperationResult RestorePlayPoints(DelayConfig? delayConfig = null)
        {
            var result = new DomainOperationResult();
            int targetMax = Math.Min(GameConstants.PlayPoints.MaxPlayPoints, this.MaxPlayPoints + 1);
            int targetCurrent = targetMax; // restore to full
            result.GeneratedEvents.Add(new PpMaxUpdateEvent(this, targetMax, delayConfig));
            result.GeneratedEvents.Add(new PpUpdateEvent(this, targetCurrent, delayConfig: delayConfig));
            return result;
        }

        public DomainOperationResult DrawCards(int drawCount, DelayConfig? delayConfig = null)
        {
            var result = new DomainOperationResult();

            if (drawCount <= 0)
            {
                result.Success = true;
                return result;
            }

            // 抽牌目标在 DrawEvent.Commit 时按当前牌库状态计算并回填，避免提前锁定实例ID导致失效。
            result.GeneratedEvents.Add(new DrawEvent(this, drawCount, delayConfig));

            result.Success = true;
            return result;
        }

        public DomainOperationResult DrawCards(List<Guid> cardInstanceIds)
        {
            var result = new DomainOperationResult();
            var projectedHandCount = this.Hand.Count;

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

                if (projectedHandCount >= GameConstants.Hand.MaxHandSize)
                {
                    var burnOpResult = MoveCard(cardToDraw, CardZone.Graveyard, null, CardMoveReason.Burned);
                    result.GeneratedEvents.AddRange(burnOpResult.GeneratedEvents);
                }
                else
                {
                    var addOpResult = MoveCard(cardToDraw, CardZone.Hand, null, CardMoveReason.Drawn);
                    projectedHandCount++;
                    result.GeneratedEvents.AddRange(addOpResult.GeneratedEvents);
                }
            }

            result.Success = true;
            return result;
        }

        public DomainOperationResult AddCardsToHand(List<CardInstance> cardInstancesToAdd)
        {
            var result = new DomainOperationResult();
            var projectedHandCount = this.Hand.Count;
            if (cardInstancesToAdd == null)
            {
                result.Success = false;
                result.ErrorMessage = "cardInstancesToAdd cannot be null.";
                return result;
            }
            foreach (var cardInstance in cardInstancesToAdd)
            {
                if (cardInstance.CurrentZone == CardZone.Hand)
                {
                    continue;
                }
                DomainOperationResult addResult;
                if (projectedHandCount >= GameConstants.Hand.MaxHandSize)
                {
                    addResult = MoveCard(cardInstance, CardZone.Graveyard, null, CardMoveReason.Burned);
                }
                else
                {
                    addResult = MoveCard(cardInstance, CardZone.Hand, null, CardMoveReason.AddedToHand);
                    projectedHandCount++;
                }
                result.GeneratedEvents.AddRange(addResult.GeneratedEvents);
            }
            result.Success = true;
            return result;
        }

        public DomainOperationResult AddCardsToDeck(List<CardInstance> cardInstancesToAdd, DeckInsertMode insertMode = DeckInsertMode.Random)
        {
            var result = new DomainOperationResult();
            if (cardInstancesToAdd == null)
            {
                result.Success = false;
                result.ErrorMessage = "cardInstancesToAdd cannot be null.";
                return result;
            }

            var generatedExpectedMoveEvent = false;
            var projectedDeckCount = Deck.Count;
            foreach (var cardInstance in cardInstancesToAdd)
            {
                if (cardInstance.CurrentZone == CardZone.Deck)
                {
                    continue;
                }

                var insertIndex = ResolveDeckInsertIndex(insertMode, projectedDeckCount);
                var addResult = MoveCard(cardInstance, CardZone.Deck, insertIndex, CardMoveReason.AddedToDeck);
                var movedToDeck = HasMoveToZone(addResult, CardZone.Deck);
                generatedExpectedMoveEvent = generatedExpectedMoveEvent || movedToDeck;
                if (movedToDeck)
                {
                    projectedDeckCount++;
                }
                result.GeneratedEvents.AddRange(addResult.GeneratedEvents);
            }

            if (generatedExpectedMoveEvent)
            {
                result.GeneratedEvents.Add(new DeckCountChangedEvent(this, Deck.Count));
            }
            result.Success = true;
            return result;
        }

        private static int ResolveDeckInsertIndex(DeckInsertMode insertMode, int currentDeckCount)
        {
            return insertMode switch
            {
                DeckInsertMode.Top => 0,
                DeckInsertMode.Bottom => currentDeckCount,
                _ => Random.Shared.Next(0, currentDeckCount + 1)
            };
        }

        public DomainOperationResult ShuffleCardsIntoDeck(
            List<CardInstance> cardInstancesToShuffle,
            DeckInsertMode insertMode = DeckInsertMode.Random)
        {
            var result = new DomainOperationResult();
            if (cardInstancesToShuffle == null)
            {
                result.Success = false;
                result.ErrorMessage = "cardInstancesToShuffle cannot be null.";
                return result;
            }

            var generatedExpectedMoveEvent = false;
            var projectedDeckCount = Deck.Count;
            foreach (var cardInstance in cardInstancesToShuffle)
            {
                if (cardInstance.CurrentZone == CardZone.Deck)
                {
                    continue;
                }

                var insertIndex = ResolveDeckInsertIndex(insertMode, projectedDeckCount);
                var shuffleResult = MoveCard(cardInstance, CardZone.Deck, insertIndex, CardMoveReason.ShuffledIntoDeck);
                var movedToDeck = HasMoveToZone(shuffleResult, CardZone.Deck);
                generatedExpectedMoveEvent = generatedExpectedMoveEvent || movedToDeck;
                if (movedToDeck)
                {
                    projectedDeckCount++;
                }
                result.GeneratedEvents.AddRange(shuffleResult.GeneratedEvents);
            }

            if (generatedExpectedMoveEvent)
            {
                ShuffleDeck();
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
            foreach (var card in Deck.Concat(Hand).Concat(Graveyard).Concat(Extra).Concat(Emblem))
            {
                var hadFusionUsage = card.FusionsThisTurn > 0;
                if (hadFusionUsage)
                {
                    card.ResetActionUsage(CardActionUsageType.Fusion);
                }
                card.ZoneStayTurn++;

                if (hadFusionUsage)
                {
                    result.GeneratedEvents.Add(new FusionStateUpdatedEvent(card, card.FusionsThisTurn, card.MaxFusionsPerTurn));
                }
            }

            foreach (var unit in Field)
            {
                var oldCanAttack = unit.CanAttack;
                var hadActUsage = unit.ActsThisTurn > 0;
                var hadFusionUsage = unit.FusionsThisTurn > 0;
                unit.ResetActionUsage(CardActionUsageType.Attack);
                unit.ResetActionUsage(CardActionUsageType.Act);
                unit.ResetActionUsage(CardActionUsageType.Fusion);
                unit.ZoneStayTurn++;
                if (!oldCanAttack && unit.CanAttack)
                {
                    result.GeneratedEvents.Add(new AttackStateUpdatedEvent(unit, true));
                }
                if (hadActUsage)
                {
                    result.GeneratedEvents.Add(new ActStateUpdatedEvent(unit, unit.ActsThisTurn, unit.MaxActsPerTurn));
                }
                if (hadFusionUsage)
                {
                    result.GeneratedEvents.Add(new FusionStateUpdatedEvent(unit, unit.FusionsThisTurn, unit.MaxFusionsPerTurn));
                }
            }
            result.Success = true;
            return result;
        }

        public DomainOperationResult PreparePlayCard(
            CardInstance cardInHand,
            PlayMode playMode = PlayMode.Base,
            int? paidCost = null,
            Guid? triggeredAbilityRuntimeId = null,
            int? alternateCardDefinitionId = null)
        {
            var result = new DomainOperationResult();
            if (!this.Hand.Contains(cardInHand))
            {
                result.Success = false;
                result.ErrorMessage = $"Card {cardInHand.InstanceId} not found in hand.";
                return result;
            }
            if (cardInHand.HasKeyword(CardKeyword.CannotUse))
            {
                result.Success = false;
                result.ErrorMessage = "该卡牌处于【无法使用】状态。";
                return result;
            }
            var effectiveCost = paidCost ?? cardInHand.CurrentCost;
            var spendablePp = GetSpendablePlayPoints();
            if (effectiveCost > spendablePp)
            {
                result.Success = false;
                result.ErrorMessage = $"Not enough PP. Required: {effectiveCost}, Available: {spendablePp}";
                return result;
            }

            // The cost is now paid when the PlayCardEvent is committed, not when it's requested.
            // This prevents PP loss if the player cancels target selection.
            result.GeneratedEvents.Add(new PlayCardEvent(
                this,
                cardInHand,
                playMode,
                effectiveCost,
                triggeredAbilityRuntimeId: triggeredAbilityRuntimeId,
                alternateCardDefinitionId: alternateCardDefinitionId));
            return result;
        }

        public DomainOperationResult ExecutePlayCard(CardInstance cardInHand, Guid parentEventId, int? paidCost = null)
        {
            var result = new DomainOperationResult();
            if (cardInHand.HasKeyword(CardKeyword.CannotUse))
            {
                result.Success = false;
                result.ErrorMessage = "该卡牌处于【无法使用】状态。";
                return result;
            }

            // Pay PP cost and increment play count now that the action is confirmed.
            var effectiveCost = paidCost ?? cardInHand.CurrentCost;
            var ppResult = SpendPlayPoints(effectiveCost);
            if (!ppResult.Success)
            {
                return ppResult;
            }
            result.GeneratedEvents.AddRange(ppResult.GeneratedEvents);
            var oldCombo = Combo;
            ApplyPlayedCardCountChange(1);
            var newCombo = Combo;
            if (newCombo != oldCombo)
            {
                result.GeneratedEvents.Add(new CounterChangedEvent(this, ComboCounterKey, oldCombo, newCombo));
            }

            switch (cardInHand.CardType)
            {
                case CardType.Follower:
                case CardType.Amulet:
                    var expectedPosition = Field.Count + 1;
                    result.GeneratedEvents.Add(new SummonEvent(this, cardInHand, expectedPosition, EventReasonSource.System));
                    break;
                case CardType.Spell:
                    var toPlayingResult = MoveCard(cardInHand, CardZone.Playing, null);
                    result.GeneratedEvents.AddRange(toPlayingResult.GeneratedEvents);
                    var spellboostTargets = Hand
                        .Where(c => c.InstanceId != cardInHand.InstanceId && c.HasAbility(AbilityKeyword.SpellBoost))
                        .ToList();
                    result.GeneratedEvents.Add(new SpellBoostEvent(this, cardInHand, spellboostTargets));
                    result.GeneratedEvents.Add(new CardMovedEvent(
                        cardInHand,
                        CardZone.Playing,
                        CardZone.Graveyard,
                        CardMoveReason.SpellPlayed,
                        DelayConfig.Parent(parentEventId)));
                    break;
                default:
                    result.Success = false;
                    result.ErrorMessage = $"Unknown card type: {cardInHand.CardType}";
                    return result;
            }
            result.Success = true;
            return result;
        }

        public DomainOperationResult DiscardCard(CardInstance cardToDiscard, EventReasonSource reason)
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
                result.GeneratedEvents.Add(new LeaveFieldEvent(
                    cardToReturn,
                    LeaveFieldKind.Return,
                    EventReasonSource.Ability,
                    null,
                    CardZone.Hand));
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
                if (FieldOccupancyCount >= GameConstants.Field.MaxFieldSize)
                {
                    break;
                }
                var summonResult = SummonCardToField(tokenInstance);
                result.GeneratedEvents.AddRange(summonResult.GeneratedEvents);
            }
            return result;
        }

        public DomainOperationResult VanishCard(CardInstance cardToVanish, EventReasonSource vanishReason = EventReasonSource.Ability)
        {
            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.Add(new LeaveFieldEvent(
                cardToVanish,
                LeaveFieldKind.Vanish,
                vanishReason));
            return result;
        }

        public List<CardInstance> GetCardsInZone(CardZone zone)
        {
            var zoneList = GetZoneList(zone);
            return zoneList != null ? new List<CardInstance>(zoneList) : new List<CardInstance>();
        }

        public int GetCounter(string key)
        {
            return _counters.Get(key);
        }

        public int SetCounter(string key, int value)
        {
            return _counters.Set(key, value);
        }

        public int AddCounter(string key, int delta)
        {
            return _counters.Add(key, delta);
        }

        public bool TryConsumeCounter(string key, int amount)
        {
            return _counters.TryConsume(key, amount);
        }

    }
}
