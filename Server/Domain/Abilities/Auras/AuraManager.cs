using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Auras
{
    public class AuraManager : IAuraManager
    {
        private sealed class AuraInstance
        {
            public Guid AuraId { get; }
            public string AuraKey { get; }
            public Guid SourceAbilityId { get; }
            public Guid? SourceCardId { get; }
            public int AuraIndex { get; }
            public IReadOnlyList<BuffConfig> BuffConfigs { get; }
            public HashSet<Guid> ActiveTargets { get; } = new();

            public AuraInstance(
                Guid auraId,
                string auraKey,
                Guid sourceAbilityId,
                Guid? sourceCardId,
                int auraIndex,
                IReadOnlyList<BuffConfig> buffConfigs)
            {
                AuraId = auraId;
                AuraKey = auraKey;
                SourceAbilityId = sourceAbilityId;
                SourceCardId = sourceCardId;
                AuraIndex = auraIndex;
                BuffConfigs = buffConfigs;
            }
        }

        private static readonly HashSet<ModifierLayer> AllowedStatLayers = new()
        {
            ModifierLayer.Base,
            ModifierLayer.Additive,
            ModifierLayer.Multiplicative,
            ModifierLayer.Set
        };

        private static readonly HashSet<ModifierLayer> AllowedAbilityKeywordLayers = new()
        {
            ModifierLayer.Base,
            ModifierLayer.Additive,
            ModifierLayer.Multiplicative,
            ModifierLayer.Set
        };

        private readonly ILogger<AuraManager> _logger;
        private readonly IValueSourceResolver _valueSourceResolver;
        private readonly IBuffService _buffService;
        private readonly IBuffFactory _buffFactory;
        private readonly Dictionary<Guid, AuraInstance> _aurasById = new();
        private readonly Dictionary<string, Guid> _auraIdsByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, HashSet<Guid>> _auraIdsBySourceAbilityId = new();
        private readonly Dictionary<Guid, HashSet<Guid>> _auraIdsBySourceCardId = new();

        public AuraManager(
            ILogger<AuraManager> logger,
            IValueSourceResolver valueSourceResolver,
            IBuffService buffService,
            IBuffFactory buffFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _valueSourceResolver = valueSourceResolver ?? throw new ArgumentNullException(nameof(valueSourceResolver));
            _buffService = buffService ?? throw new ArgumentNullException(nameof(buffService));
            _buffFactory = buffFactory ?? throw new ArgumentNullException(nameof(buffFactory));
        }

        public DomainOperationResult RegisterAura(AuraConfig auraConfig, IAbility sourceAbility, GameContext context, int auraIndex)
        {
            var result = new DomainOperationResult { Success = true };
            if (auraConfig == null)
            {
                result.Success = false;
                result.ErrorMessage = "Aura registration failed: config is null.";
                return result;
            }

            if (sourceAbility == null)
            {
                result.Success = false;
                result.ErrorMessage = "Aura registration failed: source ability is null.";
                return result;
            }

            if (!auraConfig.IsEnabled)
            {
                return result;
            }

            var sourceAbilityId = sourceAbility?.RuntimeId
                ?? context.CurrentEffect?.sourceAbilityId
                ?? Guid.Empty;
            var sourceCard = context.SourceCard ?? sourceAbility?.SourceCard;
            var sourceCardId = sourceCard?.InstanceId ?? sourceAbility?.SourceCardId;

            var auraKey = BuildAuraKey(sourceAbilityId, auraConfig, auraIndex);
            if (_auraIdsByKey.TryGetValue(auraKey, out var existingId) && _aurasById.ContainsKey(existingId))
            {
                return result;
            }

            var buffConfigs = auraConfig.BuffConfigs ?? new List<BuffConfig>();
            if (buffConfigs.Count == 0)
            {
#pragma warning disable CS0618
                buffConfigs = BuffConfigAdapter.FromTemplate(auraConfig.BuffTemplate);
#pragma warning restore CS0618
            }

            if (buffConfigs == null || buffConfigs.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "Aura registration failed: no BuffConfig entries provided.";
                return result;
            }

            foreach (var config in buffConfigs)
            {
                if (!ValidateBuffConfig(config, out var validationError))
                {
                    result.Success = false;
                    result.ErrorMessage = validationError ?? "Aura registration failed: invalid buff config.";
                    return result;
                }
            }

            var auraId = Guid.NewGuid();

            var aura = new AuraInstance(
                auraId,
                auraKey,
                sourceAbilityId,
                sourceCardId,
                auraIndex,
                buffConfigs);

            _aurasById[auraId] = aura;
            _auraIdsByKey[auraKey] = auraId;
            IndexAura(aura);

            return result;
        }

        public DomainOperationResult RefreshAura(
            AuraConfig auraConfig,
            IAbility sourceAbility,
            GameContext context,
            int auraIndex,
            IReadOnlyDictionary<string, List<TargetInfo>> resolvedTargets)
        {
            var result = new DomainOperationResult { Success = true };
            if (auraConfig == null)
            {
                return result;
            }

            var registerResult = RegisterAura(auraConfig, sourceAbility, context, auraIndex);
            result.Merge(registerResult);
            if (!registerResult.Success)
            {
                return registerResult;
            }

            var auraKey = BuildAuraKey(sourceAbility.RuntimeId, auraConfig, auraIndex);
            if (!_auraIdsByKey.TryGetValue(auraKey, out var auraId) || !_aurasById.TryGetValue(auraId, out var aura))
            {
                return result;
            }

            if (sourceAbility is Ability ability && !IsSourceZoneValidForAura(ability, auraConfig, context))
            {
                var unregisterResult = UnregisterAuraInternal(aura, context);
                result.Merge(unregisterResult);
                return result;
            }

            var refreshResult = RefreshAuraInternal(aura, context, resolvedTargets);
            result.Merge(refreshResult);
            return result;
        }

        public DomainOperationResult ClearAura(AuraConfig auraConfig, IAbility sourceAbility, GameContext context, int auraIndex)
        {
            var result = new DomainOperationResult { Success = true };
            if (auraConfig == null)
            {
                return result;
            }

            var auraKey = BuildAuraKey(sourceAbility.RuntimeId, auraConfig, auraIndex);
            if (!_auraIdsByKey.TryGetValue(auraKey, out var auraId) || !_aurasById.TryGetValue(auraId, out var aura))
            {
                return result;
            }

            return ClearAuraTargets(aura, context);
        }

        public DomainOperationResult RefreshAuras(IGameEvent gameEvent, GameContext context, TriggerTiming timing)
        {
            return new DomainOperationResult { Success = true };
        }

        private static bool IsSourceZoneValidForAura(Ability ability, AuraConfig auraConfig, GameContext context)
        {
            var sourceCard = context.SourceCard ?? ability.SourceCard;
            if (sourceCard == null)
            {
                return true;
            }

            foreach (var trigger in GetAuraRefreshTriggers(ability, auraConfig))
            {
                if (trigger == null || !trigger.IsEnabled)
                {
                    continue;
                }

                if (trigger.IsValidZone(sourceCard.CurrentZone))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<TriggerConfig> GetAuraRefreshTriggers(Ability ability, AuraConfig auraConfig)
        {
            return new[] { ability.Config.Trigger };
        }

        public DomainOperationResult UnregisterBySourceCard(Guid sourceCardId, GameContext context)
        {
            var result = new DomainOperationResult { Success = true };
            if (sourceCardId == Guid.Empty)
            {
                return result;
            }

            if (!_auraIdsBySourceCardId.TryGetValue(sourceCardId, out var auraIds))
            {
                _logger.LogDebug("[AuraManager] UnregisterBySourceCard: no auras found for SourceCardId {SourceCardId}.", sourceCardId);
                return result;
            }

            _logger.LogDebug("[AuraManager] UnregisterBySourceCard: removing {AuraCount} auras for SourceCardId {SourceCardId}.", auraIds.Count, sourceCardId);
            foreach (var auraId in auraIds.ToList())
            {
                if (_aurasById.TryGetValue(auraId, out var aura))
                {
                    result.Merge(UnregisterAuraInternal(aura, context));
                }
            }

            return result;
        }

        public DomainOperationResult UnregisterBySourceAbilities(IEnumerable<Guid> abilityRuntimeIds, GameContext context)
        {
            var result = new DomainOperationResult { Success = true };
            if (abilityRuntimeIds == null)
            {
                return result;
            }

            foreach (var abilityId in abilityRuntimeIds.Distinct())
            {
                if (abilityId == Guid.Empty)
                {
                    continue;
                }

                if (!_auraIdsBySourceAbilityId.TryGetValue(abilityId, out var auraIds))
                {
                    continue;
                }

                foreach (var auraId in auraIds.ToList())
                {
                    if (_aurasById.TryGetValue(auraId, out var aura))
                    {
                        result.Merge(UnregisterAuraInternal(aura, context));
                    }
                }
            }

            return result;
        }

        private DomainOperationResult RefreshAuraInternal(
            AuraInstance aura,
            GameContext context,
            IReadOnlyDictionary<string, List<TargetInfo>> resolvedTargets)
        {
            var result = new DomainOperationResult { Success = true };
            var sourceAbility = ResolveSourceAbility(context, aura.SourceAbilityId);
            var sourceCard = ResolveSourceCard(context, aura.SourceCardId, sourceAbility);

            if (aura.SourceAbilityId != Guid.Empty && sourceAbility == null)
            {
                _logger.LogDebug("Aura {AuraId} removed because source ability {AbilityId} is missing.", aura.AuraId, aura.SourceAbilityId);
                return UnregisterAuraInternal(aura, context);
            }

            if (aura.SourceCardId.HasValue && sourceCard == null)
            {
                _logger.LogDebug("Aura {AuraId} removed because source card {CardId} is missing.", aura.AuraId, aura.SourceCardId);
                return UnregisterAuraInternal(aura, context);
            }

            var auraContext = context with
            {
                SourceCard = sourceCard,
                SourceAbility = sourceAbility,
                CurrentEffect = null,
                ResolvedTargets = resolvedTargets ?? new Dictionary<string, List<TargetInfo>>()
            };

            if (sourceCard != null && sourceCard.IsSilenced)
            {
                return ClearAuraTargets(aura, auraContext);
            }

            var effectTargets = resolvedTargets.Values.SelectMany(list => list).ToList();
            var effectTargetCards = effectTargets
                .Where(target => target.Type == TargetType.Card && target.Card != null)
                .Select(target => target.Card!)
                .ToList();

            var currentTargetIds = new HashSet<Guid>(effectTargetCards.Select(card => card.InstanceId));

            NormalizeActiveTargets(aura, aura.ActiveTargets, auraContext);

            var addedTargets = currentTargetIds.Except(aura.ActiveTargets).ToList();
            var removedTargets = aura.ActiveTargets.Except(currentTargetIds).ToList();

            foreach (var targetId in addedTargets)
            {
                var card = effectTargetCards.FirstOrDefault(c => c.InstanceId == targetId);
                if (card == null)
                {
                    continue;
                }

                var applyResult = ApplyAuraBuffToTarget(aura, card, auraContext);
                if (!applyResult.Success)
                {
                    return applyResult;
                }
                result.Merge(applyResult);
            }

            foreach (var targetId in removedTargets)
            {
                var removeResult = RemoveAuraBuffFromTarget(aura, targetId, auraContext);
                if (!removeResult.Success)
                {
                    return removeResult;
                }
                result.Merge(removeResult);
            }

            aura.ActiveTargets.Clear();
            foreach (var targetId in currentTargetIds)
            {
                aura.ActiveTargets.Add(targetId);
            }

            return result;
        }

        private void IndexAura(AuraInstance aura)
        {
            if (aura.SourceAbilityId != Guid.Empty)
            {
                if (!_auraIdsBySourceAbilityId.TryGetValue(aura.SourceAbilityId, out var abilitySet))
                {
                    abilitySet = new HashSet<Guid>();
                    _auraIdsBySourceAbilityId[aura.SourceAbilityId] = abilitySet;
                }
                abilitySet.Add(aura.AuraId);
            }

            if (aura.SourceCardId.HasValue)
            {
                if (!_auraIdsBySourceCardId.TryGetValue(aura.SourceCardId.Value, out var cardSet))
                {
                    cardSet = new HashSet<Guid>();
                    _auraIdsBySourceCardId[aura.SourceCardId.Value] = cardSet;
                }
                cardSet.Add(aura.AuraId);
            }
        }

        private DomainOperationResult UnregisterAuraInternal(AuraInstance aura, GameContext context)
        {
            _logger.LogDebug("[AuraManager] UnregisterAuraInternal: aura {AuraId} key {AuraKey} sourceAbility {SourceAbilityId} sourceCard {SourceCardId} index {AuraIndex}.",
                aura.AuraId, aura.AuraKey, aura.SourceAbilityId, aura.SourceCardId, aura.AuraIndex);
            _aurasById.Remove(aura.AuraId);
            _auraIdsByKey.Remove(aura.AuraKey);
            RemoveAuraIndexes(aura);
            return RemoveAuraBuffsByPrefix(aura, context);
        }

        private void RemoveAuraIndexes(AuraInstance aura)
        {
            if (aura.SourceAbilityId != Guid.Empty && _auraIdsBySourceAbilityId.TryGetValue(aura.SourceAbilityId, out var abilitySet))
            {
                abilitySet.Remove(aura.AuraId);
                if (abilitySet.Count == 0)
                {
                    _auraIdsBySourceAbilityId.Remove(aura.SourceAbilityId);
                }
            }

            if (aura.SourceCardId.HasValue && _auraIdsBySourceCardId.TryGetValue(aura.SourceCardId.Value, out var cardSet))
            {
                cardSet.Remove(aura.AuraId);
                if (cardSet.Count == 0)
                {
                    _auraIdsBySourceCardId.Remove(aura.SourceCardId.Value);
                }
            }
        }

        private static IAbility? ResolveSourceAbility(GameContext context, Guid sourceAbilityId)
        {
            if (sourceAbilityId == Guid.Empty)
            {
                return null;
            }

            return context.AbilityManager.GetAbilityByRuntimeId(sourceAbilityId);
        }

        private static CardInstance? ResolveSourceCard(GameContext context, Guid? sourceCardId, IAbility? sourceAbility)
        {
            if (sourceAbility?.SourceCard != null)
            {
                return sourceAbility.SourceCard;
            }

            if (sourceCardId.HasValue)
            {
                return context.CurrentGameState.FindCardInstance(sourceCardId.Value);
            }

            return null;
        }

        private void NormalizeActiveTargets(
            AuraInstance aura,
            HashSet<Guid> activeTargets,
            GameContext context)
        {
            if (activeTargets.Count == 0)
            {
                return;
            }

            var toRemove = new List<Guid>();
            foreach (var targetId in activeTargets)
            {
                var buffList = context.BuffManager.GetBuffsForCard(targetId);
                if (!buffList.Any(buff =>
                        buff.SourceAbilityId == aura.SourceAbilityId
                        && buff.SourceEffectIndex == aura.AuraIndex))
                {
                    toRemove.Add(targetId);
                }
            }

            foreach (var targetId in toRemove)
            {
                activeTargets.Remove(targetId);
            }
        }

        private DomainOperationResult ApplyAuraBuffToTarget(
            AuraInstance aura,
            CardInstance target,
            GameContext context)
        {
            var sourceAbility = context.GetCurrentAbility();
            var sourceCard = context.SourceCard;
            var ownerPlayer = context.CurrentGameState.GetPlayerState(sourceCard?.OwnerPlayerId ?? context.RequestingPlayerId);
            var turnNumber = ownerPlayer?.TurnNumber ?? 0;
            var result = new DomainOperationResult { Success = true };

            foreach (var config in aura.BuffConfigs)
            {
                if (!TryBuildBuffEffects(config, context, out var effects, out var errorMessage))
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = errorMessage };
                }

                if (effects.Count == 0)
                {
                    continue;
                }

                var durationType = config.DurationType;
                if (!TryResolveDurationParameter(config.DurationParameter, context, out var durationParameter, out var durationError))
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = durationError ?? "Aura registration failed: invalid duration parameter."
                    };
                }
                var canStack = config.CanStack;

                Buff buff;
                if (sourceAbility == null)
                {
                    buff = new Buff(Guid.Empty, target.InstanceId, turnNumber, effects, durationType: durationType, durationParameter: durationParameter);
                }
                else
                {
                    buff = _buffFactory.CreateCompositeBuff(
                        target,
                        sourceCard,
                        sourceAbility,
                        effects,
                        durationType,
                        durationParameter,
                        turnNumber,
                        string.IsNullOrWhiteSpace(config.Key) ? null : config.Key.Trim());
                }

                buff.SourceEffectIndex = aura.AuraIndex;
                _logger.LogDebug("[AuraManager] ApplyAuraBuffToTarget: aura {AuraId} -> target {TargetId}. SourceAbilityId={SourceAbilityId} SourceCardId={SourceCardId} EffectIndex={EffectIndex}.",
                    aura.AuraId,
                    target.InstanceId,
                    buff.SourceAbilityId,
                    buff.SourceCardInstanceId,
                    buff.SourceEffectIndex);
                var applyResult = _buffService.ApplyBuffProcess(buff, context, canStack);
                result.Merge(applyResult);
                if (!applyResult.Success)
                {
                    return applyResult;
                }
            }

            return result;
        }

        private DomainOperationResult RemoveAuraBuffFromTarget(
            AuraInstance aura,
            Guid targetId,
            GameContext context)
        {
            return _buffService.RemoveAllBuffs(targetId, context, buff =>
                buff.SourceAbilityId == aura.SourceAbilityId
                && buff.SourceEffectIndex == aura.AuraIndex);
        }

        private bool TryBuildBuffEffects(
            BuffConfig config,
            GameContext context,
            out List<IBuffEffect> effects,
            out string? errorMessage)
        {
            effects = new List<IBuffEffect>();
            errorMessage = null;

            var modifications = new List<StatModification>();
            foreach (var statConfig in config.StatModifiers ?? new List<StatModifierConfig>())
            {
                if (statConfig == null || statConfig.Value == null)
                {
                    continue;
                }

                if (!AllowedStatLayers.Contains(statConfig.Layer))
                {
                    errorMessage = $"Unsupported stat modifier layer: {statConfig.Layer}.";
                    return false;
                }

                var resolved = _valueSourceResolver.Resolve(statConfig.Value, context);
                var resolvedValue = CoerceToNullableDouble(resolved);
                if (!resolvedValue.HasValue)
                {
                    continue;
                }

                if (ShouldApplyStat(resolvedValue, statConfig.Layer))
                {
                    modifications.Add(new StatModification(statConfig.Stat, resolvedValue.Value, statConfig.Layer));
                }
            }

            if (modifications.Count > 0)
            {
                effects.Add(new StatBuffEffect(modifications));
            }

            var abilityKeywordModifications = new List<AbilityKeywordModification>();
            foreach (var abilityKeywordConfig in config.AbilityKeywordModifiers ?? new List<AbilityKeywordModifierConfig>())
            {
                if (abilityKeywordConfig == null || abilityKeywordConfig.Value == null)
                {
                    continue;
                }

                if (!AllowedAbilityKeywordLayers.Contains(abilityKeywordConfig.Layer))
                {
                    errorMessage = $"Unsupported ability keyword modifier layer: {abilityKeywordConfig.Layer}.";
                    return false;
                }

                if (abilityKeywordConfig.Keyword == AbilityKeyword.None)
                {
                    continue;
                }

                var resolved = _valueSourceResolver.Resolve(abilityKeywordConfig.Value, context);
                var resolvedValue = CoerceToNullableDouble(resolved);
                if (!resolvedValue.HasValue)
                {
                    continue;
                }

                if (ShouldApplyStat(resolvedValue, abilityKeywordConfig.Layer))
                {
                    abilityKeywordModifications.Add(new AbilityKeywordModification(
                        abilityKeywordConfig.Keyword,
                        resolvedValue.Value,
                        abilityKeywordConfig.Layer));
                }
            }

            if (abilityKeywordModifications.Count > 0)
            {
                effects.Add(new AbilityKeywordBuffEffect(abilityKeywordModifications));
            }

            var addedKeywords = new List<CardKeyword>();
            var removedKeywords = new List<CardKeyword>();

            foreach (var keywordConfig in config.KeywordModifiers ?? new List<KeywordModifierConfig>())
            {
                if (keywordConfig == null || keywordConfig.Keyword == CardKeyword.None)
                {
                    continue;
                }

                if (keywordConfig.Operation == KeywordOperation.Remove)
                {
                    removedKeywords.Add(keywordConfig.Keyword);
                }
                else
                {
                    addedKeywords.Add(keywordConfig.Keyword);
                }
            }

            if (addedKeywords.Count > 0)
            {
                effects.Add(new KeywordBuffEffect(addedKeywords));
            }

            if (removedKeywords.Count > 0)
            {
                effects.Add(new RemoveKeywordBuffEffect(removedKeywords));
            }

            var addedTribes = (config.TribeModifiers ?? new List<TribeModifierConfig>())
                .Where(static tribeConfig => tribeConfig != null && !string.IsNullOrWhiteSpace(tribeConfig.Tribe))
                .Select(static tribeConfig => tribeConfig.Tribe.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (addedTribes.Count > 0)
            {
                effects.Add(new TribeBuffEffect(addedTribes));
            }

            if (config.GrantedAbilities != null && config.GrantedAbilities.Count > 0)
            {
                var abilityConfigs = config.GrantedAbilities.Where(config => config != null).ToList();
                if (abilityConfigs.Count > 0)
                {
                    effects.Add(new AbilityBuffEffect(abilityConfigs));
                }
            }

            return true;
        }

        private DomainOperationResult ClearAuraTargets(AuraInstance aura, GameContext context)
        {
            aura.ActiveTargets.Clear();
            return RemoveAuraBuffsByPrefix(aura, context);
        }

        private DomainOperationResult RemoveAuraBuffsByPrefix(AuraInstance aura, GameContext context)
        {
            var result = new DomainOperationResult { Success = true };
            foreach (var card in context.CurrentGameState.AllCards)
            {
                var buffs = context.BuffManager.GetBuffsForCard(card.InstanceId);
                var buffsToRemove = buffs
                    .Where(buff => buff.SourceAbilityId == aura.SourceAbilityId
                                   && buff.SourceEffectIndex == aura.AuraIndex)
                    .Select(buff => buff.Id)
                    .ToHashSet();

                if (buffsToRemove.Count == 0)
                {
                    continue;
                }

                var removeResult = _buffService.RemoveAllBuffs(card.InstanceId, context, buff => buffsToRemove.Contains(buff.Id));
                result.Merge(removeResult);
                if (!removeResult.Success)
                {
                    return removeResult;
                }
            }

            return result;
        }

        private static bool ShouldApplyStat(double? value, ModifierLayer layer)
        {
            if (!value.HasValue)
            {
                return false;
            }

            return value.Value != 0 || layer == ModifierLayer.Set;
        }

        private static string BuildAuraKey(Guid sourceAbilityId, AuraConfig auraConfig, int auraIndex)
        {
            var keyPart = !string.IsNullOrWhiteSpace(auraConfig.Name)
                ? auraConfig.Name.Trim()
                : $"idx:{auraIndex}";
            return $"{sourceAbilityId:N}:{keyPart}";
        }


        private static bool ValidateBuffConfig(BuffConfig? config, out string? errorMessage)
        {
            errorMessage = null;
            if (config == null)
            {
                errorMessage = "Aura registration failed: buff config is missing.";
                return false;
            }

            if (config.DurationParameter is StaticValueConfig staticDuration &&
                TryCoerceToCeilInt(staticDuration.Value, out var staticDurationValue) &&
                staticDurationValue < 0)
            {
                errorMessage = "Aura registration failed: duration parameter cannot be negative.";
                return false;
            }

            var hasEffects = false;

            foreach (var statConfig in config.StatModifiers ?? new List<StatModifierConfig>())
            {
                if (statConfig == null)
                {
                    continue;
                }

                if (!AllowedStatLayers.Contains(statConfig.Layer))
                {
                    errorMessage = $"Aura registration failed: unsupported stat modifier layer {statConfig.Layer}.";
                    return false;
                }

                if (statConfig.Value == null)
                {
                    errorMessage = "Aura registration failed: stat modifier value is required.";
                    return false;
                }

                hasEffects = true;
            }

            foreach (var keywordConfig in config.KeywordModifiers ?? new List<KeywordModifierConfig>())
            {
                if (keywordConfig == null)
                {
                    continue;
                }

                if (keywordConfig.Keyword == CardKeyword.None)
                {
                    errorMessage = "Aura registration failed: keyword modifier requires a keyword.";
                    return false;
                }

                hasEffects = true;
            }

            foreach (var tribeConfig in config.TribeModifiers ?? new List<TribeModifierConfig>())
            {
                if (tribeConfig == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(tribeConfig.Tribe))
                {
                    errorMessage = "Aura registration failed: tribe modifier requires a non-empty tribe.";
                    return false;
                }

                hasEffects = true;
            }

            foreach (var abilityKeywordConfig in config.AbilityKeywordModifiers ?? new List<AbilityKeywordModifierConfig>())
            {
                if (abilityKeywordConfig == null)
                {
                    continue;
                }

                if (!AllowedAbilityKeywordLayers.Contains(abilityKeywordConfig.Layer))
                {
                    errorMessage = $"Aura registration failed: unsupported ability keyword modifier layer {abilityKeywordConfig.Layer}.";
                    return false;
                }

                if (abilityKeywordConfig.Keyword == AbilityKeyword.None)
                {
                    errorMessage = "Aura registration failed: ability keyword modifier requires a keyword.";
                    return false;
                }

                if (abilityKeywordConfig.Value == null)
                {
                    errorMessage = "Aura registration failed: ability keyword modifier value is required.";
                    return false;
                }

                hasEffects = true;
            }

            if (config.GrantedAbilities != null && config.GrantedAbilities.Any())
            {
                hasEffects = true;
            }

            if (!hasEffects)
            {
                errorMessage = "Aura registration failed: buff template has no effects.";
                return false;
            }

            return true;
        }

        private bool TryResolveDurationParameter(
            ValueConfig? durationParameterConfig,
            GameContext context,
            out int? durationParameter,
            out string? errorMessage)
        {
            durationParameter = 0;
            errorMessage = null;

            if (durationParameterConfig == null)
            {
                return true;
            }

            var resolved = _valueSourceResolver.Resolve(durationParameterConfig, context);
            if (!TryCoerceToCeilInt(resolved, out var parsed))
            {
                errorMessage = "Aura registration failed: duration parameter must resolve to an integer.";
                return false;
            }

            if (parsed < 0)
            {
                errorMessage = "Aura registration failed: duration parameter cannot be negative.";
                return false;
            }

            durationParameter = parsed;
            return true;
        }

        private static bool TryCoerceToCeilInt(object? value, out int result)
        {
            result = 0;
            var number = CoerceToNullableDouble(value);
            if (!number.HasValue)
            {
                return false;
            }

            result = (int)Math.Ceiling(number.Value);
            return true;
        }

        private static double? CoerceToNullableDouble(object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return longValue;
            }

            if (value is double doubleValue)
            {
                return doubleValue;
            }

            if (value is float floatValue)
            {
                return floatValue;
            }

            if (value is decimal decimalValue)
            {
                return (double)decimalValue;
            }

            if (value is string text && double.TryParse(text, out var parsed))
            {
                return parsed;
            }

            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetDouble(out var number))
                {
                    return number;
                }
                if (jsonElement.ValueKind == JsonValueKind.String && double.TryParse(jsonElement.GetString(), out var jsonParsed))
                {
                    return jsonParsed;
                }
            }

            return null;
        }
    }
}
