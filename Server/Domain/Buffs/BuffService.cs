using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    /// <summary>
    /// Implements the logic for calculating card stats and keywords based on active buffs.
    /// </summary>
    public class BuffService : IBuffService
    {
        private sealed class DerivedStateCacheEntry
        {
            public long RefreshCycleId { get; init; }
            public int Attack { get; init; }
            public int MaxHealth { get; init; }
            public int Cost { get; init; }
            public bool WasSilenced { get; init; }
            public List<CardKeyword> Keywords { get; init; } = [];
        }

        private readonly IBuffManager _buffManager;
        private readonly IBuffEvaluator _buffEvaluator;
        private readonly IBuffFactory _buffFactory;
        private readonly ILogger<BuffService> _logger;
        private readonly BuffStackingEngine _stackingEngine;
        private readonly Dictionary<string, Dictionary<Guid, DerivedStateCacheEntry>> _derivedStateCacheByGameId = new();

        public BuffService(IBuffManager buffManager, IBuffEvaluator buffEvaluator, IBuffFactory buffFactory, ILogger<BuffService> logger, BuffStackingEngine stackingEngine)
        {
            _buffManager = buffManager;
            _buffEvaluator = buffEvaluator;
            _buffFactory = buffFactory;
            _logger = logger;
            _stackingEngine = stackingEngine;
        }

        public int GetEffectiveAttack(CardInstance card, GameContext gameContext)
        {
            var derived = GetDerivedState(card, gameContext);
            return derived.Attack;
        }

        public int GetEffectiveMaxHealth(CardInstance card, GameContext gameContext)
        {
            var derived = GetDerivedState(card, gameContext);
            return derived.MaxHealth;
        }

        public int GetEffectiveCost(CardInstance card, GameContext gameContext)
        {
            var derived = GetDerivedState(card, gameContext);
            return derived.Cost;
        }

        public List<CardKeyword> GetEffectiveKeywords(CardInstance card, GameContext gameContext)
        {
            if (card.IsSilenced)
            {
                return [];
            }

            var derived = GetDerivedState(card, gameContext);
            return derived.Keywords;
        }

        private DerivedStateCacheEntry GetDerivedState(CardInstance card, GameContext gameContext)
        {
            var gameState = gameContext.CurrentGameState;
            if (!_derivedStateCacheByGameId.TryGetValue(gameState.GameId, out var cache))
            {
                cache = new Dictionary<Guid, DerivedStateCacheEntry>();
                _derivedStateCacheByGameId[gameState.GameId] = cache;
            }

            if (cache.TryGetValue(card.InstanceId, out var cached) &&
                cached.RefreshCycleId == gameState.RefreshCycleId &&
                cached.WasSilenced == card.IsSilenced)
            {
                return cached;
            }

            var computed = ComputeDerivedState(card, gameContext, gameState.RefreshCycleId);
            cache[card.InstanceId] = computed;
            return computed;
        }

        private DerivedStateCacheEntry ComputeDerivedState(CardInstance card, GameContext gameContext, long refreshCycleId)
        {
            var attack = card.BaseAttack;
            var maxHealth = card.BaseHealth;
            var cost = card.BaseCost;
            var grantedKeywords = new HashSet<CardKeyword>(card.IsSilenced ? [] : card.BaseKeywords);
            var suppressedKeywords = new HashSet<CardKeyword>();

            var orderedBuffs = ResolveBuffManager(gameContext).GetBuffsForCard(card.InstanceId);
            foreach (var buff in orderedBuffs)
            {
                if (!_buffEvaluator.IsApplicable(buff, card, gameContext))
                {
                    continue;
                }

                var attackMods = buff.GetStatModifications()
                    .Where(m => m.Stat == StatType.Attack)
                    .OrderBy(m => m.Layer);
                foreach (var mod in attackMods)
                {
                    attack = ApplyStatModification(attack, mod);
                }

                var healthMods = buff.GetStatModifications()
                    .Where(m => m.Stat == StatType.Defense)
                    .OrderBy(m => m.Layer);
                foreach (var mod in healthMods)
                {
                    maxHealth = ApplyStatModification(maxHealth, mod);
                }

                var costMods = buff.GetStatModifications()
                    .Where(m => m.Stat == StatType.Cost)
                    .OrderBy(m => m.Layer);
                foreach (var mod in costMods)
                {
                    cost = ApplyStatModification(cost, mod);
                }

                if (!card.IsSilenced)
                {
                    foreach (var effect in buff.Effects)
                    {
                        if (effect is KeywordBuffEffect addEffect)
                        {
                            foreach (var keyword in addEffect.GrantedKeywords)
                            {
                                grantedKeywords.Add(keyword);
                            }
                        }
                        else if (effect is RemoveKeywordBuffEffect removeEffect)
                        {
                            foreach (var keyword in removeEffect.KeywordsToRemove)
                            {
                                suppressedKeywords.Add(keyword);
                            }
                        }
                    }
                }
            }

            if (!card.IsSilenced)
            {
                grantedKeywords.ExceptWith(suppressedKeywords);
            }

            return new DerivedStateCacheEntry
            {
                RefreshCycleId = refreshCycleId,
                Attack = Math.Max(0, attack),
                MaxHealth = Math.Max(0, maxHealth),
                Cost = Math.Max(0, cost),
                WasSilenced = card.IsSilenced,
                Keywords = card.IsSilenced ? [] : [.. grantedKeywords]
            };
        }

        private static int ApplyStatModification(int currentValue, StatModification modification)
        {
            return modification.Layer switch
            {
                ModifierLayer.Additive => currentValue + modification.Value,
                ModifierLayer.Multiplicative => (int)(currentValue * (modification.Value / 100.0)),
                ModifierLayer.Set => modification.Value,
                ModifierLayer.Base => modification.Value,
                _ => currentValue,
            };
        }

        public void RefreshDerivedStateCache(GameContext context, string reason)
        {
            var gameState = context.CurrentGameState;
            var refreshCycleId = gameState.AdvanceRefreshCycle();
            var cache = new Dictionary<Guid, DerivedStateCacheEntry>();

            foreach (var card in gameState.AllCards)
            {
                cache[card.InstanceId] = ComputeDerivedState(card, context, refreshCycleId);
            }

            _derivedStateCacheByGameId[gameState.GameId] = cache;
            _logger.LogDebug("Derived-state cache refreshed for game {GameId} (cycle {CycleId}, reason={Reason}).",
                gameState.GameId, refreshCycleId, reason);
        }

        public void InvalidateDerivedState(GameState gameState, Guid cardInstanceId)
        {
            if (cardInstanceId == Guid.Empty)
            {
                return;
            }

            if (_derivedStateCacheByGameId.TryGetValue(gameState.GameId, out var cache))
            {
                cache.Remove(cardInstanceId);
            }
        }

        public IReadOnlyList<BuffRemovedEvent> CollectEndOfTurnExpiryEvents(GameContext context)
        {
            var events = new List<BuffRemovedEvent>();
            foreach (var card in context.CurrentGameState.AllCards)
            {
                var buffs = ResolveBuffManager(context).GetBuffsForCard(card.InstanceId);
                foreach (var buff in buffs)
                {
                    if (buff.DurationType != DurationType.EndOfTurn)
                    {
                        continue;
                    }

                    var sourceCard = ResolveSourceCard(context, buff.SourceAbilityId);
                    events.Add(new BuffRemovedEvent(buff, sourceCard, "duration_end_of_turn"));
                }
            }

            return events;
        }

        public IReadOnlyList<BuffRemovedEvent> CollectStartOfNextTurnExpiryEvents(GameContext context, PlayerState startingPlayer)
        {
            var events = new List<BuffRemovedEvent>();
            foreach (var card in context.CurrentGameState.AllCards)
            {
                if (card.OwnerPlayerId != startingPlayer.PlayerId)
                {
                    continue;
                }

                var buffs = ResolveBuffManager(context).GetBuffsForCard(card.InstanceId);
                foreach (var buff in buffs)
                {
                    if (buff.DurationType != DurationType.StartOfNextTurn)
                    {
                        continue;
                    }

                    var sourceCard = ResolveSourceCard(context, buff.SourceAbilityId);
                    events.Add(new BuffRemovedEvent(buff, sourceCard, "duration_start_of_next_turn"));
                }
            }

            return events;
        }
        public DomainOperationResult ApplyKeywordBuff(
            GameContext context,
            CardInstance target,
            List<CardKeyword> keywords,
            Domain.Abilities.Common.DurationType durationType,
            object? durationParameter,
            bool isNonStackable = false)
        {
            var sourceAbility = context.GetCurrentAbility();
            if (context.SourceCard == null || sourceAbility == null)
            {
                var requestingPlayer = context.CurrentGameState.GetPlayerState(context.RequestingPlayerId);
                var currentTurn = requestingPlayer?.TurnNumber ?? 0;

                var systemKeywordBuff = _buffFactory.CreateSystemKeywordBuff(
                    target: target,
                    keywords: keywords,
                    durationType: durationType,
                    durationParameter: durationParameter,
                    currentTurn: currentTurn,
                    isNonStackable: isNonStackable);

                return ApplyBuffProcess(systemKeywordBuff, context);
            }

            var sourcePlayer = context.CurrentGameState.GetPlayerState(context.SourceCard.OwnerPlayerId);
            if (sourcePlayer == null)
            {
                throw new InvalidOperationException($"Could not find player state for owner {context.SourceCard.OwnerPlayerId}");
            }

            var keywordBuff = _buffFactory.CreateKeywordBuff(
                target: target,
                sourceCard: context.SourceCard,
                sourceAbility: sourceAbility,
                keywords: keywords,
                durationType: durationType,
                durationParameter: durationParameter,
                currentTurn: sourcePlayer.TurnNumber,
                isNonStackable: isNonStackable
            );

            return ApplyBuffProcess(keywordBuff, context);
        }

        public DomainOperationResult ApplyRemoveKeywordBuff(
            GameContext context,
            CardInstance target,
            List<CardKeyword> keywordsToRemove)
        {
            var sourceAbilityToRemove = context.GetCurrentAbility();
            if (context.SourceCard == null || sourceAbilityToRemove == null)
            {
                var requestingPlayer = context.CurrentGameState.GetPlayerState(context.RequestingPlayerId);
                var currentTurn = requestingPlayer?.TurnNumber ?? 0;

                var systemRemoveKeywordBuff = _buffFactory.CreateSystemRemoveKeywordBuff(
                    target: target,
                    keywordsToRemove: keywordsToRemove,
                    currentTurn: currentTurn);

                return ApplyBuffProcess(systemRemoveKeywordBuff, context);
            }

            var sourcePlayer = context.CurrentGameState.GetPlayerState(context.SourceCard.OwnerPlayerId);
            if (sourcePlayer == null)
            {
                throw new InvalidOperationException($"Could not find player state for owner {context.SourceCard.OwnerPlayerId}");
            }

            var removeKeywordBuff = _buffFactory.CreateRemoveKeywordBuff(
                target: target,
                sourceCard: context.SourceCard,
                sourceAbility: sourceAbilityToRemove,
                keywordsToRemove: keywordsToRemove,
                currentTurn: sourcePlayer.TurnNumber
            );

            return ApplyBuffProcess(removeKeywordBuff, context);
        }

        public DomainOperationResult ApplyStatBuff(
            GameContext context,
            CardInstance target,
            List<StatModification> modifications,
            Domain.Abilities.Common.DurationType durationType,
            object? durationParameter,
            bool isNonStackable = false)
        {
            if (target == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ApplyStatBuff: target is null." };
            }

            if (modifications == null || modifications.Count == 0)
            {
                return new DomainOperationResult { Success = true };
            }

            var sourceAbilityForStat = context.GetCurrentAbility();

            // Determine a turn number for buff bookkeeping.
            // Prefer source card owner; otherwise fall back to requesting player.
            var ownerPlayerId = context.SourceCard?.OwnerPlayerId ?? target.OwnerPlayerId;
            var ownerPlayer = context.CurrentGameState.GetPlayerState(ownerPlayerId)
                ?? context.CurrentGameState.GetPlayerState(context.RequestingPlayerId);
            var currentTurn = ownerPlayer?.TurnNumber ?? 0;

            if (sourceAbilityForStat == null)
            {
                // System/no-source path: create a permanent buff without a source ability.
                var systemBuff = _buffFactory.CreateSystemStatBuff(
                    target: target,
                    statModifications: modifications,
                    durationType: durationType,
                    durationParameter: durationParameter,
                    currentTurn: currentTurn,
                    isNonStackable: isNonStackable);

                var readableBuffId = context.CurrentGameState.InGameIds.Format(InGameIdKind.Buff, systemBuff.Id);
                var readableTargetId = context.CurrentGameState.InGameIds.Format(InGameIdKind.Card, target.InstanceId);
                _logger.LogInformation("[BuffService] System stat buff {BuffId} (guid={BuffGuid}, SourceAbilityId=EMPTY) -> Target {CardId} (guid={CardGuid})",
                    readableBuffId, systemBuff.Id, readableTargetId, target.InstanceId);

                return ApplyBuffProcess(systemBuff, context);
            }

            var statBuff = _buffFactory.CreateStatBuff(
                target: target,
                sourceCard: context.SourceCard,
                sourceAbility: sourceAbilityForStat,
                statModifications: modifications,
                durationType: durationType,
                durationParameter: durationParameter,
                currentTurn: currentTurn,
                isNonStackable: isNonStackable
            );

            return ApplyBuffProcess(statBuff, context);
        }

        public DomainOperationResult ApplyBuffProcess(Buff buff, GameContext context)
        {
            var result = new DomainOperationResult();
            if (buff == null)
            {
                _logger.LogWarning("Attempted to process a null buff.");
                return new DomainOperationResult { Success = false, ErrorMessage = "Buff cannot be null." };
            }

            var cardId = buff.TargetCardInstanceId;
            var buffList = ResolveBuffManager(context).GetBuffsForCard(cardId);

            var stackingResult = _stackingEngine.EvaluateStacking(buff, buffList);

            if (!stackingResult.AllowAdd)
            {
                _logger.LogInformation("Buff {BuffId} application rejected: {Reason}", buff.Id, stackingResult.Reason);
                return new DomainOperationResult { Success = false, ErrorMessage = stackingResult.Reason };
            }

            // Generate events for removing buffs that need to be replaced
            foreach (var buffToRemove in stackingResult.BuffsToRemove)
            {
                var sourceCard = ResolveSourceCard(context, buffToRemove.SourceAbilityId);

                // If sourceCard is null, it might be a system-generated buff without a card source.
                // We still need to generate the removal event.
                result.GeneratedEvents.Add(new BuffRemovedEvent(buffToRemove, sourceCard, "replaced"));
                _logger.LogDebug("Generated BuffRemovedEvent for Buff {BuffId} from Card {CardInstanceId} to make room for new buff", buffToRemove.Id, cardId);
            }

            // Generate the event for adding the new buff
            var sourceCardForNewBuff = ResolveSourceCard(context, buff.SourceAbilityId);

            result.GeneratedEvents.Add(new BuffAppliedEvent(buff, sourceCardForNewBuff));
            _logger.LogDebug("Generated BuffAppliedEvent for Buff {BuffId} for Card {CardInstanceId}. It will replace {ReplacedCount} buffs.", buff.Id, cardId, stackingResult.BuffsToRemove.Count);

            result.Success = true;
            return result;
        }

        public DomainOperationResult RemoveBuffProcess(Guid buffId, GameContext context)
        {
            var result = new DomainOperationResult();
            if (buffId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to generate a removal event for a buff with an empty ID.");
                return new DomainOperationResult { Success = false, ErrorMessage = "Buff ID cannot be empty." };
            }

            // Find the buff in the manager, but do not remove it here.
            var buffToRemove = ResolveBuffManager(context).FindBuffInstance(buffId);

            if (buffToRemove != null)
            {
                _logger.LogDebug("Found Buff {BuffId} on Card {CardInstanceId} for removal. Generating event.", buffId, buffToRemove.TargetCardInstanceId);

                var sourceCard = ResolveSourceCard(context, buffToRemove.SourceAbilityId);

                // Generate the event that will perform the atomic removal upon commit.
                result.GeneratedEvents.Add(new BuffRemovedEvent(buffToRemove, sourceCard, "manual"));
                result.Success = true;
                return result;
            }

            _logger.LogWarning("Buff {BuffId} not found for removal event generation.", buffId);
            return new DomainOperationResult { Success = false, ErrorMessage = $"Buff with ID {buffId} not found." };
        }
        public DomainOperationResult RemoveBuffsFromSource(long sourceAbilityId, GameContext context)
        {
            var result = new DomainOperationResult { Success = true };

            // This logic is complex because it iterates through all cards.
            // Consider if a reverse lookup dictionary is needed for performance.
            var allCardIdsWithBuffs = context.CurrentGameState.AllCards.Select(c => c.InstanceId).ToList();

            foreach (var cardId in allCardIdsWithBuffs)
            {
                var buffList = ResolveBuffManager(context).GetBuffsForCard(cardId);
                var buffsFromSource = buffList
                    .Where(b =>
                    {
                        var ability = ResolveSourceAbility(context, b.SourceAbilityId);
                        return ability != null && ability.DefinitionId == sourceAbilityId;
                    })
                    .ToList();

                if (buffsFromSource.Any())
                {
                    _logger.LogDebug("Removing {Count} buffs from Card {CardInstanceId} originating from SourceAbility DefinitionId {SourceAbilityDefId}.",
                        buffsFromSource.Count, cardId, sourceAbilityId);

                    foreach (var buffToRemove in buffsFromSource)
                    {
                        ResolveBuffManager(context).UnregisterBuffInstance(buffToRemove.Id);
                        var sourceCardSourceRemoved = ResolveSourceCard(context, buffToRemove.SourceAbilityId);
                        if (sourceCardSourceRemoved != null)
                        {
                            result.GeneratedEvents.Add(new BuffRemovedEvent(buffToRemove, sourceCardSourceRemoved, "source_removed"));
                        }
                    }

                    InvalidateDerivedState(context.CurrentGameState, cardId);
                }
            }

            return result;
        }

        public DomainOperationResult RemoveAllBuffs(Guid cardInstanceId, GameContext context, Func<Buff, bool>? predicate = null)
        {
            var result = new DomainOperationResult { Success = true };
            if (cardInstanceId == Guid.Empty)
            {
                result.Success = false;
                result.ErrorMessage = "CardInstanceId cannot be empty.";
                return result;
            }

            var buffList = ResolveBuffManager(context).GetBuffsForCard(cardInstanceId);
            if (buffList.Any())
            {
                var buffsToRemove = predicate == null ? buffList.ToList() : buffList.Where(predicate).ToList();

                if (buffsToRemove.Any())
                {
                    foreach (var buffToRemove in buffsToRemove)
                    {
                        ResolveBuffManager(context).UnregisterBuffInstance(buffToRemove.Id);
                        var sourceCardAllRemoved = ResolveSourceCard(context, buffToRemove.SourceAbilityId);
                        if (sourceCardAllRemoved != null)
                        {
                            result.GeneratedEvents.Add(new BuffRemovedEvent(buffToRemove, sourceCardAllRemoved, "removed_all"));
                        }
                    }
                    _logger.LogDebug("Removed {Count} buffs from Card {CardInstanceId}.", buffsToRemove.Count, cardInstanceId);
                    InvalidateDerivedState(context.CurrentGameState, cardInstanceId);
                }
            }
            else
            {
                _logger.LogDebug("No buffs found to remove for Card {CardInstanceId}.", cardInstanceId);
            }

            return result;
        }

        private IBuffManager ResolveBuffManager(GameContext context)
        {
            return context.BuffManager ?? _buffManager;
        }

        private static IAbility? ResolveSourceAbility(GameContext context, Guid sourceAbilityId)
        {
            if (sourceAbilityId == Guid.Empty)
            {
                return null;
            }

            return context.AbilityManager.GetAbilityByRuntimeId(sourceAbilityId);
        }

        private static CardInstance? ResolveSourceCard(GameContext context, Guid sourceAbilityId)
        {
            var ability = ResolveSourceAbility(context, sourceAbilityId);
            if (ability == null)
            {
                return null;
            }

            if (ability.SourceCard != null)
            {
                return ability.SourceCard;
            }

            if (ability.SourceCardId.HasValue)
            {
                return context.CurrentGameState.FindCardInstance(ability.SourceCardId.Value);
            }

            return null;
        }
    }
}
