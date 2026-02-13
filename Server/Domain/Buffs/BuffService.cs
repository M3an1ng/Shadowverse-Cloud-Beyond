using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities;
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

        public int GetEffectiveAbilityKeywordValue(CardInstance card, AbilityKeyword keyword, int baseValue, GameContext gameContext)
        {
            var value = baseValue;
            var orderedBuffs = ResolveBuffManager(gameContext).GetBuffsForCard(card.InstanceId);

            foreach (var buff in orderedBuffs)
            {
                if (!_buffEvaluator.IsApplicable(buff, card, gameContext))
                {
                    continue;
                }

                foreach (var effect in buff.Effects.OfType<AbilityKeywordBuffEffect>())
                {
                    var modifiers = effect.Modifications
                        .Where(m => m.Keyword == keyword)
                        .OrderBy(m => m.Layer);

                    foreach (var mod in modifiers)
                    {
                        value = ApplyKeywordModification(value, mod);
                    }
                }
            }

            return Math.Max(0, value);
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
                    .Where(m => m.Stat == StatType.Health)
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
                Attack = attack,
                MaxHealth = maxHealth,
                Cost = cost,
                WasSilenced = card.IsSilenced,
                Keywords = card.IsSilenced ? [] : [.. grantedKeywords]
            };
        }

        private static int ApplyStatModification(int currentValue, StatModification modification)
        {
            return modification.Layer switch
            {
                ModifierLayer.Additive => currentValue + (int)modification.Value,
                ModifierLayer.Multiplicative => CeilToInt(currentValue * modification.Value),
                ModifierLayer.Set => CeilToInt(modification.Value),
                ModifierLayer.Base => CeilToInt(modification.Value),
                _ => currentValue,
            };
        }

        private static int ApplyKeywordModification(int currentValue, AbilityKeywordModification modification)
        {
            return modification.Layer switch
            {
                ModifierLayer.Additive => currentValue + (int)modification.Value,
                ModifierLayer.Set => CeilToInt(modification.Value),
                ModifierLayer.Multiplicative => CeilToInt(currentValue * modification.Value),
                ModifierLayer.Base => CeilToInt(modification.Value),
                _ => currentValue,
            };
        }

        private static int CeilToInt(double value) => (int)Math.Ceiling(value);

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
            var currentPlayerId = context.CurrentGameState.CurrentPlayerId;
            foreach (var card in context.CurrentGameState.AllCards)
            {
                var buffs = ResolveBuffManager(context).GetBuffsForCard(card.InstanceId);
                foreach (var buff in buffs)
                {
                    if (buff.DurationType != DurationType.EndOfTurn
                        && buff.DurationType != DurationType.OwnerTurnEnd
                        && buff.DurationType != DurationType.OpponentTurnEnd)
                    {
                        continue;
                    }

                    if (buff.DurationType == DurationType.OwnerTurnEnd || buff.DurationType == DurationType.OpponentTurnEnd)
                    {
                        if (!currentPlayerId.HasValue)
                        {
                            continue;
                        }

                        var isOwnerTurn = card.OwnerPlayerId == currentPlayerId.Value;
                        if (buff.DurationType == DurationType.OwnerTurnEnd && !isOwnerTurn)
                        {
                            continue;
                        }

                        if (buff.DurationType == DurationType.OpponentTurnEnd && isOwnerTurn)
                        {
                            continue;
                        }
                    }

                    var remaining = NormalizeDurationParameter(buff.DurationParameter);
                    if (remaining <= 1)
                    {
                        var sourceCard = ResolveSourceCard(context, buff.SourceAbilityId);
                        var reason = buff.DurationType switch
                        {
                            DurationType.OwnerTurnEnd => "duration_owner_turn_end",
                            DurationType.OpponentTurnEnd => "duration_opponent_turn_end",
                            _ => "duration_end_of_turn"
                        };
                        events.Add(new BuffRemovedEvent(buff, sourceCard, reason));
                        continue;
                    }

                    buff.DurationParameter = remaining - 1;
                }
            }

            return events;
        }

        public IReadOnlyList<BuffRemovedEvent> CollectStartOfTurnExpiryEvents(GameContext context, PlayerState startingPlayer)
        {
            var events = new List<BuffRemovedEvent>();
            foreach (var card in context.CurrentGameState.AllCards)
            {
                var buffs = ResolveBuffManager(context).GetBuffsForCard(card.InstanceId);
                foreach (var buff in buffs)
                {
                    if (buff.DurationType != DurationType.StartOfTurn
                        && buff.DurationType != DurationType.OwnerTurnStart
                        && buff.DurationType != DurationType.OpponentTurnStart)
                    {
                        continue;
                    }

                    if (buff.DurationType == DurationType.OwnerTurnStart || buff.DurationType == DurationType.OpponentTurnStart)
                    {
                        var isOwnerTurn = card.OwnerPlayerId == startingPlayer.PlayerId;
                        if (buff.DurationType == DurationType.OwnerTurnStart && !isOwnerTurn)
                        {
                            continue;
                        }

                        if (buff.DurationType == DurationType.OpponentTurnStart && isOwnerTurn)
                        {
                            continue;
                        }
                    }

                    var remaining = NormalizeDurationParameter(buff.DurationParameter);
                    if (remaining <= 1)
                    {
                        var sourceCard = ResolveSourceCard(context, buff.SourceAbilityId);
                        var reason = buff.DurationType switch
                        {
                            DurationType.OwnerTurnStart => "duration_owner_turn_start",
                            DurationType.OpponentTurnStart => "duration_opponent_turn_start",
                            _ => "duration_start_of_turn"
                        };
                        events.Add(new BuffRemovedEvent(buff, sourceCard, reason));
                        continue;
                    }

                    buff.DurationParameter = remaining - 1;
                }
            }

            return events;
        }

        private static int NormalizeDurationParameter(int? durationParameter)
        {
            return durationParameter.HasValue && durationParameter.Value > 0
                ? durationParameter.Value
                : 1;
        }
        public DomainOperationResult ApplyKeywordBuff(
            GameContext context,
            CardInstance target,
            List<CardKeyword> keywords,
            Domain.Abilities.Common.DurationType durationType,
            int? durationParameter,
            string? effectKey = null,
            bool canStack = true)
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
                    currentTurn: currentTurn);

                if (!string.IsNullOrWhiteSpace(effectKey) && systemKeywordBuff.SourceAbilityId != Guid.Empty)
                {
                    systemKeywordBuff.EffectKey = effectKey;
                }

                return ApplyBuffProcess(systemKeywordBuff, context, canStack);
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
                currentTurn: sourcePlayer.TurnNumber
            );

            if (!string.IsNullOrWhiteSpace(effectKey) && keywordBuff.SourceAbilityId != Guid.Empty)
            {
                keywordBuff.EffectKey = effectKey;
            }

            return ApplyBuffProcess(keywordBuff, context, canStack);
        }

        public DomainOperationResult ApplyRemoveKeywordBuff(
            GameContext context,
            CardInstance target,
            List<CardKeyword> keywordsToRemove,
            DurationType durationType,
            int? durationParameter,
            string? effectKey = null,
            bool canStack = true)
        {
            var sourceAbilityToRemove = context.GetCurrentAbility();
            _logger.LogDebug(
                "ApplyRemoveKeywordBuff: Target={TargetId} Keywords=[{Keywords}] DurationType={DurationType} DurationParameter={DurationParameter} EffectKey={EffectKey} CanStack={CanStack} SourceAbilityId={SourceAbilityId}",
                target?.InstanceId,
                string.Join(",", keywordsToRemove ?? new List<CardKeyword>()),
                durationType,
                durationParameter,
                effectKey ?? "<null>",
                canStack,
                sourceAbilityToRemove?.RuntimeId ?? Guid.Empty);

            if (context.SourceCard == null || sourceAbilityToRemove == null)
            {
                var requestingPlayer = context.CurrentGameState.GetPlayerState(context.RequestingPlayerId);
                var currentTurn = requestingPlayer?.TurnNumber ?? 0;

                var systemRemoveKeywordBuff = _buffFactory.CreateSystemRemoveKeywordBuff(
                    target: target,
                    keywordsToRemove: keywordsToRemove,
                    durationType: durationType,
                    durationParameter: durationParameter,
                    currentTurn: currentTurn);

                if (!string.IsNullOrWhiteSpace(effectKey) && systemRemoveKeywordBuff.SourceAbilityId != Guid.Empty)
                {
                    systemRemoveKeywordBuff.EffectKey = effectKey;
                }

                return ApplyBuffProcess(systemRemoveKeywordBuff, context, canStack);
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
                durationType: durationType,
                durationParameter: durationParameter,
                currentTurn: sourcePlayer.TurnNumber
            );
            if (!string.IsNullOrWhiteSpace(effectKey) && removeKeywordBuff.SourceAbilityId != Guid.Empty)
            {
                removeKeywordBuff.EffectKey = effectKey;
            }

            return ApplyBuffProcess(removeKeywordBuff, context, canStack);
        }

        public DomainOperationResult ApplyStatBuff(
            GameContext context,
            CardInstance target,
            List<StatModification> modifications,
            Domain.Abilities.Common.DurationType durationType,
            int? durationParameter,
            string? effectKey = null,
            bool canStack = true)
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
                    currentTurn: currentTurn);

                var readableBuffId = context.CurrentGameState.InGameIds.Format(InGameIdKind.Buff, systemBuff.Id);
                var readableTargetId = context.CurrentGameState.InGameIds.Format(InGameIdKind.Card, target.InstanceId);
                _logger.LogInformation("[BuffService] System stat buff {BuffId} (guid={BuffGuid}, SourceAbilityId=EMPTY) -> Target {CardId} (guid={CardGuid})",
                    readableBuffId, systemBuff.Id, readableTargetId, target.InstanceId);

                if (!string.IsNullOrWhiteSpace(effectKey) && systemBuff.SourceAbilityId != Guid.Empty)
                {
                    systemBuff.EffectKey = effectKey;
                }

                return ApplyBuffProcess(systemBuff, context, canStack);
            }

            var statBuff = _buffFactory.CreateStatBuff(
                target: target,
                sourceCard: context.SourceCard,
                sourceAbility: sourceAbilityForStat,
                statModifications: modifications,
                durationType: durationType,
                durationParameter: durationParameter,
                currentTurn: currentTurn
            );

            if (!string.IsNullOrWhiteSpace(effectKey) && statBuff.SourceAbilityId != Guid.Empty)
            {
                statBuff.EffectKey = effectKey;
            }

            return ApplyBuffProcess(statBuff, context, canStack);
        }

        public DomainOperationResult ApplyTribeBuff(
            GameContext context,
            CardInstance target,
            List<string> tribes,
            DurationType durationType,
            int? durationParameter,
            string? effectKey = null,
            bool canStack = true)
        {
            if (target == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ApplyTribeBuff: target is null." };
            }

            var normalizedTribes = (tribes ?? new List<string>())
                .Where(static tribe => !string.IsNullOrWhiteSpace(tribe))
                .Select(static tribe => tribe.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedTribes.Count == 0)
            {
                return new DomainOperationResult { Success = true };
            }

            var sourceAbilityForTribe = context.GetCurrentAbility();
            var ownerPlayerId = context.SourceCard?.OwnerPlayerId ?? target.OwnerPlayerId;
            var ownerPlayer = context.CurrentGameState.GetPlayerState(ownerPlayerId)
                ?? context.CurrentGameState.GetPlayerState(context.RequestingPlayerId);
            var currentTurn = ownerPlayer?.TurnNumber ?? 0;

            var effects = new List<IBuffEffect> { new TribeBuffEffect(normalizedTribes) };
            Buff tribeBuff;
            if (sourceAbilityForTribe == null)
            {
                tribeBuff = new Buff(
                    sourceAbilityId: Guid.Empty,
                    targetCardInstanceId: target.InstanceId,
                    currentTurn: currentTurn,
                    effects: effects,
                    durationType: durationType,
                    durationParameter: durationParameter);
            }
            else
            {
                tribeBuff = _buffFactory.CreateCompositeBuff(
                    target: target,
                    sourceCard: context.SourceCard,
                    sourceAbility: sourceAbilityForTribe,
                    effects: effects,
                    durationType: durationType,
                    durationParameter: durationParameter,
                    currentTurn: currentTurn);
            }

            if (!string.IsNullOrWhiteSpace(effectKey) && tribeBuff.SourceAbilityId != Guid.Empty)
            {
                tribeBuff.EffectKey = effectKey;
            }

            return ApplyBuffProcess(tribeBuff, context, canStack);
        }

        public DomainOperationResult ApplyBuffProcess(Buff buff, GameContext context, bool canStack = true)
        {
            var result = new DomainOperationResult();
            if (buff == null)
            {
                _logger.LogWarning("Attempted to process a null buff.");
                return new DomainOperationResult { Success = false, ErrorMessage = "Buff cannot be null." };
            }

            var currentAbility = context.CurrentEffect?.SourceAbility ?? context.GetCurrentAbility();
            if (currentAbility != null)
            {
                if (!buff.SourceCardInstanceId.HasValue && currentAbility.SourceCardId.HasValue)
                {
                    buff.SourceCardInstanceId = currentAbility.SourceCardId.Value;
                }

                if (string.IsNullOrWhiteSpace(buff.SourceCardName))
                {
                    var sourceCardName = currentAbility.SourceCard?.Name;
                    if (string.IsNullOrWhiteSpace(sourceCardName) && buff.SourceCardInstanceId.HasValue)
                    {
                        sourceCardName = context.CurrentGameState.FindCardInstance(buff.SourceCardInstanceId.Value)?.Name;
                    }

                    if (!string.IsNullOrWhiteSpace(sourceCardName))
                    {
                        buff.SourceCardName = sourceCardName;
                    }
                }

                if (string.IsNullOrWhiteSpace(buff.SourceAbilityName))
                {
                    buff.SourceAbilityName = currentAbility.Name;
                }
            }
            else if (context.SourceCard != null)
            {
                buff.SourceCardInstanceId ??= context.SourceCard.InstanceId;
                if (string.IsNullOrWhiteSpace(buff.SourceCardName))
                {
                    buff.SourceCardName = context.SourceCard.Name;
                }
            }

            if (!buff.SourceEffectIndex.HasValue && context.CurrentEffect != null)
            {
                buff.SourceEffectIndex = context.CurrentEffect.EffectIndex;
            }

            if (!buff.SummaryRootAbilityId.HasValue || buff.SummaryRootAbilityId == Guid.Empty)
            {
                var effectRootId = context.CurrentEffect?.SummaryRootAbilityId;
                if (effectRootId.HasValue && effectRootId.Value != Guid.Empty)
                {
                    buff.SummaryRootAbilityId = effectRootId.Value;
                }
                else if (currentAbility is Ability runtimeAbility && runtimeAbility.SummaryRootAbilityId != Guid.Empty)
                {
                    buff.SummaryRootAbilityId = runtimeAbility.SummaryRootAbilityId;
                }
                else if (currentAbility != null && currentAbility.RuntimeId != Guid.Empty)
                {
                    buff.SummaryRootAbilityId = currentAbility.RuntimeId;
                }
            }

            if (string.IsNullOrWhiteSpace(buff.EffectKey) && buff.SourceAbilityId != Guid.Empty)
            {
                var effectIndex = buff.SourceEffectIndex ?? context.CurrentEffect?.EffectIndex;
                if (effectIndex.HasValue)
                {
                    buff.EffectKey = $"{buff.SourceAbilityId:N}:{effectIndex.Value}";
                }
            }

            if (string.IsNullOrWhiteSpace(buff.EffectKey))
            {
                _logger.LogInformation(
                    "[BuffService] Buff {BuffId} has empty EffectKey after assignment. Target={TargetId} SourceAbilityId={SourceAbilityId} SourceEffectIndex={SourceEffectIndex} CurrentEffectIndex={CurrentEffectIndex} CurrentEffectAbilityId={CurrentEffectAbilityId} CachedSourceCardName={SourceCardName} CachedSourceAbilityName={SourceAbilityName}",
                    buff.Id,
                    buff.TargetCardInstanceId,
                    buff.SourceAbilityId,
                    buff.SourceEffectIndex,
                    context.CurrentEffect?.EffectIndex,
                    context.CurrentEffect?.SourceAbility?.RuntimeId ?? Guid.Empty,
                    buff.SourceCardName ?? "<null>",
                    buff.SourceAbilityName ?? "<null>");
            }

            var cardId = buff.TargetCardInstanceId;
            var buffList = ResolveBuffManager(context).GetBuffsForCard(cardId);

            if (!canStack && !string.IsNullOrWhiteSpace(buff.EffectKey))
            {
                var hasSameKey = buffList.Any(existing =>
                    existing.IsActive &&
                    !string.IsNullOrWhiteSpace(existing.EffectKey) &&
                    string.Equals(existing.EffectKey, buff.EffectKey, StringComparison.Ordinal));

                if (hasSameKey)
                {
                    _logger.LogInformation("Buff {BuffId} skipped due to CanStack=false and existing EffectKey {EffectKey}.", buff.Id, buff.EffectKey);
                    return new DomainOperationResult { Success = true };
                }
            }

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
