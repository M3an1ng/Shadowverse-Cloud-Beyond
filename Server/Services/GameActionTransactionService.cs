using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // Add this for PlayerChoiceRequest
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Events.Services;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Reflection;
using System.Linq.Expressions;
using System.Globalization;
using ShadowverseBeyond.Server.Exceptions;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// Implements the service for processing player actions as atomic, cancellable transactions.
    /// </summary>
    public class GameActionTransactionService : IGameActionTransactionService
    {
        private readonly ILogger<GameActionTransactionService> _logger;
        private readonly IAbilityManager _abilityManager;
        private readonly IEventProcessor _eventProcessor;
        private readonly IPendingActionService _pendingActionService;
        private readonly IEffectExecutor _effectExecutor;
        private readonly IClientMethodMapper _clientMethodMapper;
        private readonly ICommittedEventJournal _eventJournal;
        private readonly ISnapshotStore _snapshotStore;
        private readonly IRecordedEventMapper _recordedEventMapper;
        private readonly IGameEventRawEventMapper _rawEventMapper;
        private readonly IMetaProgressionService _metaProgressionService;
        private readonly ReplayOptions _replayOptions;
        private readonly Queue<Effect> _effectQueue = new();
        private readonly Dictionary<DecisionCacheKey, PreflightDecisionState> _preflightDecisionCache = new();
        private static readonly ConcurrentDictionary<EventType, Type?> ReplacementEventTypeCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<ReplacementEventParameter>> ReplacementEventParameterCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<ReplacementConstructorBinding>> ReplacementEventConstructorCache = new();
        private static readonly ConcurrentDictionary<Type, Func<object?, (bool Ok, object? Value, string? ErrorMessage)>> ValueCoercerCache = new();
        private enum DecisionResolutionPhase
        {
            Preflight,
            Commit
        }

        private readonly record struct DecisionCacheKey(
            Guid TriggerEventId,
            int EffectIndex,
            Guid AbilityRuntimeId,
            int ExecutionIndex,
            bool IsRandom);

        private readonly record struct DecisionInputSignature(
            string OptionIdsKey,
            int SelectionCount,
            bool AllowRepeat,
            string WeightsKey);

        private sealed class PreflightDecisionState
        {
            public DecisionInputSignature Signature { get; init; }
            public List<int> SelectedIndexes { get; init; } = new();
        }

        public GameActionTransactionService(
            ILogger<GameActionTransactionService> logger,
            IAbilityManager abilityManager,
            IEventProcessor eventProcessor,
            IPendingActionService pendingActionService,
            IEffectExecutor effectExecutor,
            IClientMethodMapper clientMethodMapper,
            ICommittedEventJournal eventJournal,
            ISnapshotStore snapshotStore,
            IRecordedEventMapper recordedEventMapper,
            IGameEventRawEventMapper rawEventMapper,
            IMetaProgressionService metaProgressionService,
            IOptions<ReplayOptions> replayOptions)
        {
            _logger = logger;
            _abilityManager = abilityManager;
            _eventProcessor = eventProcessor;
            _pendingActionService = pendingActionService;
            _effectExecutor = effectExecutor;
            _clientMethodMapper = clientMethodMapper;
            _eventJournal = eventJournal;
            _snapshotStore = snapshotStore;
            _recordedEventMapper = recordedEventMapper;
            _rawEventMapper = rawEventMapper;
            _metaProgressionService = metaProgressionService;
            _replayOptions = replayOptions.Value;
        }

        /// <inheritdoc />
        public async Task<PausableTransactionResult> ProcessTransactionAsync(GameContext gameContext, IEnumerable<IGameEvent> initialEvents)
        {
            var initialEventList = initialEvents.ToList();
            _logger.LogWarning("[STATE_POLLUTION_TRACE] TransactionService: Received GameContext for GameId: {GameId} with GameState HashCode: {HashCode}", gameContext.CurrentGameState.GameId, gameContext.CurrentGameState.GetHashCode());
            _logger.LogInformation("--- Transaction Start: Processing {EventCount} initial events. ---", initialEventList.Count);

            try
            {
                _preflightDecisionCache.Clear();

                var expansionResult = ExpandRequestToActionRoots(gameContext, initialEventList);
                if (!expansionResult.Result.IsSuccess)
                {
                    return expansionResult.Result;
                }

                // Phase 1: Analyze and resolve primary abilities (Cancellable Part)
                var analysisResult = await AnalyzePrimaryAbilitiesAsync(gameContext, expansionResult.ActionRootEvents);
                if (!analysisResult.IsSuccess)
                {
                    return analysisResult;
                }
                if (analysisResult.IsCancelled)
                {
                    return analysisResult;
                }

                // Action is now locked in.

                // Phase 2: Analyze and resolve chained abilities (Committed Part)
                // Phase 2 is now integrated into the commit phase and will be handled by the recursive processing.
                // var chainedAbilitiesResult = await AnalyzeChainedAbilitiesAsync(gameContext, initialEvents);
                // if (!chainedAbilitiesResult.IsSuccess || chainedAbilitiesResult.IsPaused)
                // {
                //     return chainedAbilitiesResult; // Return if failed or paused
                // }

                // Phase 3: Commit events and handle subsequent effects
                var commitResult = await CommitTransactionAsync(gameContext, expansionResult.CommitEvents);

                return commitResult;
            }
            finally
            {
                _preflightDecisionCache.Clear();
                _abilityManager.ClearAbilityTombstones();
            }
        }

        private (PausableTransactionResult Result, List<IGameEvent> CommitEvents, List<IGameEvent> ActionRootEvents) ExpandRequestToActionRoots(
            GameContext gameContext,
            IReadOnlyList<IGameEvent> initialEvents)
        {
            var commitEvents = new List<IGameEvent>();
            var actionRootEvents = new List<IGameEvent>();

            foreach (var gameEvent in initialEvents)
            {
                if (gameEvent is IPlayerActionEvent)
                {
                    commitEvents.Add(gameEvent);
                    actionRootEvents.Add(gameEvent);
                    continue;
                }

                if (!ShouldExpandRequestEvent(gameEvent) || gameEvent is not ICommittableEvent committableEvent)
                {
                    commitEvents.Add(gameEvent);
                    continue;
                }

                var previewResult = committableEvent.Commit(gameContext);
                if (!previewResult.Success)
                {
                    return (PausableTransactionResult.Failure(previewResult.ErrorMessage ?? "Request expansion failed."), commitEvents, actionRootEvents);
                }

                var generatedRoots = previewResult.GeneratedEvents.Where(e => e is IPlayerActionEvent).ToList();
                if (!generatedRoots.Any())
                {
                    commitEvents.Add(gameEvent);
                    continue;
                }

                commitEvents.AddRange(previewResult.GeneratedEvents);
                actionRootEvents.AddRange(generatedRoots);
            }

            return (PausableTransactionResult.Success(new List<IGameEvent>()), commitEvents, actionRootEvents);
        }

        private static bool ShouldExpandRequestEvent(IGameEvent gameEvent)
        {
            return gameEvent is PlayCardRequestedEvent
                || gameEvent is AttackRequestedEvent
                || gameEvent is EvolveRequestedEvent
                || gameEvent is SuperEvolveRequestedEvent
                || gameEvent is ActRequestedEvent
                || gameEvent is FusionRequestedEvent;
        }

        private async Task<PausableTransactionResult> AnalyzePrimaryAbilitiesAsync(GameContext gameContext, IEnumerable<IGameEvent> actionRootEvents)
        {
            _logger.LogInformation("--- Phase 1: Analyzing Primary Abilities ---");

            foreach (var gameEvent in actionRootEvents)
            {
                // A primary ability is linked to a direct player action.
                if (gameEvent is IPlayerActionEvent)
                {
                    var primarySourceCard = ResolvePrimarySourceCard(gameEvent);
                    var primaryContext = gameContext with { TriggeringEvent = gameEvent, SourceCard = primarySourceCard, IsCancellable = true };

                    // --- DIAGNOSTIC LOGGING START ---
                    _logger.LogWarning("[DIAGNOSTIC] Analyzing event: {EventType}. Context SourceCard: {CardName} ({CardId})",
                        gameEvent.GetType().Name,
                        primarySourceCard?.Name ?? "NULL",
                        primarySourceCard?.InstanceId ?? Guid.Empty);
                    // --- DIAGNOSTIC LOGGING END ---

                    var triggeredAbilities = await gameContext.AbilityManager.GetTriggeredAbilitiesAsync(gameEvent, primaryContext, TriggerTiming.On)
                        ?? new List<IAbility>();

                    // --- DIAGNOSTIC LOGGING START ---
                    var triggeredAbilitiesInfo = string.Join(", ", triggeredAbilities.Select(a => $"{a.KeywordSummary} on {a.SourceCard?.Name ?? "System"}"));
                    _logger.LogWarning("[DIAGNOSTIC] Triggered abilities found: [{Abilities}]", triggeredAbilitiesInfo);
                    // --- DIAGNOSTIC LOGGING END ---

                    // According to the design doc, a "Primary Ability" is one where the ability's source
                    // is the same as the event's source. The user has clarified to use gameContext.SourceCard.
                    var primaryAbilities = triggeredAbilities.Where(ability =>
                        ability.SourceCard != null &&
                        primarySourceCard != null &&
                        ability.SourceCard.InstanceId == primarySourceCard.InstanceId
                    ).ToList();

                    // --- DIAGNOSTIC LOGGING START ---
                    var primaryAbilitiesInfo = string.Join(", ", primaryAbilities.Select(a => $"{a.KeywordSummary} on {a.SourceCard?.Name ?? "System"}"));
                    _logger.LogWarning("[DIAGNOSTIC] Filtered primary abilities: [{Abilities}]", primaryAbilitiesInfo);
                    // --- DIAGNOSTIC LOGGING END ---

                    if (!primaryAbilities.Any())
                    {
                        _logger.LogInformation("No primary abilities found for event {EventType}.", gameEvent.GetType().Name);
                        continue;
                    }

                    _logger.LogInformation("Found {Count} primary abilities for event {EventType}.", primaryAbilities.Count, gameEvent.GetType().Name);

                    foreach (var ability in primaryAbilities)
                    {
                        // Primary abilities are cancellable.
                        var abilityContext = gameContext with { SourceCard = ability.SourceCard, TriggeringEvent = gameEvent, IsCancellable = true };
                        var abilityResult = await gameContext.AbilityManager.ProcessSingleAbilityAsync(ability, abilityContext, true, false, recordTrigger: false);
                        if (abilityResult == null)
                        {
                            return PausableTransactionResult.Failure("Primary ability processing returned null result.");
                        }

                        // The IsPaused logic is now handled internally by TargetSelectionService,
                        // so ProcessSingleAbilityAsync will await until the choice is made.
                        // We only need to handle the final success or failure.

                        if (abilityResult.IsCancelled)
                        {
                            _logger.LogInformation("Primary ability '{AbilityKeywords}' was cancelled.", ability.KeywordSummary);
                            if (ability is Ability concreteAbility)
                            {
                                concreteAbility.ClearDecisionCache(gameEvent.EventId);
                            }
                            return PausableTransactionResult.Cancelled();
                        }

                        if (!abilityResult.IsSuccess)
                        {
                            _logger.LogError("Primary ability '{AbilityKeywords}' failed to process: {Error}", ability.KeywordSummary, abilityResult.ErrorMessage);
                            return PausableTransactionResult.Failure(abilityResult.ErrorMessage ?? "Primary ability processing failed.");
                        }

                        if (ability is Ability decisionConcreteAbility)
                        {
                            var preflightDecisionResult = await PreflightDecisionEffectsAsync(abilityContext, decisionConcreteAbility, gameEvent, abilityResult.Effects);
                            if (preflightDecisionResult.IsCancelled)
                            {
                                decisionConcreteAbility.ClearDecisionCache(gameEvent.EventId);
                                return preflightDecisionResult;
                            }

                            if (!preflightDecisionResult.IsSuccess)
                            {
                                return preflightDecisionResult;
                            }
                        }

                        if (ability is Ability concretePrimaryAbility)
                        {
                            concretePrimaryAbility.CachePreflightPrimaryAbilityResult(
                                gameEvent.EventId,
                                abilityResult.Effects,
                                abilityResult.GeneratedEvents);
                        }
                    }
                }
            }

            _logger.LogInformation("--- Phase 1: Primary Ability analysis completed successfully. ---");
            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private static CardInstance? ResolvePrimarySourceCard(IGameEvent gameEvent)
        {
            return gameEvent switch
            {
                PlayCardEvent playCardEvent => playCardEvent.PlayedCard,
                AttackEvent attackEvent => attackEvent.Attacker,
                EvolveEvent evolveEvent => evolveEvent.EvolvedCard,
                SuperEvolveEvent superEvolveEvent => superEvolveEvent.SuperEvolvedCard,
                ActInvokedEvent actInvokedEvent => actInvokedEvent.ActCard,
                FusionEvent fusionEvent => fusionEvent.FusionCard,
                _ => gameEvent.Source?.Card ?? gameEvent.Target.FirstOrDefault()?.Card
            };
        }

        private async Task<PausableTransactionResult> PreflightDecisionEffectsAsync(
            GameContext gameContext,
            Ability decisionAbility,
            IGameEvent triggeringEvent,
            IReadOnlyList<Effect> effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            var preflightContext = gameContext with { IsCancellable = true };
            var processedEvents = new List<IGameEvent>();

            foreach (var effect in effects)
            {
                if (effect.Type != EffectType.Choose && effect.Type != EffectType.Random)
                {
                    continue;
                }

                var sourceCard = effect.SourceCardId.HasValue
                    ? gameContext.CurrentGameState.FindCardInstance(effect.SourceCardId.Value)
                    : decisionAbility.SourceCard;

                var preflightExecutionCount = GetPreflightDecisionExecutionCount(effect);
                for (var executionIndex = 0; executionIndex < preflightExecutionCount; executionIndex++)
                {
                    effect.ExecutionIndex = executionIndex;

                    var effectContext = preflightContext with
                    {
                        SourceCard = sourceCard,
                        TriggeringEvent = triggeringEvent,
                        CurrentEffect = effect,
                        ResolvedTargets = effect.ResolvedTargets
                    };

                    var decisionPreparation = await PrepareDecisionEventAsync(effect, effectContext, executionIndex);
                    if (!decisionPreparation.Result.IsSuccess)
                    {
                        return decisionPreparation.Result;
                    }

                    if (decisionPreparation.DecisionEvent == null)
                    {
                        continue;
                    }

                    var (interceptResult, interceptedEvent) = await ApplyBeforeInterceptsAsync(effectContext, decisionPreparation.DecisionEvent, processedEvents);
                    if (!interceptResult.IsSuccess)
                    {
                        return interceptResult;
                    }

                    var decisionResolveResult = await ResolveDecisionEventIfNeededAsync(effectContext, interceptedEvent, DecisionResolutionPhase.Preflight);
                    if (decisionResolveResult.IsCancelled)
                    {
                        return decisionResolveResult;
                    }

                    if (!decisionResolveResult.IsSuccess)
                    {
                        return decisionResolveResult;
                    }

                    processedEvents.Add(interceptedEvent);
                }
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private int GetPreflightDecisionExecutionCount(Effect effect)
        {
            var repeatCount = Math.Max(0, effect.EffectConfig.RepeatCount);
            if (repeatCount == 0)
            {
                return 1;
            }

            if (effect.EffectConfig.RepeatCondition != null)
            {
                _logger.LogDebug(
                    "Decision effect {EffectType} ({EffectId}) has RepeatCondition; preflight only executes first iteration.",
                    effect.Type,
                    effect.Id);
                return 1;
            }

            if (!HasStableDecisionInputForRepeatPreflight(effect.EffectConfig))
            {
                _logger.LogDebug(
                    "Decision effect {EffectType} ({EffectId}) has non-static decision input; preflight only executes first iteration.",
                    effect.Type,
                    effect.Id);
                return 1;
            }

            return repeatCount + 1;
        }

        private static bool HasStableDecisionInputForRepeatPreflight(EffectConfig effectConfig)
        {
            return IsStaticDecisionParameter(effectConfig, "abilityConfigs")
                && IsStaticDecisionParameter(effectConfig, "selectionCount")
                && IsStaticDecisionParameter(effectConfig, "allowRepeat")
                && IsStaticDecisionParameter(effectConfig, "weights");
        }

        private static bool IsStaticDecisionParameter(EffectConfig effectConfig, string key)
        {
            if (!effectConfig.Parameters.TryGetValue(key, out var valueConfig) || valueConfig == null)
            {
                return true;
            }

            return valueConfig is StaticValueConfig;
        }

        private async Task<(PausableTransactionResult Result, List<Effect> Effects, List<IGameEvent> GeneratedEvents)> GetTriggerAbilityEffectsAsync(
            GameContext gameContext,
            IGameEvent gameEvent,
            TriggerTiming timing)
        {
            var generatedEffects = new List<Effect>();
            var generatedEvents = new List<IGameEvent>();
            var triggeredAbilities = await gameContext.AbilityManager.GetTriggeredAbilitiesAsync(gameEvent, gameContext, timing);

            // This logic is now refined. "Primary" abilities (cancellable, require choice) are handled
            // exclusively in AnalyzePrimaryAbilitiesAsync. This method should handle all other
            // triggered abilities (like standard Fanfares, Last Words, etc.).

            if (!triggeredAbilities.Any())
            {
                return (PausableTransactionResult.Success(new List<IGameEvent>()), generatedEffects, generatedEvents);
            }

            _logger.LogDebug("Found {Count} chained abilities for event {EventType} at timing: {Timing}", triggeredAbilities.Count, gameEvent.GetType().Name, timing);

            foreach (var ability in triggeredAbilities)
            {
                var abilityContext = gameContext with { SourceCard = ability.SourceCard, TriggeringEvent = gameEvent };

                if (timing == TriggerTiming.On
                    && gameEvent is IPlayerActionEvent
                    && ability is Ability concreteAbility
                    && concreteAbility.TryConsumePreflightPrimaryAbilityResult(
                        gameEvent.EventId,
                        out var preflightEffects,
                        out var preflightGeneratedEvents))
                {
                    ability.RecordTrigger(abilityContext);
                    generatedEffects.AddRange(preflightEffects);
                    if (preflightGeneratedEvents.Count > 0)
                    {
                        generatedEvents.AddRange(preflightGeneratedEvents);
                    }
                    continue;
                }

                var abilityResult = await gameContext.AbilityManager.ProcessSingleAbilityAsync(ability, abilityContext, false, true); // Triggered abilities are not cancellable

                // The IsPaused logic is now handled internally by TargetSelectionService.
                // We only need to handle the final success or failure.

                if (!abilityResult.IsSuccess)
                {
                    return (PausableTransactionResult.Failure(abilityResult.ErrorMessage ?? "Chained ability processing failed."), generatedEffects, generatedEvents);
                }

                generatedEffects.AddRange(abilityResult.Effects);
                if (abilityResult.GeneratedEvents.Count > 0)
                {
                    generatedEvents.AddRange(abilityResult.GeneratedEvents);
                }
            }

            return (PausableTransactionResult.Success(new List<IGameEvent>()), generatedEffects, generatedEvents);
        }

        private static bool IsInterceptEffect(Effect effect)
        {
            return IsInterceptEffectType(effect.Type);
        }

        private static bool IsInterceptEffectType(EffectType effectType)
        {
            return effectType == EffectType.RewriteEventParams || effectType == EffectType.ReplaceEvent;
        }

        private static void EnqueueDelayedEvent(
            Dictionary<Guid, Queue<IGameEvent>> delayedEventsByParent,
            Guid parentEventId,
            IGameEvent gameEvent)
        {
            if (!delayedEventsByParent.TryGetValue(parentEventId, out var queue))
            {
                queue = new Queue<IGameEvent>();
                delayedEventsByParent[parentEventId] = queue;
            }

            queue.Enqueue(gameEvent);
        }

        private void ReleaseDelayedEventsIfReady(
            Guid parentEventId,
            Queue<IGameEvent> eventStagingArea,
            Dictionary<Guid, Queue<IGameEvent>> delayedEventsByParent)
        {
            if (parentEventId == Guid.Empty)
            {
                return;
            }

            if (!delayedEventsByParent.TryGetValue(parentEventId, out var queue) || queue.Count == 0)
            {
                return;
            }

            if (_effectQueue.Any(effect => effect.TriggeredEventId == parentEventId))
            {
                return;
            }

            while (queue.Count > 0)
            {
                eventStagingArea.Enqueue(queue.Dequeue());
            }

            delayedEventsByParent.Remove(parentEventId);
        }

        private void ReleaseAllDelayedEventsWhenIdle(
            Queue<IGameEvent> eventStagingArea,
            Dictionary<Guid, Queue<IGameEvent>> delayedEventsByParent)
        {
            if (eventStagingArea.Count > 0 || _effectQueue.Count > 0 || delayedEventsByParent.Count == 0)
            {
                return;
            }

            foreach (var queue in delayedEventsByParent.Values)
            {
                while (queue.Count > 0)
                {
                    eventStagingArea.Enqueue(queue.Dequeue());
                }
            }

            delayedEventsByParent.Clear();
        }

        private void ReleaseQueueDrainedEventIfReady(
            Queue<IGameEvent> queueDrainedDelayedEvents,
            HashSet<Guid> readyQueueDrainedEvents,
            Queue<IGameEvent> eventStagingArea,
            Dictionary<Guid, Queue<IGameEvent>> delayedEventsByParent)
        {
            if (eventStagingArea.Count > 0 || _effectQueue.Count > 0 || delayedEventsByParent.Count > 0)
            {
                return;
            }

            if (queueDrainedDelayedEvents.Count == 0)
            {
                return;
            }

            var next = queueDrainedDelayedEvents.Dequeue();
            readyQueueDrainedEvents.Add(next.EventId);
            eventStagingArea.Enqueue(next);
        }

        private async Task<(PausableTransactionResult Result, IGameEvent FinalEvent)> ApplyBeforeInterceptsAsync(
            GameContext gameContext,
            IGameEvent initialEvent,
            List<IGameEvent> processedEvents)
        {
            var currentEvent = initialEvent;
            var interceptIteration = 0;
            var interceptedAbilityIds = new HashSet<Guid>();
            var maxIterations = GameConstants.AbilityTrigger.BeforeInterceptMaxIterations;

            while (interceptIteration < maxIterations)
            {
                bool eventChanged = false;

                var triggeredAbilities = await gameContext.AbilityManager.GetTriggeredAbilitiesAsync(currentEvent, gameContext, TriggerTiming.Before);

                foreach (var ability in triggeredAbilities)
                {
                    if (interceptedAbilityIds.Contains(ability.RuntimeId))
                    {
                        continue;
                    }

                    var abilityContext = gameContext with { SourceCard = ability.SourceCard, TriggeringEvent = currentEvent };
                    var abilityResult = await gameContext.AbilityManager.ProcessSingleAbilityAsync(
                        ability,
                        abilityContext,
                        false,
                        true,
                        recordTrigger: false);

                    if (!abilityResult.IsSuccess)
                    {
                        return (PausableTransactionResult.Failure(abilityResult.ErrorMessage ?? "Before intercept ability processing failed."), currentEvent);
                    }

                    var interceptEffects = abilityResult.Effects.Where(IsInterceptEffect).ToList();
                    if (interceptEffects.Count == 0)
                    {
                        continue;
                    }

                    interceptedAbilityIds.Add(ability.RuntimeId);
                    ability.RecordTrigger(abilityContext);

                    foreach (var interceptEffect in interceptEffects)
                    {
                        var (effectResult, actionResult) = await ExecuteInterceptEffectAsync(interceptEffect, abilityContext, processedEvents);
                        if (!effectResult.IsSuccess)
                        {
                            return (effectResult, currentEvent);
                        }

                        var intercept = actionResult?.EventIntercept;
                        if (intercept == null)
                        {
                            continue;
                        }

                        if (!TryApplyEventIntercept(gameContext, currentEvent, intercept, out var updatedEvent, out var changed, out var errorMessage))
                        {
                            return (PausableTransactionResult.Failure(errorMessage ?? "Before intercept failed to apply."), currentEvent);
                        }

                        if (changed)
                        {
                            currentEvent = updatedEvent;
                            eventChanged = true;
                            interceptIteration++;
                            break;
                        }
                    }

                    if (eventChanged)
                    {
                        break;
                    }
                }

                if (!eventChanged)
                {
                    break;
                }
            }

            if (interceptIteration >= maxIterations)
            {
                _logger.LogWarning("Before intercept reached max iterations ({MaxIterations}) for event {EventType} ({EventId}).",
                    maxIterations, currentEvent.EventType, currentEvent.EventId);
            }

            return (PausableTransactionResult.Success(new List<IGameEvent>()), currentEvent);
        }

        private async Task<PausableTransactionResult> ResolveDecisionEventIfNeededAsync(
            GameContext gameContext,
            IGameEvent gameEvent,
            DecisionResolutionPhase phase = DecisionResolutionPhase.Commit)
        {
            var resolvedChoose = gameEvent as ChooseEvent;
            var resolvedRandom = gameEvent as RandomEvent;

            if (resolvedChoose == null && resolvedRandom == null)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            if (resolvedChoose?.SelectedOptionIds.Count > 0)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            if (resolvedRandom?.SelectedOptionIds.Count > 0)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            var currentEffect = gameContext.CurrentEffect;
            if (currentEffect == null)
            {
                return PausableTransactionResult.Failure("Decision event must be processed within effect context.");
            }

            var ability = currentEffect.SourceAbility as Ability;
            if (ability == null && currentEffect.sourceAbilityId.HasValue)
            {
                ability = gameContext.AbilityManager.GetAbilityByRuntimeId(currentEffect.sourceAbilityId.Value) as Ability;
            }

            if (ability == null)
            {
                return PausableTransactionResult.Failure("Decision event requires a source ability.");
            }

            var isRandom = resolvedRandom != null;
            var executionIndex = Math.Max(0, currentEffect.ExecutionIndex);
            var optionIds = resolvedRandom != null
                ? resolvedRandom.OptionIds
                : resolvedChoose!.OptionIds;

            if (optionIds.Count == 0)
            {
                if (resolvedChoose != null)
                {
                    resolvedChoose.SetSelectedOptionIndexes(Array.Empty<int>());
                }
                else if (resolvedRandom != null)
                {
                    resolvedRandom.SetSelectedOptionIndexes(Array.Empty<int>());
                }
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            IReadOnlyList<Ability.DecisionOptionSnapshot> availableOptions;
            try
            {
                availableOptions = ability.GetDecisionOptions(currentEffect.TriggeredEventId, currentEffect.EffectIndex, isRandom, executionIndex);
            }
            catch (GameLogicException ex)
            {
                return PausableTransactionResult.Failure(ex.Message);
            }

            var optionLookup = availableOptions.ToDictionary(option => option.Id);
            var orderedOptions = new List<Ability.DecisionOptionSnapshot>(optionIds.Count);
            foreach (var optionId in optionIds)
            {
                if (!optionLookup.TryGetValue(optionId, out var option))
                {
                    return PausableTransactionResult.Failure("Decision options are out of sync with event parameters.");
                }
                orderedOptions.Add(option);
            }

            if (!TryCreateDecisionCacheKey(currentEffect, ability, isRandom, out var cacheKey))
            {
                return PausableTransactionResult.Failure("Decision cache key is invalid.");
            }

            var signature = BuildDecisionInputSignature(optionIds, resolvedChoose?.SelectionCount ?? resolvedRandom?.SelectionCount ?? 0, resolvedRandom?.AllowRepeat ?? false, resolvedRandom?.Weights);
            if (phase == DecisionResolutionPhase.Commit && _preflightDecisionCache.TryGetValue(cacheKey, out var cachedDecision))
            {
                if (cachedDecision.Signature != signature)
                {
                    return PausableTransactionResult.Failure("Decision input signature mismatch between preflight and commit.");
                }

                if (resolvedChoose != null)
                {
                    resolvedChoose.SetSelectedOptionIndexes(cachedDecision.SelectedIndexes);
                }
                else if (resolvedRandom != null)
                {
                    resolvedRandom.SetSelectedOptionIndexes(cachedDecision.SelectedIndexes);
                }

                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            if (resolvedRandom != null)
            {
                var selectionCount = Math.Max(0, resolvedRandom.SelectionCount);
                if (!resolvedRandom.AllowRepeat)
                {
                    selectionCount = Math.Min(selectionCount, optionIds.Count);
                }

                var weights = NormalizeWeights(resolvedRandom.Weights, optionIds.Count);
                var selectedIndexes = SampleWeightedIndexes(weights, selectionCount, resolvedRandom.AllowRepeat);
                resolvedRandom.SetSelectedOptionIndexes(selectedIndexes);

                if (phase == DecisionResolutionPhase.Preflight)
                {
                    _preflightDecisionCache[cacheKey] = new PreflightDecisionState
                    {
                        Signature = signature,
                        SelectedIndexes = selectedIndexes
                    };
                }
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            if (resolvedChoose != null)
            {
                var selectionCount = Math.Max(0, resolvedChoose.SelectionCount);
                if (selectionCount == 0)
                {
                    resolvedChoose.SetSelectedOptionIndexes(Array.Empty<int>());
                    return PausableTransactionResult.Success(new List<IGameEvent>());
                }

                if (optionIds.Count < selectionCount)
                {
                    return PausableTransactionResult.Failure("Not enough decision options for the required selection count.");
                }

                var request = new PlayerChoiceRequest
                {
                    PendingChoiceId = Guid.NewGuid(),
                    SourceAbilityRuntimeId = ability.RuntimeId,
                    TargetKey = "decision",
                    RequestingPlayerId = gameContext.RequestingPlayerId,
                    SelectableTargetIds = optionIds.ToList(),
                    SelectableCardInstances = Ability.BuildDecisionOptionCards(orderedOptions, gameContext.RequestingPlayerId),
                    MinSelectionCount = selectionCount,
                    MaxSelectionCount = selectionCount,
                    Message = BuildDecisionChoicePrompt(selectionCount),
                    IsCancellable = phase == DecisionResolutionPhase.Preflight
                };

                _logger.LogInformation("Awaiting player decision for ability {AbilityKeywords}.", ability.KeywordSummary);
                var choiceResult = await _pendingActionService.WaitForPlayerChoiceAsync(gameContext.CurrentGameState.GameId, request, gameContext.CancellationToken);

                if (choiceResult.WasCancelled)
                {
                    return phase == DecisionResolutionPhase.Preflight
                        ? PausableTransactionResult.Cancelled()
                        : PausableTransactionResult.Failure("Decision event cannot be cancelled during commit.");
                }

                var indexLookup = optionIds
                    .Select((id, index) => (id, index))
                    .ToDictionary(item => item.id, item => item.index);

                var chosenIds = choiceResult.ChosenTargetIds ?? new List<Guid>();
                var selectedIndexes = new List<int>(chosenIds.Count);
                foreach (var chosenId in chosenIds)
                {
                    if (!indexLookup.TryGetValue(chosenId, out var idx))
                    {
                        return PausableTransactionResult.Failure("Invalid decision selection result.");
                    }
                    selectedIndexes.Add(idx);
                }

                resolvedChoose.SetSelectedOptionIndexes(selectedIndexes);
                if (phase == DecisionResolutionPhase.Preflight)
                {
                    _preflightDecisionCache[cacheKey] = new PreflightDecisionState
                    {
                        Signature = signature,
                        SelectedIndexes = selectedIndexes
                    };
                }
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private static DecisionInputSignature BuildDecisionInputSignature(
            IReadOnlyList<Guid> optionIds,
            int selectionCount,
            bool allowRepeat,
            IReadOnlyList<double>? weights)
        {
            var optionKey = string.Join(",", optionIds.Select(id => id.ToString("N")));
            var weightsKey = weights == null
                ? string.Empty
                : string.Join(",", weights.Select(weight => weight.ToString("R", CultureInfo.InvariantCulture)));
            return new DecisionInputSignature(optionKey, selectionCount, allowRepeat, weightsKey);
        }

        private static bool TryCreateDecisionCacheKey(
            Effect currentEffect,
            Ability ability,
            bool isRandom,
            out DecisionCacheKey cacheKey)
        {
            cacheKey = default;
            if (currentEffect.TriggeredEventId == Guid.Empty || ability.RuntimeId == Guid.Empty)
            {
                return false;
            }

            cacheKey = new DecisionCacheKey(
                currentEffect.TriggeredEventId,
                currentEffect.EffectIndex,
                ability.RuntimeId,
                Math.Max(0, currentEffect.ExecutionIndex),
                isRandom);
            return true;
        }

        private static string BuildDecisionChoicePrompt(int selectionCount)
        {
            if (selectionCount <= 1)
            {
                return "请选择一个选项。";
            }

            return $"请选择{selectionCount}个选项。";
        }

        private static List<double> NormalizeWeights(IReadOnlyList<double> weights, int optionCount)
        {
            if (optionCount <= 0)
            {
                return new List<double>();
            }

            var normalized = new List<double>(optionCount);
            for (var i = 0; i < optionCount; i++)
            {
                var weight = i < weights.Count ? weights[i] : 1d;
                if (double.IsNaN(weight) || weight < 0)
                {
                    weight = 0d;
                }
                normalized.Add(weight);
            }

            if (normalized.All(value => value <= 0))
            {
                return Enumerable.Repeat(1d, optionCount).ToList();
            }

            return normalized;
        }

        private static List<int> SampleWeightedIndexes(
            IReadOnlyList<double> weights,
            int selectionCount,
            bool allowRepeat)
        {
            var results = new List<int>();
            if (selectionCount <= 0 || weights.Count == 0)
            {
                return results;
            }

            var rng = Random.Shared;
            if (allowRepeat)
            {
                for (var i = 0; i < selectionCount; i++)
                {
                    results.Add(PickWeightedIndex(weights, rng));
                }
                return results;
            }

            var remainingWeights = weights.ToList();
            var remainingIndexes = Enumerable.Range(0, weights.Count).ToList();
            var picks = Math.Min(selectionCount, remainingWeights.Count);

            for (var i = 0; i < picks; i++)
            {
                if (remainingWeights.Count == 0)
                {
                    break;
                }

                var pick = PickWeightedIndex(remainingWeights, rng);
                results.Add(remainingIndexes[pick]);
                remainingIndexes.RemoveAt(pick);
                remainingWeights.RemoveAt(pick);
            }

            return results;
        }

        private static int PickWeightedIndex(IReadOnlyList<double> weights, Random rng)
        {
            if (weights.Count == 0)
            {
                return 0;
            }

            double total = 0;
            foreach (var weight in weights)
            {
                if (weight > 0)
                {
                    total += weight;
                }
            }

            if (total <= 0)
            {
                return rng.Next(weights.Count);
            }

            var roll = rng.NextDouble() * total;
            var cumulative = 0d;
            for (var i = 0; i < weights.Count; i++)
            {
                var weight = weights[i];
                if (weight <= 0)
                {
                    continue;
                }

                cumulative += weight;
                if (roll <= cumulative)
                {
                    return i;
                }
            }

            return weights.Count - 1;
        }

        private async Task<(PausableTransactionResult Result, GameStateActionResult? ActionResult)> ExecuteInterceptEffectAsync(
            Effect effect,
            GameContext gameContext,
            List<IGameEvent> processedEvents)
        {
            var sourceCard = effect.SourceCardId.HasValue
                ? gameContext.CurrentGameState.FindCardInstance(effect.SourceCardId.Value)
                : null;

            var sourceAbility = effect.SourceAbility;
            if (sourceAbility == null && effect.sourceAbilityId.HasValue)
            {
                sourceAbility = gameContext.AbilityManager.GetAbilityByRuntimeId(effect.sourceAbilityId.Value);
            }

            if (!effect.ForceExecution && effect.sourceAbilityId.HasValue)
            {
                var ability = effect.SourceAbility ?? gameContext.AbilityManager.GetAbilityByRuntimeId(effect.sourceAbilityId.Value);
                if (ability?.TriggerConfig?.RevalidateZoneBeforeExecution == true)
                {
                    var cardToCheck = sourceCard ?? ability.SourceCard;
                    if (cardToCheck != null && !ability.TriggerConfig.IsValidZone(cardToCheck.CurrentZone))
                    {
                        _logger.LogDebug(
                            "Skipping intercept effect {EffectType} ({EffectId}) because source card {CardId} is now in zone {Zone}, which is outside ValidZones.",
                            effect.Type,
                            effect.Id,
                            cardToCheck.InstanceId,
                            cardToCheck.CurrentZone);
                        return (PausableTransactionResult.Success(new List<IGameEvent>()), null);
                    }
                }
            }

            IGameEvent? triggeringEvent = null;
            if (effect.TriggeredEventId != Guid.Empty)
            {
                triggeringEvent = processedEvents.FirstOrDefault(e => e.EventId == effect.TriggeredEventId);
            }

            if (triggeringEvent == null && gameContext.TriggeringEvent != null)
            {
                if (effect.TriggeredEventId == Guid.Empty || gameContext.TriggeringEvent.EventId == effect.TriggeredEventId)
                {
                    triggeringEvent = gameContext.TriggeringEvent;
                }
            }

            var effectContext = gameContext with
            {
                SourceCard = sourceCard,
                SourceAbility = sourceAbility,
                TriggeringEvent = triggeringEvent,
                CurrentEffect = effect,
                ResolvedTargets = effect.ResolvedTargets
            };

            var deferredResolutionResult = await ResolveDeferredTargetsIfNeededAsync(effect, effectContext);
            if (!deferredResolutionResult.IsSuccess)
            {
                return (deferredResolutionResult, null);
            }

            var actionResult = _effectExecutor.ExecuteEffect(effect, effectContext);
            if (!actionResult.Success)
            {
                switch (effect.EffectConfig.OnFailure)
                {
                    case FailureHandling.CompleteWithFailure:
                        _logger.LogWarning("Intercept effect {EffectType} failed but will continue due to OnFailure=CompleteWithFailure.", effect.Type);
                        return (PausableTransactionResult.Success(new List<IGameEvent>()), actionResult);
                    case FailureHandling.SkipEffect:
                        _logger.LogWarning("Intercept effect {EffectType} failed and was skipped due to OnFailure=SkipEffect.", effect.Type);
                        return (PausableTransactionResult.Success(new List<IGameEvent>()), null);
                    case FailureHandling.AbortAbility:
                    default:
                        var errorMessage = $"Intercept effect execution failed for {effect.Type}.";
                        _logger.LogError(errorMessage);
                        return (PausableTransactionResult.Failure(errorMessage), null);
                }
            }

            return (PausableTransactionResult.Success(new List<IGameEvent>()), actionResult);
        }

        private bool TryApplyEventIntercept(
            GameContext gameContext,
            IGameEvent currentEvent,
            EventInterceptResult intercept,
            out IGameEvent updatedEvent,
            out bool changed,
            out string? errorMessage)
        {
            updatedEvent = currentEvent;
            changed = false;
            errorMessage = null;

            switch (intercept.Kind)
            {
                case EventInterceptKind.RewriteParameters:
                    return TryApplyEventRewrite(currentEvent, intercept, out updatedEvent, out changed, out errorMessage);
                case EventInterceptKind.ReplaceEvent:
                    return TryApplyEventReplacement(gameContext, currentEvent, intercept, out updatedEvent, out changed, out errorMessage);
                default:
                    return true;
            }
        }

        private static bool TryApplyEventRewrite(
            IGameEvent currentEvent,
            EventInterceptResult intercept,
            out IGameEvent updatedEvent,
            out bool changed,
            out string? errorMessage)
        {
            updatedEvent = currentEvent;
            changed = false;
            errorMessage = null;

            var paramName = intercept.ParameterName?.Trim();
            if (string.IsNullOrWhiteSpace(paramName))
            {
                return true;
            }

            if (!EventParameterCache.TryGetRewritableBinding(currentEvent.GetType(), paramName, out var binding, out var resolveError))
            {
                // Safe no-op for unsupported parameters or scopes.
                errorMessage = resolveError;
                return true;
            }

            if (!TryCoerceValue(intercept.ParameterValue, binding.PropertyType, out var coercedValue, out var convertError))
            {
                // Safe no-op for conversion failures.
                errorMessage = convertError;
                return true;
            }

            var currentValue = binding.Getter(currentEvent);
            if (Equals(currentValue, coercedValue))
            {
                return true;
            }

            if (binding.Setter == null)
            {
                // Safe no-op for non-settable properties.
                errorMessage = $"RewriteEventParamsEffect: parameter '{binding.PropertyName}' is not settable.";
                return true;
            }

            binding.Setter(currentEvent, coercedValue);
            changed = true;
            return true;
        }

        private bool TryApplyEventReplacement(
            GameContext gameContext,
            IGameEvent currentEvent,
            EventInterceptResult intercept,
            out IGameEvent updatedEvent,
            out bool changed,
            out string? errorMessage)
        {
            updatedEvent = currentEvent;
            changed = false;
            errorMessage = null;

            if (!intercept.ReplacementEventType.HasValue)
            {
                return true;
            }

            var replacementParams = intercept.ReplacementParameters ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (!TryCreateReplacementEvent(
                gameContext,
                currentEvent,
                intercept.ReplacementEventType.Value,
                replacementParams,
                out var replacementEvent,
                out var createError))
            {
                errorMessage = createError;
                return false;
            }

            updatedEvent = replacementEvent;
            changed = true;
            return true;
        }

        private bool TryCreateReplacementEvent(
            GameContext gameContext,
            IGameEvent currentEvent,
            EventType replacementEventType,
            IReadOnlyDictionary<string, object?> replacementParams,
            out IGameEvent replacementEvent,
            out string? errorMessage)
        {
            replacementEvent = currentEvent;
            errorMessage = null;

            var eventType = ResolveReplacementEventType(replacementEventType);
            if (eventType == null)
            {
                errorMessage = $"ReplaceEventEffect: unsupported replacement event type '{replacementEventType}'.";
                return false;
            }

            var paramLookup = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in replacementParams)
            {
                paramLookup[kvp.Key] = kvp.Value;
            }

            var inputParams = GetReplacementEventParameters(eventType);
            var constructors = GetReplacementConstructors(eventType);

            foreach (var constructor in constructors)
            {
                var parameters = constructor.Parameters;
                var args = new object?[parameters.Length];
                var canUse = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    var paramName = param.Name ?? string.Empty;

                    if (TryGetReplacementParameterValue(paramLookup, inputParams, paramName, out var rawValue))
                    {
                        if (!TryCoerceValue(gameContext, rawValue, param.ParameterType, out var coercedValue, out var convertError))
                        {
                            errorMessage = convertError;
                            canUse = false;
                            break;
                        }

                        args[i] = coercedValue;
                        continue;
                    }

                    if (paramName.Equals("eventId", StringComparison.OrdinalIgnoreCase))
                    {
                        args[i] = currentEvent.EventId;
                        continue;
                    }

                    if (paramName.Equals("timestamp", StringComparison.OrdinalIgnoreCase))
                    {
                        args[i] = currentEvent.Timestamp;
                        continue;
                    }

                    if (param.HasDefaultValue)
                    {
                        args[i] = param.DefaultValue;
                        continue;
                    }

                    canUse = false;
                    break;
                }

                if (!canUse)
                {
                    continue;
                }

                try
                {
                    replacementEvent = constructor.Activator(args);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ReplaceEventEffect: failed to construct event {EventType} with constructor {Constructor}.", eventType.Name, constructor.Constructor);
                }
            }

            errorMessage = $"ReplaceEventEffect: unable to construct event '{replacementEventType}' with provided parameters.";
            return false;
        }

        private static bool TryGetReplacementParameterValue(
            Dictionary<string, object?> paramLookup,
            IReadOnlyList<ReplacementEventParameter> inputParams,
            string paramName,
            out object? rawValue)
        {
            rawValue = null;
            if (paramLookup.TryGetValue(paramName, out rawValue))
            {
                return true;
            }

            var candidate = inputParams
                .Where(p => p.PropertyName.EndsWith(paramName, StringComparison.OrdinalIgnoreCase))
                .Select(p =>
                {
                    if (paramLookup.TryGetValue(p.PropertyName, out var value))
                    {
                        return (Found: true, Value: value);
                    }

                    if (!string.IsNullOrWhiteSpace(p.DisplayName) && paramLookup.TryGetValue(p.DisplayName, out var displayValue))
                    {
                        return (Found: true, Value: displayValue);
                    }

                    return (Found: false, Value: (object?)null);
                })
                .FirstOrDefault(p => p.Found);

            if (candidate.Found)
            {
                rawValue = candidate.Value;
                return true;
            }

            return false;
        }

        private static IReadOnlyList<ReplacementEventParameter> GetReplacementEventParameters(Type eventType)
        {
            return ReplacementEventParameterCache.GetOrAdd(eventType, type =>
            {
                var descriptor = EventParameterCache.GetDescriptor(type);
                return descriptor.Parameters
                    .Where(p => p.Scope == GameEventParameterScope.Input || p.Scope == GameEventParameterScope.Both)
                    .Select(p => new ReplacementEventParameter(p.PropertyName, string.IsNullOrWhiteSpace(p.DisplayName) ? null : p.DisplayName))
                    .ToList();
            });
        }

        private static IReadOnlyList<ReplacementConstructorBinding> GetReplacementConstructors(Type eventType)
        {
            return ReplacementEventConstructorCache.GetOrAdd(eventType, type =>
            {
                return type.GetConstructors()
                    .OrderByDescending(ctor => ctor.GetParameters().Length)
                    .Select(ctor => new ReplacementConstructorBinding(ctor, ctor.GetParameters(), CreateReplacementActivator(ctor)))
                    .ToList();
            });
        }

        private static Func<object?[], IGameEvent> CreateReplacementActivator(ConstructorInfo constructor)
        {
            try
            {
                var args = Expression.Parameter(typeof(object[]), "args");
                var parameters = constructor.GetParameters();
                var argExpressions = parameters
                    .Select((param, index) =>
                        Expression.Convert(
                            Expression.ArrayIndex(args, Expression.Constant(index)),
                            param.ParameterType))
                    .ToArray();

                var newExpression = Expression.New(constructor, argExpressions);
                var castResult = Expression.Convert(newExpression, typeof(IGameEvent));
                return Expression.Lambda<Func<object?[], IGameEvent>>(castResult, args).Compile();
            }
            catch
            {
                return args => (IGameEvent)constructor.Invoke(args);
            }
        }

        private static Type? ResolveReplacementEventType(EventType replacementEventType)
        {
            return ReplacementEventTypeCache.GetOrAdd(replacementEventType, eventType =>
            {
                var eventInterfaceType = typeof(IGameEvent);

                static IEnumerable<Type> SafeGetTypes(Assembly assembly)
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        return ex.Types.Where(t => t != null)!;
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                }

                var candidates = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .SelectMany(SafeGetTypes)
                    .Where(t => t.IsClass && !t.IsAbstract && eventInterfaceType.IsAssignableFrom(t))
                    .Select(t => (Type: t, Attr: t.GetCustomAttribute<GameEventMetadataAttribute>()))
                    .Where(x => x.Attr != null && x.Attr.EventType == eventType)
                    .ToList();

                if (!candidates.Any())
                {
                    return null;
                }

                return candidates
                    .OrderByDescending(x => GetVisibilityRank(x.Attr!.Visibility))
                    .ThenBy(x => x.Type.Name, StringComparer.Ordinal)
                    .Select(x => x.Type)
                    .FirstOrDefault();
            });
        }

        private static int GetVisibilityRank(EventMetadataVisibility visibility)
        {
            return visibility switch
            {
                EventMetadataVisibility.Visible => 2,
                EventMetadataVisibility.System => 1,
                EventMetadataVisibility.Hidden => 0,
                _ => 0
            };
        }

        private static bool TryCoerceValue(
            GameContext context,
            object? value,
            Type targetType,
            out object? coercedValue,
            out string? errorMessage)
        {
            if (value != null)
            {
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                if (underlyingType == typeof(CardInstance))
                {
                    if (value is Guid cardId)
                    {
                        coercedValue = context.CurrentGameState.FindCardInstance(cardId);
                        if (coercedValue == null)
                        {
                            errorMessage = $"ReplaceEventEffect: card {cardId} not found.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }

                    if (value is string cardString && Guid.TryParse(cardString, out var parsedCardId))
                    {
                        coercedValue = context.CurrentGameState.FindCardInstance(parsedCardId);
                        if (coercedValue == null)
                        {
                            errorMessage = $"ReplaceEventEffect: card {parsedCardId} not found.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }
                }

                if (underlyingType == typeof(PlayerState))
                {
                    if (value is Guid playerId)
                    {
                        coercedValue = context.CurrentGameState.GetPlayerState(playerId);
                        if (coercedValue == null)
                        {
                            errorMessage = $"ReplaceEventEffect: player {playerId} not found.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }

                    if (value is string playerString && Guid.TryParse(playerString, out var parsedPlayerId))
                    {
                        coercedValue = context.CurrentGameState.GetPlayerState(parsedPlayerId);
                        if (coercedValue == null)
                        {
                            errorMessage = $"ReplaceEventEffect: player {parsedPlayerId} not found.";
                            return false;
                        }

                        errorMessage = null;
                        return true;
                    }
                }
            }

            return TryCoerceValue(value, targetType, out coercedValue, out errorMessage);
        }

        private sealed record ReplacementEventParameter(string PropertyName, string? DisplayName);
        private sealed record ReplacementConstructorBinding(
            ConstructorInfo Constructor,
            ParameterInfo[] Parameters,
            Func<object?[], IGameEvent> Activator);

        private static bool TryCoerceValue(object? value, Type targetType, out object? coercedValue, out string? errorMessage)
        {
            errorMessage = null;
            coercedValue = null;

            if (value == null)
            {
                if (!IsNullableType(targetType))
                {
                    errorMessage = $"RewriteEventParamsEffect: parameter type '{targetType.Name}' does not accept null.";
                    return false;
                }
                return true;
            }

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var coercer = ValueCoercerCache.GetOrAdd(underlyingType, BuildValueCoercer);
            var result = coercer(value);
            if (!result.Ok)
            {
                errorMessage = result.ErrorMessage ?? $"RewriteEventParamsEffect: failed to convert value to '{targetType.Name}'.";
                return false;
            }

            coercedValue = result.Value;
            return true;
        }

        private static bool IsNullableType(Type type)
        {
            if (!type.IsValueType)
            {
                return true;
            }

            return Nullable.GetUnderlyingType(type) != null;
        }

        private static Func<object?, (bool Ok, object? Value, string? ErrorMessage)> BuildValueCoercer(Type targetType)
        {
            if (targetType.IsEnum)
            {
                return value =>
                {
                    try
                    {
                        if (value is string enumString)
                        {
                            return (true, Enum.Parse(targetType, enumString, true), null);
                        }

                        return (true, Enum.ToObject(targetType, value!), null);
                    }
                    catch
                    {
                        return (false, null, $"RewriteEventParamsEffect: failed to convert value to '{targetType.Name}'.");
                    }
                };
            }

            if (targetType == typeof(Guid))
            {
                return value =>
                {
                    if (value is Guid guid)
                    {
                        return (true, guid, null);
                    }

                    if (value is string guidString && Guid.TryParse(guidString, out var parsedGuid))
                    {
                        return (true, parsedGuid, null);
                    }

                    return (false, null, $"RewriteEventParamsEffect: failed to convert value to '{targetType.Name}'.");
                };
            }

            return value =>
            {
                try
                {
                    if (value == null)
                    {
                        return (true, null, null);
                    }

                    if (targetType.IsInstanceOfType(value))
                    {
                        return (true, value, null);
                    }

                    var converted = System.Convert.ChangeType(value, targetType);
                    return (true, converted, null);
                }
                catch
                {
                    return (false, null, $"RewriteEventParamsEffect: failed to convert value to '{targetType.Name}'.");
                }
            };
        }

        private async Task<PausableTransactionResult> CommitTransactionAsync(GameContext gameContext, IEnumerable<IGameEvent> initialEvents)
        {
            _logger.LogInformation("--- Phase 3: Committing Transaction ---");
            _effectQueue.Clear();
            var eventStagingArea = new Queue<IGameEvent>(initialEvents);
            var delayedEventsByParent = new Dictionary<Guid, Queue<IGameEvent>>();
            var queueDrainedDelayedEvents = new Queue<IGameEvent>();
            var readyQueueDrainedEvents = new HashSet<Guid>();
            var processedEvents = new List<IGameEvent>();
            var eventSignatureCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var bundleId = Guid.NewGuid();

            while (eventStagingArea.Count > 0 || _effectQueue.Count > 0 || queueDrainedDelayedEvents.Count > 0)
            {
                if (eventStagingArea.Count == 0 && _effectQueue.Count == 0)
                {
                    ReleaseAllDelayedEventsWhenIdle(eventStagingArea, delayedEventsByParent);
                    ReleaseQueueDrainedEventIfReady(queueDrainedDelayedEvents, readyQueueDrainedEvents, eventStagingArea, delayedEventsByParent);
                }

                if (eventStagingArea.Count > 0)
                {
                    var currentEvent = eventStagingArea.Dequeue();
                    var result = await ProcessEventRecursivelyAsync(
                        gameContext,
                        currentEvent,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,
                        recursionDepth: 0);
                    if (!result.IsSuccess) return result;
                }
                else
                {
                    var currentEffect = _effectQueue.Dequeue();
                    var result = await ProcessEffectAsync(
                        currentEffect,
                        gameContext,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,
                        recursionDepth: 0);
                    if (!result.IsSuccess) return result;
                }
            }

            var updatedCards = CollectUpdatedCardInstances(gameContext, processedEvents);
            if (updatedCards.Count > 0)
            {
                processedEvents.Add(new CardInstanceUpdatedEvent(updatedCards));
            }

            var broadcastPlan = new List<(IGameEvent Event, string? Announcement, IReadOnlyList<GameEventDto> Dtos)>(processedEvents.Count);
            var recordedEvents = new List<CommittedEventWriteDto>(processedEvents.Count);

            foreach (var gameEvent in processedEvents)
            {
                var announcement = (gameEvent as ILogAnnounceableEvent)?.GetAnnouncement(gameContext.CurrentGameState);
                if (!string.IsNullOrWhiteSpace(announcement))
                {
                    announcement = AnnouncementTokens.WithTurn(gameContext.CurrentGameState, announcement);
                }
                var processorResult = await _eventProcessor.ProcessEventAsync(gameEvent, gameContext);
                var dtos = processorResult.ResultingDtos.ToList();

                if (dtos.Count == 0 && !string.IsNullOrWhiteSpace(announcement))
                {
                    var sourcePlayerId = gameEvent.Source?.Player?.PlayerId ?? Guid.Empty;
                    dtos.Add(new LogEventDto(gameEvent.EventId, gameEvent.EventType.ToString(), announcement, sourcePlayerId));
                }

                var envelopes = _recordedEventMapper.MapClientDtos(dtos);
                string? rawEventJson = null;
                try
                {
                    rawEventJson = _rawEventMapper.MapToOutputRawEventJson(gameContext.CurrentGameState, gameEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to map {EventType} (ID: {EventId}) to RawEventJson.", gameEvent.EventType, gameEvent.EventId);
                }

                var targets = gameEvent.Target?.Select(t => t.InstanceId).ToArray() ?? Array.Empty<Guid>();
                recordedEvents.Add(new CommittedEventWriteDto(
                    gameEvent.EventId,
                    gameEvent.EventType.ToString(),
                    gameEvent.Timestamp,
                    announcement,
                    gameEvent.Source?.InstanceId,
                    targets,
                    envelopes,
                    rawEventJson));

                broadcastPlan.Add((gameEvent, announcement, dtos));
            }

            // Persist the committed event bundle for replay/debug export (best-effort; do not break the match).
            if (ShouldPersistReplay(gameContext.CurrentGameState))
            {
                try
                {
                    var appendResult = await _eventJournal.AppendBundleAsync(
                        gameContext.CurrentGameState.GameId,
                        bundleId,
                        recordedEvents,
                        gameContext.CancellationToken);

                    if (_replayOptions.EnableSnapshots && appendResult.EndSeq > 0)
                    {
                        var shouldSnapshot =
                            (_replayOptions.SnapshotOnGameStart && processedEvents.Any(e => e.EventType == EventType.GameStart)) ||
                            (_replayOptions.SnapshotOnTurnStart && processedEvents.Any(e => e.EventType == EventType.TurnStart)) ||
                            (_replayOptions.SnapshotOnGameEnd && processedEvents.Any(e => e.EventType == EventType.GameEnd)) ||
                            (_replayOptions.SnapshotEveryBundles > 0 && appendResult.BundleSeq % _replayOptions.SnapshotEveryBundles == 0);

                        if (shouldSnapshot)
                        {
                            var snapshot = gameContext.Mapper.Map<GameStateDto>(gameContext.CurrentGameState);
                            await _snapshotStore.SaveSnapshotAsync(
                                gameContext.CurrentGameState.GameId,
                                appendResult.EndSeq,
                                snapshot,
                                gameContext.CancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist replay bundle {BundleId} for GameId={GameId}.", bundleId, gameContext.CurrentGameState.GameId);
                }
            }
            else
            {
                _logger.LogDebug("Skipping replay persistence for single-player game {GameId}.", gameContext.CurrentGameState.GameId);
            }

            // --- Broadcast all processed events AFTER the entire transaction is complete ---
            // Apply meta-progression updates (economy/profile/quests) after state commit, before broadcasting.
            gameContext.BuffService.RefreshDerivedStateCache(gameContext, "transaction_end");
            await _metaProgressionService.ApplyPostCommitAsync(gameContext, processedEvents, gameContext.CancellationToken);

            _logger.LogInformation("Broadcasting {Count} processed events at the end of the transaction.", processedEvents.Count);
            string groupName = $"Game_{gameContext.CurrentGameState.GameId}";
            foreach (var item in broadcastPlan)
            {
                var announcement = item.Announcement;
                var hasDto = item.Dtos.Count > 0;
                foreach (var dto in item.Dtos)
                {
                    if (!string.IsNullOrWhiteSpace(announcement) && dto is GameEventDto gameEventDto)
                    {
                        gameEventDto.Announcement ??= announcement;
                    }

                    await _clientMethodMapper.BroadcastEventToGroupAllAsync(groupName, dto);
                }

                // If this event produces no DTOs, still broadcast a lightweight log entry.
                if (!hasDto && !string.IsNullOrWhiteSpace(announcement))
                {
                    var sourcePlayerId = item.Event.Source?.Player?.PlayerId ?? Guid.Empty;
                    var logDto = new LogEventDto(item.Event.EventId, item.Event.EventType.ToString(), announcement, sourcePlayerId);
                    await _clientMethodMapper.BroadcastEventToGroupAllAsync(groupName, logDto);
                }
            }

            _logger.LogInformation("--- Transaction End: All events and effects processed successfully. ---");
            return PausableTransactionResult.Success(processedEvents);
        }

        private static List<CardInstance> CollectUpdatedCardInstances(GameContext gameContext, IReadOnlyList<IGameEvent> processedEvents)
        {
            var updatedIds = new HashSet<Guid>();

            foreach (var gameEvent in processedEvents)
            {
                var sourceCard = gameEvent.Source?.Card;
                if (sourceCard != null && sourceCard.InstanceId != Guid.Empty)
                {
                    updatedIds.Add(sourceCard.InstanceId);
                }

                if (gameEvent.Target != null)
                {
                    foreach (var target in gameEvent.Target)
                    {
                        if (target.Card != null && target.Card.InstanceId != Guid.Empty)
                        {
                            updatedIds.Add(target.Card.InstanceId);
                        }
                    }
                }

                if (gameEvent is BuffAppliedEvent buffApplied)
                {
                    var targetId = buffApplied.AppliedBuff.TargetCardInstanceId;
                    if (targetId != Guid.Empty)
                    {
                        updatedIds.Add(targetId);
                    }
                }
                else if (gameEvent is BuffRemovedEvent buffRemoved)
                {
                    var targetId = buffRemoved.RemovedBuff.TargetCardInstanceId;
                    if (targetId != Guid.Empty)
                    {
                        updatedIds.Add(targetId);
                    }
                }
            }

            var updatedCards = new List<CardInstance>();
            foreach (var cardId in updatedIds)
            {
                var card = gameContext.CurrentGameState.FindCardInstance(cardId);
                if (card != null)
                {
                    updatedCards.Add(card);
                }
            }

            return updatedCards;
        }

        private static bool ShouldPersistReplay(GameState gameState)
        {
            return !gameState.Player1.IsAI && !gameState.Player2.IsAI;
        }

        private async Task<PausableTransactionResult> ProcessEventRecursivelyAsync(
            GameContext gameContext,
            IGameEvent gameEvent,
            List<IGameEvent> processedEvents,
            Dictionary<string, int> eventSignatureCounts,
            Queue<IGameEvent> eventStagingArea,
            Dictionary<Guid, Queue<IGameEvent>> delayedEventsByParent,
            Queue<IGameEvent> queueDrainedDelayedEvents,
            HashSet<Guid> readyQueueDrainedEvents,
            int recursionDepth)
        {
            if (!gameContext.IsAuraRefresh)
            {
                Guid sourceAbilityId = Guid.Empty;
                if (gameEvent is BuffAppliedEvent appliedEvent)
                {
                    sourceAbilityId = appliedEvent.AppliedBuff.SourceAbilityId;
                }
                else if (gameEvent is BuffRemovedEvent removedEvent)
                {
                    sourceAbilityId = removedEvent.RemovedBuff.SourceAbilityId;
                }

                if (sourceAbilityId != Guid.Empty)
                {
                    var sourceAbility = gameContext.AbilityManager.GetAbilityByRuntimeId(sourceAbilityId);
                    if (sourceAbility is Ability ability && ability.Config.Auras.Any())
                    {
                        gameContext = gameContext with { IsAuraRefresh = true };
                    }
                }
            }

            if (recursionDepth > GameConstants.AbilityTrigger.MaxEventRecursionDepth)
            {
                var message = $"Event recursion depth exceeded ({GameConstants.AbilityTrigger.MaxEventRecursionDepth}) at {gameEvent.EventType}.";
                _logger.LogError(message);
                return PausableTransactionResult.Failure(message);
            }

            var signature = BuildEventSignature(gameEvent);
            if (!string.IsNullOrWhiteSpace(signature))
            {
                if (!eventSignatureCounts.TryGetValue(signature, out var count))
                {
                    count = 0;
                }

                count++;
                eventSignatureCounts[signature] = count;

                if (count > GameConstants.AbilityTrigger.MaxEventSignatureRepeats)
                {
                    var message = $"Event recursion guard triggered for signature '{signature}'.";
                    _logger.LogError(message);
                    return PausableTransactionResult.Failure(message);
                }
            }

            _logger.LogDebug("Processing event: {EventType} ({EventId})", gameEvent.GetType().Name, gameEvent.EventId);

            if (gameEvent is IDelayedEvent delayedEvent)
            {
                delayedEvent.LockDelayConfig();
                var delayConfig = delayedEvent.DelayConfig.Normalize();

                if (delayConfig.IsActive)
                {
                    if (delayConfig.Mode == DelayMode.QueueDrained)
                    {
                        if (!readyQueueDrainedEvents.Remove(gameEvent.EventId))
                        {
                            queueDrainedDelayedEvents.Enqueue(gameEvent);
                            return PausableTransactionResult.Success(new List<IGameEvent>());
                        }
                    }
                    else if (delayConfig.ParentEventId != Guid.Empty)
                    {
                        if (_effectQueue.Any(effect => effect.TriggeredEventId == delayConfig.ParentEventId))
                        {
                            EnqueueDelayedEvent(delayedEventsByParent, delayConfig.ParentEventId, gameEvent);
                            return PausableTransactionResult.Success(new List<IGameEvent>());
                        }
                    }
                }
            }

            // NOTE: Broadcasting is now deferred to the end of the CommitTransactionAsync method.
            // This method now only focuses on processing the domain logic.
            if (gameEvent is TurnStartEvent turnStartEvent && turnStartEvent.StartingPlayer != null)
            {
                var expiryEvents = gameContext.BuffService.CollectStartOfTurnExpiryEvents(gameContext, turnStartEvent.StartingPlayer);
                foreach (var expiryEvent in expiryEvents)
                {
                    var expiryResult = await ProcessEventRecursivelyAsync(
                        gameContext,
                        expiryEvent,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,
                        recursionDepth + 1);
                    if (!expiryResult.IsSuccess) return expiryResult;
                }

                gameContext.BuffService.RefreshDerivedStateCache(gameContext, "turn_start");
            }

            // 0. Before intercepts: allow rewrites/replacements before committing the event.
            var (interceptResult, interceptedEvent) = await ApplyBeforeInterceptsAsync(gameContext, gameEvent, processedEvents);
            if (!interceptResult.IsSuccess) return interceptResult;
            gameEvent = interceptedEvent;

            var decisionResolveResult = await ResolveDecisionEventIfNeededAsync(gameContext, gameEvent, DecisionResolutionPhase.Commit);
            if (!decisionResolveResult.IsSuccess) return decisionResolveResult;

            processedEvents.Add(gameEvent);

            // 1. 'Before' phase: Effects are executed immediately and their events are processed recursively.
            var auraBeforeResult = await RefreshAurasIfNeededAsync(
                gameContext,
                gameEvent,
                TriggerTiming.Before,
                processedEvents,
                eventSignatureCounts,
                eventStagingArea,
                delayedEventsByParent,
                queueDrainedDelayedEvents,
                readyQueueDrainedEvents,
                recursionDepth + 1);
            if (!auraBeforeResult.IsSuccess) return auraBeforeResult;

            var (beforeResult, beforeEffects, beforeEvents) = await GetTriggerAbilityEffectsAsync(gameContext, gameEvent, TriggerTiming.Before);
            if (!beforeResult.IsSuccess) return beforeResult;
            foreach (var abilityEvent in beforeEvents)
            {
                var eventResult = await ProcessEventRecursivelyAsync(
                    gameContext,
                    abilityEvent,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,
                    recursionDepth + 1);
                if (!eventResult.IsSuccess) return eventResult;
            }
            foreach (var effect in beforeEffects.Where(effect => !IsInterceptEffect(effect)))
            {
                var result = await ProcessEffectAsync(
                    effect,
                    gameContext,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,
                    recursionDepth + 1);
                if (!result.IsSuccess) return result;
            }

            // 2. 'On' phase: Effects are enqueued for later processing.
            var auraOnResult = await RefreshAurasIfNeededAsync(
                gameContext,
                gameEvent,
                TriggerTiming.On,
                processedEvents,
                eventSignatureCounts,
                eventStagingArea,
                delayedEventsByParent,
                queueDrainedDelayedEvents,
                readyQueueDrainedEvents,
                recursionDepth + 1);
            if (!auraOnResult.IsSuccess) return auraOnResult;

            var (onResult, onEffects, onEvents) = await GetTriggerAbilityEffectsAsync(gameContext, gameEvent, TriggerTiming.On);
            if (!onResult.IsSuccess) return onResult;
            foreach (var abilityEvent in onEvents)
            {
                var eventResult = await ProcessEventRecursivelyAsync(
                    gameContext,
                    abilityEvent,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,
                    recursionDepth + 1);
                if (!eventResult.IsSuccess) return eventResult;
            }
            foreach (var effect in onEffects)
            {
                _effectQueue.Enqueue(effect);
            }

            // 3. 'Commit' phase: The event's state change is applied, and resulting events are processed recursively.
            if (gameEvent is ICommittableEvent committableEvent)
            {
                _logger.LogInformation("Committing state changes for {EventType}.", gameEvent.GetType().Name);
                var commitResult = committableEvent.Commit(gameContext);
                if (!commitResult.Success)
                {
                    return PausableTransactionResult.Failure(commitResult.ErrorMessage ?? $"Commit failed for {gameEvent.GetType().Name}.");
                }
                gameContext.CurrentGameState.IncrementStateRevision();
                foreach (var subEvent in commitResult.GeneratedEvents)
                {
                    var recursiveResult = await ProcessEventRecursivelyAsync(
                        gameContext,
                        subEvent,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,
                        recursionDepth + 1);
                    if (!recursiveResult.IsSuccess) return recursiveResult;
                }

                if (gameEvent is TurnEndEvent)
                {
                    var expiryEvents = gameContext.BuffService.CollectEndOfTurnExpiryEvents(gameContext);
                    foreach (var expiryEvent in expiryEvents)
                    {
                        var expiryResult = await ProcessEventRecursivelyAsync(
                            gameContext,
                            expiryEvent,
                            processedEvents,
                            eventSignatureCounts,
                            eventStagingArea,
                            delayedEventsByParent,
                            queueDrainedDelayedEvents,
                            readyQueueDrainedEvents,
                            recursionDepth + 1);
                        if (!expiryResult.IsSuccess) return expiryResult;
                    }

                    gameContext.BuffService.RefreshDerivedStateCache(gameContext, "turn_end");
                }
            }

            // 4. 'After' phase: Effects are enqueued for later processing.
            var auraAfterResult = await RefreshAurasIfNeededAsync(
                gameContext,
                gameEvent,
                TriggerTiming.After,
                processedEvents,
                eventSignatureCounts,
                eventStagingArea,
                delayedEventsByParent,
                queueDrainedDelayedEvents,
                readyQueueDrainedEvents,
                recursionDepth + 1);
            if (!auraAfterResult.IsSuccess) return auraAfterResult;

            var (afterResult, afterEffects, afterEvents) = await GetTriggerAbilityEffectsAsync(gameContext, gameEvent, TriggerTiming.After);
            if (!afterResult.IsSuccess) return afterResult;
            foreach (var abilityEvent in afterEvents)
            {
                var eventResult = await ProcessEventRecursivelyAsync(
                    gameContext,
                    abilityEvent,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,
                    recursionDepth + 1);
                if (!eventResult.IsSuccess) return eventResult;
            }
            foreach (var effect in afterEffects)
            {
                _effectQueue.Enqueue(effect);
            }

            ResetAbilityEventFlags(gameContext);
            ReleaseDelayedEventsIfReady(gameEvent.EventId, eventStagingArea, delayedEventsByParent);
            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private static void ResetAbilityEventFlags(GameContext gameContext)
        {
            if (gameContext.AbilityManager is AbilityManager abilityManager)
            {
                abilityManager.ResetEventFlagsForAllAbilities();
            }
        }

        private static string BuildEventSignature(IGameEvent gameEvent)
        {
            var sourceId = gameEvent.Source?.InstanceId ?? Guid.Empty;
            var targetIds = gameEvent.Target?
                .Select(target => target.InstanceId)
                .Where(id => id != Guid.Empty)
                .OrderBy(id => id)
                .Select(id => id.ToString("N"))
                .ToList()
                ?? new List<string>();

            var targets = targetIds.Count == 0 ? string.Empty : string.Join(",", targetIds);
            return $"{gameEvent.EventType}:{sourceId:N}:{targets}";
        }

        private async Task<PausableTransactionResult> RefreshAurasIfNeededAsync(
            GameContext gameContext,
            IGameEvent gameEvent,
            TriggerTiming timing,
            List<IGameEvent> processedEvents,
            Dictionary<string, int> eventSignatureCounts,
            Queue<IGameEvent> eventStagingArea,
            Dictionary<Guid, Queue<IGameEvent>> delayedEventsByParent,
            Queue<IGameEvent> queueDrainedDelayedEvents,
            HashSet<Guid> readyQueueDrainedEvents,
            int recursionDepth)
        {
            if (gameContext.IsAuraRefresh)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            if (timing != TriggerTiming.After)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            var triggeredBeforeAbilities = await gameContext.AbilityManager.GetTriggeredAbilitiesAsync(
                gameEvent,
                gameContext,
                TriggerTiming.Before);

            var triggeredOnAbilities = await gameContext.AbilityManager.GetTriggeredAbilitiesAsync(
                gameEvent,
                gameContext,
                TriggerTiming.On);

            var triggeredAfterAbilities = await gameContext.AbilityManager.GetTriggeredAbilitiesAsync(
                gameEvent,
                gameContext,
                TriggerTiming.After);

            var auraAbilities = triggeredBeforeAbilities
                .Concat(triggeredOnAbilities)
                .Concat(triggeredAfterAbilities)
                .OfType<Ability>()
                .Where(IsAuraOnlyAbility)
                .GroupBy(ability => ability.RuntimeId)
                .Select(group => group.First())
                .ToList();

            if (auraAbilities.Count == 0)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            foreach (var ability in auraAbilities)
            {
                var abilityContext = gameContext with { SourceCard = ability.SourceCard, TriggeringEvent = gameEvent };
                var abilityResult = await gameContext.AbilityManager.ProcessSingleAbilityAsync(ability, abilityContext, false, true);
                if (!abilityResult.IsSuccess)
                {
                    return PausableTransactionResult.Failure(abilityResult.ErrorMessage ?? "Aura refresh failed.");
                }

                foreach (var abilityEvent in abilityResult.GeneratedEvents)
                {
                    var eventResult = await ProcessEventRecursivelyAsync(
                        gameContext,
                        abilityEvent,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,
                        recursionDepth + 1);
                    if (!eventResult.IsSuccess) return eventResult;
                }
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private static bool IsAuraOnlyAbility(Ability ability)
        {
            if (ability == null || ability.Config == null)
            {
                return false;
            }

            if (ability.Config.Auras == null || ability.Config.Auras.Count == 0)
            {
                return false;
            }

            return ability.Type == AbilityType.Continuous;
        }

        private async Task<PausableTransactionResult> ProcessChainedAbilitiesAsync(
            Effect executedEffect,
            GameContext context,
            GameStateActionResult effectResult,
            List<IGameEvent> processedEvents,
            Dictionary<string, int> eventSignatureCounts,
            Queue<IGameEvent> eventStagingArea,
            Dictionary<Guid, Queue<IGameEvent>> delayedEventsByParent,
            Queue<IGameEvent> queueDrainedDelayedEvents,
            HashSet<Guid> readyQueueDrainedEvents,
            int recursionDepth)
        {
            if (executedEffect.ChainedAbilities.Any())
            {
                _logger.LogInformation("Effect {EffectType} has {Count} chained abilities. Processing them...", executedEffect.Type, executedEffect.ChainedAbilities.Count);

                // Find the EffectCompletedEvent which is the true trigger for these chained abilities.
                var effectCompletedEvent = effectResult.GeneratedEvents.OfType<EffectCompletedEvent>().FirstOrDefault();
                if (effectCompletedEvent == null)
                {
                    _logger.LogWarning("Could not find EffectCompletedEvent for chained ability processing. Context may be incomplete for ability on card {SourceCardId}.", executedEffect.SourceCardId);
                }

                foreach (var chainedAbility in executedEffect.ChainedAbilities)
                {
                    // Create the precise context for this chained ability.
                    var abilityContext = context with
                    {
                        SourceCard = chainedAbility.SourceCard,
                        TriggeringEvent = effectCompletedEvent, // Use the found event as the trigger
                        CurrentEffect = executedEffect,
                        ResolvedTargets = executedEffect.ResolvedTargets
                    };

                    var result = await context.AbilityManager.ProcessSingleAbilityAsync(chainedAbility, abilityContext, false, true);

                    if (!result.IsSuccess)
                    {
                        _logger.LogError("Chained ability {AbilityKeywords} failed: {Error}", chainedAbility.KeywordSummary, result.ErrorMessage);
                        continue;
                    }

                    // The IsPaused logic is now handled internally by TargetSelectionService.

                    foreach (var newEffect in result.Effects)
                    {
                        var subEffectResult = await ProcessEffectAsync(
                            newEffect,
                            abilityContext,
                            processedEvents,
                            eventSignatureCounts,
                            eventStagingArea,
                            delayedEventsByParent,
                            queueDrainedDelayedEvents,
                            readyQueueDrainedEvents,
                            recursionDepth + 1);
                        if (!subEffectResult.IsSuccess)
                        {
                            return subEffectResult;
                        }
                    }
                }
            }
            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private async Task<PausableTransactionResult> ExecuteDecisionAbilitiesAsync(
            Effect effect,
            GameContext context,
            List<IGameEvent> processedEvents,
            Dictionary<string, int> eventSignatureCounts,
            Queue<IGameEvent> eventStagingArea,
            Dictionary<Guid, Queue<IGameEvent>> delayedEventsByParent,
            Queue<IGameEvent> queueDrainedDelayedEvents,
            HashSet<Guid> readyQueueDrainedEvents,
            List<IGameEvent> decisionEvents,
            int recursionDepth)
        {
            if (!effect.DecisionAbilities.Any())
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            var capturedEventIds = new HashSet<Guid>();
            foreach (var existing in decisionEvents)
            {
                capturedEventIds.Add(existing.EventId);
            }

            foreach (var decisionAbility in effect.DecisionAbilities)
            {
                var abilityContext = context with
                {
                    SourceCard = decisionAbility.SourceCard,
                    TriggeringEvent = context.TriggeringEvent,
                    CurrentEffect = effect,
                    ResolvedTargets = effect.ResolvedTargets
                };

                var result = await context.AbilityManager.ProcessSingleAbilityAsync(decisionAbility, abilityContext, false, true);
                if (!result.IsSuccess)
                {
                    _logger.LogError("Decision ability {AbilityKeywords} failed: {Error}", decisionAbility.KeywordSummary, result.ErrorMessage);
                    continue;
                }

                foreach (var newEffect in result.Effects)
                {
                    var processedStart = processedEvents.Count;
                    var subEffectResult = await ProcessEffectAsync(
                        newEffect,
                        abilityContext,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,
                        recursionDepth + 1);
                    if (!subEffectResult.IsSuccess)
                    {
                        return subEffectResult;
                    }

                    for (var i = processedStart; i < processedEvents.Count; i++)
                    {
                        var newEvent = processedEvents[i];
                        if (capturedEventIds.Add(newEvent.EventId))
                        {
                            decisionEvents.Add(newEvent);
                        }
                    }
                }
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private Task<(PausableTransactionResult Result, Ability? DecisionAbility, IGameEvent? DecisionEvent)> PrepareDecisionEventAsync(
            Effect effect,
            GameContext effectContext,
            int executionIndex = 0)
        {
            if (effect.Type != EffectType.Choose && effect.Type != EffectType.Random)
            {
                return Task.FromResult((PausableTransactionResult.Success(new List<IGameEvent>()), (Ability?)null, (IGameEvent?)null));
            }

            var ability = effect.SourceAbility as Ability;
            if (ability == null && effect.sourceAbilityId.HasValue)
            {
                ability = effectContext.AbilityManager.GetAbilityByRuntimeId(effect.sourceAbilityId.Value) as Ability;
            }

            if (ability == null)
            {
                return Task.FromResult((PausableTransactionResult.Failure("Decision effect requires a source ability."), (Ability?)null, (IGameEvent?)null));
            }

            Ability.DecisionInput input;
            try
            {
                input = ability.PrepareDecisionInput(
                    effectContext,
                    effect.TriggeredEventId,
                    effect.EffectIndex,
                    effect.EffectConfig,
                    isRandom: effect.Type == EffectType.Random,
                    executionIndex: executionIndex);
            }
            catch (GameLogicException ex)
            {
                return Task.FromResult((PausableTransactionResult.Failure(ex.Message), (Ability?)null, (IGameEvent?)null));
            }

            IGameEvent decisionEvent = effect.Type == EffectType.Choose
                ? new ChooseEvent(
                    input.OptionIds,
                    input.SelectionCount,
                    ability.SourceCard)
                : new RandomEvent(
                    input.OptionIds,
                    input.SelectionCount,
                    input.AllowRepeat,
                    input.Weights.ToList(),
                    ability.SourceCard);

            return Task.FromResult((PausableTransactionResult.Success(new List<IGameEvent>()), (Ability?)ability, (IGameEvent?)decisionEvent));
        }

        private PausableTransactionResult ResolveDecisionAbilitiesFromEvent(
            Effect effect,
            GameContext effectContext,
            Ability ability,
            IGameEvent decisionEvent,
            out List<IAbility> decisionAbilities)
        {
            decisionAbilities = new List<IAbility>();

            IReadOnlyList<Guid> optionIds = decisionEvent switch
            {
                ChooseEvent chooseEvent => chooseEvent.OptionIds,
                RandomEvent randomEvent => randomEvent.OptionIds,
                _ => Array.Empty<Guid>()
            };

            IReadOnlyList<int> selectedIndexes = decisionEvent switch
            {
                ChooseEvent chooseEvent => chooseEvent.SelectedOptionIds,
                RandomEvent randomEvent => randomEvent.SelectedOptionIds,
                _ => Array.Empty<int>()
            };

            if (selectedIndexes.Count == 0)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            if (ability.SourceCard == null)
            {
                return PausableTransactionResult.Failure("Decision effect requires a source card to execute branch abilities.");
            }

            var isRandom = decisionEvent is RandomEvent;
            IReadOnlyList<Ability.DecisionOptionSnapshot> options;
            try
            {
                options = ability.GetDecisionOptions(effect.TriggeredEventId, effect.EffectIndex, isRandom, effect.ExecutionIndex);
            }
            catch (GameLogicException ex)
            {
                return PausableTransactionResult.Failure(ex.Message);
            }

            if (optionIds.Count == 0)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            var optionLookup = options.ToDictionary(option => option.Id, option => option.Config);
            var selectedConfigs = new List<AbilityConfig>(selectedIndexes.Count);
            foreach (var index in selectedIndexes)
            {
                if (index < 0 || index >= optionIds.Count)
                {
                    return PausableTransactionResult.Failure("Invalid decision selection result.");
                }

                var optionId = optionIds[index];
                if (!optionLookup.TryGetValue(optionId, out var config))
                {
                    return PausableTransactionResult.Failure("Invalid decision selection result.");
                }
                selectedConfigs.Add(config);
            }

            var abilityFactory = effectContext.ServiceProvider.GetRequiredService<IAbilityFactory>();
            decisionAbilities = abilityFactory
                .CreateAbilities(ability.SourceCard, selectedConfigs, ability.SourceBuffId)
                .Cast<IAbility>()
                .ToList();

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private async Task<PausableTransactionResult> ResolveDeferredTargetsIfNeededAsync(Effect effect, GameContext effectContext)
        {
            if (!effect.ResolveTargetsOnExecution)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            if (!effect.sourceAbilityId.HasValue && effect.SourceAbility == null)
            {
                _logger.LogWarning("Effect {EffectType} ({EffectId}) marked for deferred target resolution but has no source ability.", effect.Type, effect.Id);
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            var ability = effect.SourceAbility;
            if (ability == null && effect.sourceAbilityId.HasValue)
            {
                ability = effectContext.AbilityManager.GetAbilityByRuntimeId(effect.sourceAbilityId.Value);
            }
            if (ability == null)
            {
                return PausableTransactionResult.Failure($"Deferred target resolution failed: ability {effect.sourceAbilityId} not found.");
            }

            if (ability.Targets.Any(t => t.Config.PlayerChoiceOptions?.IsRequired == true))
            {
                return PausableTransactionResult.Failure($"Deferred target resolution cannot handle player choice for ability '{ability.KeywordSummary}'.");
            }

            var targetResult = await ability.ResolveTargetsAsync(effectContext, false);
            if (targetResult.Status == ResolutionStatus.Failed)
            {
                return PausableTransactionResult.Failure(targetResult.ErrorMessage ?? "Deferred target resolution failed.");
            }

            effect.SelectedTargets = ResolveEffectTargets(effect.EffectConfig, targetResult.ResolvedTargets);
            effect.ResolvedTargets = targetResult.ResolvedTargets;
            effect.ResolveTargetsOnExecution = false;

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private static List<TargetInfo> ResolveEffectTargets(EffectConfig effectConfig, IReadOnlyDictionary<string, List<TargetInfo>> resolvedTargets)
        {
            if (effectConfig.TargetSelector == null || effectConfig.TargetSelector.Count == 0)
            {
                return resolvedTargets.Values.SelectMany(list => list).ToList();
            }

            if (EffectTargetSelector.TrySelectTargets(effectConfig.TargetSelector, resolvedTargets, out var effectTargets))
            {
                return effectTargets;
            }

            return resolvedTargets.Values.SelectMany(list => list).ToList();
        }

        private async Task<PausableTransactionResult> ProcessEffectAsync(
            Effect effect,
            GameContext gameContext,
            List<IGameEvent> processedEvents,
            Dictionary<string, int> eventSignatureCounts,
            Queue<IGameEvent> eventStagingArea,
            Dictionary<Guid, Queue<IGameEvent>> delayedEventsByParent,
            Queue<IGameEvent> queueDrainedDelayedEvents,
            HashSet<Guid> readyQueueDrainedEvents,
            int recursionDepth)
        {
            _logger.LogDebug("Processing effect: {EffectType}", effect.Type);

            if (IsInterceptEffectType(effect.Type))
            {
                _logger.LogDebug("Skipping intercept effect {EffectType} outside of the Before intercept pipeline.", effect.Type);
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            // --- BUG-025 FIX START ---
            // Create a new, precise context for this specific effect execution.
            // This ensures that DSLs and handlers use the context in which the effect was generated,
            // not the broader context of the event that might have triggered it.
            var sourceCard = effect.SourceCardId.HasValue
                ? gameContext.CurrentGameState.FindCardInstance(effect.SourceCardId.Value)
                : null;

            if (!effect.ForceExecution && effect.sourceAbilityId.HasValue)
            {
                var ability = effect.SourceAbility ?? gameContext.AbilityManager.GetAbilityByRuntimeId(effect.sourceAbilityId.Value);
                if (ability?.TriggerConfig?.RevalidateZoneBeforeExecution == true)
                {
                    var cardToCheck = sourceCard ?? ability.SourceCard;
                    if (cardToCheck != null && !ability.TriggerConfig.IsValidZone(cardToCheck.CurrentZone))
                    {
                        _logger.LogDebug(
                            "Skipping effect {EffectType} ({EffectId}) because source card {CardId} is now in zone {Zone}, which is outside ValidZones.",
                            effect.Type,
                            effect.Id,
                            cardToCheck.InstanceId,
                            cardToCheck.CurrentZone);
                        return PausableTransactionResult.Success(new List<IGameEvent>());
                    }
                }
            }

            IGameEvent? triggeringEvent = null;
            if (effect.TriggeredEventId != Guid.Empty)
            {
                triggeringEvent = processedEvents.FirstOrDefault(e => e.EventId == effect.TriggeredEventId);
            }

            // Chained abilities can reference an EffectCompletedEvent that hasn't been processed yet.
            // Fall back to the current GameContext.TriggeringEvent when it matches the requested id.
            if (triggeringEvent == null && gameContext.TriggeringEvent != null)
            {
                if (effect.TriggeredEventId == Guid.Empty || gameContext.TriggeringEvent.EventId == effect.TriggeredEventId)
                {
                    triggeringEvent = gameContext.TriggeringEvent;
                }
            }

            var effectContext = gameContext with
            {
                SourceCard = sourceCard,
                TriggeringEvent = triggeringEvent,
                CurrentEffect = effect,
                ResolvedTargets = effect.ResolvedTargets
            };
            // --- BUG-025 FIX END ---

            // --- STATE_POLLUTION_TRACE ---
            _logger.LogWarning("[STATE_POLLUTION_TRACE] Processing Effect in GameId: {CurrentGameId}. Effect was generated with SourceCardId: {EffectSourceCardId}. Found Card in current state: {FoundCardId}",
                gameContext.CurrentGameState.GameId,
                effect.SourceCardId,
                sourceCard?.InstanceId);
            // --- END TRACE ---

            _logger.LogDebug("[CONTEXT_TRACKING] In ProcessEffectAsync for Effect {EffectType} ({EffectId}). SourceCardId from Effect: {SourceCardId}. Found SourceCard in Context: {SourceCardInstanceId}",
                effect.Type,
                effect.Id,
                effect.SourceCardId,
                effectContext.SourceCard?.InstanceId);

            var deferredResolutionResult = await ResolveDeferredTargetsIfNeededAsync(effect, effectContext);
            if (!deferredResolutionResult.IsSuccess)
            {
                return deferredResolutionResult;
            }

            var repeatCount = Math.Max(0, effect.EffectConfig.RepeatCount);
            var totalExecutions = repeatCount + 1;
            var repeatCondition = effect.EffectConfig.RepeatCondition;
            IConditionEvaluator? repeatConditionEvaluator = null;

            for (var executionIndex = 0; executionIndex < totalExecutions; executionIndex++)
            {
                effect.ExecutionIndex = executionIndex;

                if (executionIndex > 0 && repeatCondition != null)
                {
                    bool shouldRepeat;
                    if (!repeatCondition.IsEnabled)
                    {
                        shouldRepeat = true;
                    }
                    else
                    {
                        repeatConditionEvaluator ??= gameContext.ServiceProvider.GetRequiredService<IConditionEvaluator>();
                        shouldRepeat = repeatConditionEvaluator.Evaluate(repeatCondition, effectContext);
                    }
                    if (!shouldRepeat)
                    {
                        _logger.LogDebug(
                            "Repeat condition not met for effect {EffectType} ({EffectId}); stopping repeats at {ExecutionIndex}/{TotalExecutions}.",
                            effect.Type,
                            effect.Id,
                            executionIndex,
                            totalExecutions);
                        break;
                    }
                }

                if (repeatCount > 0)
                {
                    _logger.LogDebug("Repeating effect {EffectType} ({EffectId}) {ExecutionIndex}/{TotalExecutions} due to RepeatCount={RepeatCount}.",
                        effect.Type,
                        effect.Id,
                        executionIndex + 1,
                        totalExecutions,
                        repeatCount);
                }

                var decisionEvents = new List<IGameEvent>();
                effect.DecisionAbilities.Clear();

                if ((effect.Type == EffectType.Choose || effect.Type == EffectType.Random) && executionIndex > 0)
                {
                    var decisionAbility = effect.SourceAbility as Ability;
                    if (decisionAbility == null && effect.sourceAbilityId.HasValue)
                    {
                        decisionAbility = effectContext.AbilityManager.GetAbilityByRuntimeId(effect.sourceAbilityId.Value) as Ability;
                    }

                    var shouldClearDecisionCache = true;
                    if (decisionAbility != null
                        && TryCreateDecisionCacheKey(effect, decisionAbility, effect.Type == EffectType.Random, out var cacheKey))
                    {
                        shouldClearDecisionCache = !_preflightDecisionCache.ContainsKey(cacheKey);
                    }

                    if (shouldClearDecisionCache)
                    {
                        decisionAbility?.ClearDecisionCache(effect.TriggeredEventId, effect.EffectIndex, executionIndex);
                    }
                }

                var decisionPreparation = await PrepareDecisionEventAsync(effect, effectContext, executionIndex);
                if (!decisionPreparation.Result.IsSuccess) return decisionPreparation.Result;

                if (decisionPreparation.DecisionEvent != null)
                {
                    var decisionEventResult = await ProcessEventRecursivelyAsync(
                        effectContext,
                        decisionPreparation.DecisionEvent,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,
                        recursionDepth + 1);
                    if (!decisionEventResult.IsSuccess) return decisionEventResult;
                }

                var decisionAbilities = new List<IAbility>();
                if (decisionPreparation.DecisionEvent != null && decisionPreparation.DecisionAbility != null)
                {
                    var abilityResult = ResolveDecisionAbilitiesFromEvent(
                        effect,
                        effectContext,
                        decisionPreparation.DecisionAbility,
                        decisionPreparation.DecisionEvent,
                        out decisionAbilities);
                    if (!abilityResult.IsSuccess) return abilityResult;
                }

                if (decisionAbilities.Count > 0)
                {
                    effect.DecisionAbilities.AddRange(decisionAbilities);
                    var decisionResult = await ExecuteDecisionAbilitiesAsync(
                        effect,
                        effectContext,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,
                        decisionEvents,
                        recursionDepth + 1);
                    if (!decisionResult.IsSuccess) return decisionResult;
                }

                effect.GeneratedEvents = decisionEvents;

                var effectResult = _effectExecutor.ExecuteEffect(effect, effectContext); // Use the new context

                var generatedEvents = effectResult.GeneratedEvents ?? new List<IGameEvent>();
                var eventProcessingSucceeded = true;
                string? eventProcessingError = null;
                EffectCompletedEvent? effectCompletedEvent = null;
                var effectCompletedIndex = -1;

                foreach (var newEvent in generatedEvents)
                {
                    if (newEvent is EffectCompletedEvent completedEvent)
                    {
                        effectCompletedEvent = completedEvent;
                        effectCompletedIndex = generatedEvents.IndexOf(newEvent);
                        continue;
                    }

                    if (processedEvents.Any(e => e.EventId == newEvent.EventId))
                    {
                        continue;
                    }

                    // Events generated by an effect should be processed within the same specific context.
                    var result = await ProcessEventRecursivelyAsync(
                        effectContext,
                        newEvent,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,
                        recursionDepth + 1);
                    if (!result.IsSuccess)
                    {
                        eventProcessingSucceeded = false;
                        eventProcessingError = result.ErrorMessage ?? "Generated event processing failed.";
                        break;
                    }
                }

                if (effectCompletedEvent != null)
                {
                    var refreshedGeneratedCards = CollectGeneratedCards(effectCompletedEvent.GeneratedEvents);
                    var refreshedSnapshots = refreshedGeneratedCards.Select(CardInstanceSnapshot.From).ToList();

                    var refreshedEffectCompletedEvent = new EffectCompletedEvent(
                        effectCompletedEvent.OriginalEffectType,
                        effectCompletedEvent.OriginalEffectInputParams,
                        effectCompletedEvent.ResolvedTargets,
                        effectCompletedEvent.GeneratedEvents,
                        refreshedGeneratedCards,
                        refreshedSnapshots,
                        effectCompletedEvent.Outputs,
                        effectCompletedEvent.ParentEffectSucceeded,
                        effectCompletedEvent.Source?.Card,
                        effectCompletedEvent.EventId,
                        effectCompletedEvent.Timestamp);

                    if (effectCompletedIndex >= 0)
                    {
                        generatedEvents[effectCompletedIndex] = refreshedEffectCompletedEvent;
                    }

                    effectCompletedEvent = refreshedEffectCompletedEvent;
                }

                if (eventProcessingSucceeded && effectCompletedEvent != null)
                {
                    if (!processedEvents.Any(e => e.EventId == effectCompletedEvent.EventId))
                    {
                        var result = await ProcessEventRecursivelyAsync(
                            effectContext,
                            effectCompletedEvent,
                            processedEvents,
                            eventSignatureCounts,
                            eventStagingArea,
                            delayedEventsByParent,
                            queueDrainedDelayedEvents,
                            readyQueueDrainedEvents,
                            recursionDepth + 1);
                        if (!result.IsSuccess)
                        {
                            eventProcessingSucceeded = false;
                            eventProcessingError = result.ErrorMessage ?? "Generated event processing failed.";
                        }
                    }
                }

                var finalSuccess = effectResult.Success && eventProcessingSucceeded;
                if (!finalSuccess)
                {
                    switch (effect.EffectConfig.OnFailure)
                    {
                        case FailureHandling.CompleteWithFailure:
                            _logger.LogWarning(
                                "Effect {EffectType} failed but will continue due to OnFailure=CompleteWithFailure. Error: {Error}",
                                effect.Type,
                                eventProcessingError ?? effectResult.ErrorMessage ?? "Unknown");
                            break;
                        case FailureHandling.SkipEffect:
                            _logger.LogWarning(
                                "Effect {EffectType} failed and was skipped due to OnFailure=SkipEffect. Error: {Error}",
                                effect.Type,
                                eventProcessingError ?? effectResult.ErrorMessage ?? "Unknown");
                            continue;
                        case FailureHandling.AbortAbility:
                        default:
                            var errorMessage = eventProcessingError
                                ?? effectResult.ErrorMessage
                                ?? $"Effect execution failed for {effect.Type}.";
                            _logger.LogError(errorMessage);
                            return PausableTransactionResult.Failure(errorMessage);
                    }
                }

                // Pass the new context to chained abilities as well
                var chainedResult = await ProcessChainedAbilitiesAsync(
                    effect,
                    effectContext,
                    effectResult,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,
                    recursionDepth + 1);
                if (!chainedResult.IsSuccess) return chainedResult;
            }

            if (effect.Type == EffectType.Choose || effect.Type == EffectType.Random)
            {
                if (effect.SourceAbility is Ability ability)
                {
                    for (var executionIndex = 0; executionIndex < totalExecutions; executionIndex++)
                    {
                        ability.ClearDecisionCache(effect.TriggeredEventId, effect.EffectIndex, executionIndex);
                    }
                }
            }

            ReleaseDelayedEventsIfReady(effect.TriggeredEventId, eventStagingArea, delayedEventsByParent);
            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        /// <inheritdoc />
        public Task<PausableTransactionResult> CancelPendingActionAsync(GameContext gameContext)
        {
            _logger.LogInformation("Attempting to cancel pending action for player: {PlayerId}", gameContext.RequestingPlayerId);

            var pendingChoiceContext = _pendingActionService.GetPendingChoiceForPlayer(gameContext.CurrentGameState.GameId, gameContext.RequestingPlayerId);

            if (pendingChoiceContext == null)
            {
                _logger.LogWarning("Player {PlayerId} tried to cancel but has no pending action.", gameContext.RequestingPlayerId);
                return Task.FromResult(PausableTransactionResult.Success(new List<IGameEvent>()));
            }

            // Use the correct method to safely cancel the pending choice.
            _pendingActionService.ForceResolveChoice(pendingChoiceContext.Request.PendingChoiceId, PlayerChoiceResult.Cancelled);

            _logger.LogInformation("Successfully requested cancellation for pending choice {ChoiceId} for player {PlayerId}.",
                pendingChoiceContext.Request.PendingChoiceId, gameContext.RequestingPlayerId);

            // Signal that the transaction was cancelled.
            return Task.FromResult(PausableTransactionResult.Cancelled());
        }

        private static IReadOnlyList<CardInstance> CollectGeneratedCards(IReadOnlyList<IGameEvent> generatedEvents)
        {
            if (generatedEvents == null || generatedEvents.Count == 0)
            {
                return new List<CardInstance>();
            }

            var results = new Dictionary<Guid, CardInstance>();
            foreach (var gameEvent in generatedEvents)
            {
                if (gameEvent == null)
                {
                    continue;
                }

                foreach (var targetInfo in gameEvent.Target)
                {
                    var card = targetInfo?.Card;
                    if (card == null)
                    {
                        continue;
                    }

                    if (!results.ContainsKey(card.InstanceId))
                    {
                        results[card.InstanceId] = card;
                    }
                }
            }

            return results.Values.ToList();
        }

    }
}
