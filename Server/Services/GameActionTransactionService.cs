using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // Add this for PlayerChoiceRequest
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Events.Services;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Dtos;

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
            _logger.LogWarning("[STATE_POLLUTION_TRACE] TransactionService: Received GameContext for GameId: {GameId} with GameState HashCode: {HashCode}", gameContext.CurrentGameState.GameId, gameContext.CurrentGameState.GetHashCode());
            _logger.LogInformation("--- Transaction Start: Processing {EventCount} initial events. ---", initialEvents.Count());

            // Phase 1: Analyze and resolve primary abilities (Cancellable Part)
            var analysisResult = await AnalyzePrimaryAbilitiesAsync(gameContext, initialEvents);
            if (!analysisResult.IsSuccess)
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
            var commitResult = await CommitTransactionAsync(gameContext, initialEvents);

            return commitResult;
        }

        private async Task<PausableTransactionResult> AnalyzePrimaryAbilitiesAsync(GameContext gameContext, IEnumerable<IGameEvent> allEvents)
        {
            _logger.LogInformation("--- Phase 1: Analyzing Primary Abilities ---");

            foreach (var gameEvent in allEvents)
            {
                // A primary ability is linked to a direct player action.
                if (gameEvent is IPlayerActionEvent playerEvent)
                {
                    // --- DIAGNOSTIC LOGGING START ---
                    _logger.LogWarning("[DIAGNOSTIC] Analyzing event: {EventType}. Context SourceCard: {CardName} ({CardId})",
                        gameEvent.GetType().Name,
                        gameContext.SourceCard?.Name ?? "NULL",
                        gameContext.SourceCard?.InstanceId ?? Guid.Empty);
                    // --- DIAGNOSTIC LOGGING END ---

                    var triggeredAbilities = await gameContext.AbilityManager.GetTriggeredAbilitiesAsync(gameEvent, gameContext, TriggerTiming.On);

                    // --- DIAGNOSTIC LOGGING START ---
                    var triggeredAbilitiesInfo = string.Join(", ", triggeredAbilities.Select(a => $"{a.AbilityTag} on {a.SourceCard?.Name ?? "System"}"));
                    _logger.LogWarning("[DIAGNOSTIC] Triggered abilities found: [{Abilities}]", triggeredAbilitiesInfo);
                    // --- DIAGNOSTIC LOGGING END ---

                    // According to the design doc, a "Primary Ability" is one where the ability's source
                    // is the same as the event's source. The user has clarified to use gameContext.SourceCard.
                    var primaryAbilities = triggeredAbilities.Where(ability =>
                        ability.SourceCard != null &&
                        gameContext.SourceCard != null &&
                        ability.SourceCard.InstanceId == gameContext.SourceCard.InstanceId
                    ).ToList();

                    // --- DIAGNOSTIC LOGGING START ---
                    var primaryAbilitiesInfo = string.Join(", ", primaryAbilities.Select(a => $"{a.AbilityTag} on {a.SourceCard?.Name ?? "System"}"));
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
                        var abilityContext = gameContext with { SourceCard = ability.SourceCard, TriggeringEvent = gameEvent };
                        var abilityResult = await gameContext.AbilityManager.ProcessSingleAbilityAsync(ability, abilityContext, true);

                        // The IsPaused logic is now handled internally by TargetSelectionService,
                        // so ProcessSingleAbilityAsync will await until the choice is made.
                        // We only need to handle the final success or failure.

                        if (!abilityResult.IsSuccess)
                        {
                            _logger.LogError("Primary ability '{AbilityTag}' failed to process: {Error}", ability.AbilityTag, abilityResult.ErrorMessage);
                            return PausableTransactionResult.Failure(abilityResult.ErrorMessage ?? "Primary ability processing failed.");
                        }
                    }
                }
            }

            _logger.LogInformation("--- Phase 1: Primary Ability analysis completed successfully. ---");
            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private async Task<(PausableTransactionResult, List<Effect>)> GetTriggerAbilityEffectsAsync(GameContext gameContext, IGameEvent gameEvent, TriggerTiming timing)
        {
            var generatedEffects = new List<Effect>();
            var triggeredAbilities = await gameContext.AbilityManager.GetTriggeredAbilitiesAsync(gameEvent, gameContext, timing);

            // This logic is now refined. "Primary" abilities (cancellable, require choice) are handled
            // exclusively in AnalyzePrimaryAbilitiesAsync. This method should handle all other
            // triggered abilities (like standard Fanfares, Last Words, etc.).

            if (!triggeredAbilities.Any())
            {
                return (PausableTransactionResult.Success(new List<IGameEvent>()), generatedEffects);
            }

            _logger.LogDebug("Found {Count} chained abilities for event {EventType} at timing: {Timing}", triggeredAbilities.Count, gameEvent.GetType().Name, timing);

            foreach (var ability in triggeredAbilities)
            {
                var abilityContext = gameContext with { SourceCard = ability.SourceCard, TriggeringEvent = gameEvent };
                var abilityResult = await gameContext.AbilityManager.ProcessSingleAbilityAsync(ability, abilityContext, false); // Triggered abilities are not cancellable

                // The IsPaused logic is now handled internally by TargetSelectionService.
                // We only need to handle the final success or failure.

                if (!abilityResult.IsSuccess)
                {
                    return (PausableTransactionResult.Failure(abilityResult.ErrorMessage ?? "Chained ability processing failed."), generatedEffects);
                }

                generatedEffects.AddRange(abilityResult.Effects);
            }

            return (PausableTransactionResult.Success(new List<IGameEvent>()), generatedEffects);
        }

        private async Task<PausableTransactionResult> CommitTransactionAsync(GameContext gameContext, IEnumerable<IGameEvent> initialEvents)
        {
            _logger.LogInformation("--- Phase 3: Committing Transaction ---");
            _effectQueue.Clear();
            var eventStagingArea = new Queue<IGameEvent>(initialEvents);
            var processedEvents = new List<IGameEvent>();
            var bundleId = Guid.NewGuid();

            while (eventStagingArea.Count > 0 || _effectQueue.Count > 0)
            {
                if (eventStagingArea.Count > 0)
                {
                    var currentEvent = eventStagingArea.Dequeue();
                    var result = await ProcessEventRecursivelyAsync(gameContext, currentEvent, processedEvents);
                    if (!result.IsSuccess) return result;
                }
                else
                {
                    var currentEffect = _effectQueue.Dequeue();
                    var result = await ProcessEffectAsync(currentEffect, gameContext, processedEvents);
                    if (!result.IsSuccess) return result;
                }
            }

            var broadcastPlan = new List<(IGameEvent Event, string? Announcement, IReadOnlyList<GameEventDto> Dtos)>(processedEvents.Count);
            var recordedEvents = new List<CommittedEventWriteDto>(processedEvents.Count);

            foreach (var gameEvent in processedEvents)
            {
                var announcement = (gameEvent as ILogAnnounceableEvent)?.GetAnnouncement(gameContext.CurrentGameState);
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

        private async Task<PausableTransactionResult> ProcessEventRecursivelyAsync(GameContext gameContext, IGameEvent gameEvent, List<IGameEvent> processedEvents)
        {
            _logger.LogDebug("Processing event: {EventType} ({EventId})", gameEvent.GetType().Name, gameEvent.EventId);
            processedEvents.Add(gameEvent);

            // NOTE: Broadcasting is now deferred to the end of the CommitTransactionAsync method.
            // This method now only focuses on processing the domain logic.
            if (gameEvent is TurnStartEvent turnStartEvent && turnStartEvent.StartingPlayer != null)
            {
                var expiryEvents = gameContext.BuffService.CollectStartOfNextTurnExpiryEvents(gameContext, turnStartEvent.StartingPlayer);
                foreach (var expiryEvent in expiryEvents)
                {
                    var expiryResult = await ProcessEventRecursivelyAsync(gameContext, expiryEvent, processedEvents);
                    if (!expiryResult.IsSuccess) return expiryResult;
                }

                gameContext.BuffService.RefreshDerivedStateCache(gameContext, "turn_start");
            }

            // 1. 'Before' phase: Effects are executed immediately and their events are processed recursively.
            var (beforeResult, beforeEffects) = await GetTriggerAbilityEffectsAsync(gameContext, gameEvent, TriggerTiming.Before);
            if (!beforeResult.IsSuccess) return beforeResult;
            foreach (var effect in beforeEffects)
            {
                var result = await ProcessEffectAsync(effect, gameContext, processedEvents);
                if (!result.IsSuccess) return result;
            }

            // 2. 'On' phase: Effects are enqueued for later processing.
            var (onResult, onEffects) = await GetTriggerAbilityEffectsAsync(gameContext, gameEvent, TriggerTiming.On);
            if (!onResult.IsSuccess) return onResult;
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
                    var recursiveResult = await ProcessEventRecursivelyAsync(gameContext, subEvent, processedEvents);
                    if (!recursiveResult.IsSuccess) return recursiveResult;
                }

                if (gameEvent is TurnEndEvent)
                {
                    var expiryEvents = gameContext.BuffService.CollectEndOfTurnExpiryEvents(gameContext);
                    foreach (var expiryEvent in expiryEvents)
                    {
                        var expiryResult = await ProcessEventRecursivelyAsync(gameContext, expiryEvent, processedEvents);
                        if (!expiryResult.IsSuccess) return expiryResult;
                    }

                    gameContext.BuffService.RefreshDerivedStateCache(gameContext, "turn_end");
                }
            }

            // 4. 'After' phase: Effects are enqueued for later processing.
            var (afterResult, afterEffects) = await GetTriggerAbilityEffectsAsync(gameContext, gameEvent, TriggerTiming.After);
            if (!afterResult.IsSuccess) return afterResult;
            foreach (var effect in afterEffects)
            {
                _effectQueue.Enqueue(effect);
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private async Task<PausableTransactionResult> ProcessChainedAbilitiesAsync(Effect executedEffect, GameContext context, GameStateActionResult effectResult, List<IGameEvent> processedEvents)
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
                        CurrentEffect = executedEffect
                    };

                    var result = await context.AbilityManager.ProcessSingleAbilityAsync(chainedAbility, abilityContext, false);

                    if (!result.IsSuccess)
                    {
                        _logger.LogError("Chained ability {AbilityTag} failed: {Error}", chainedAbility.AbilityTag, result.ErrorMessage);
                        continue;
                    }

                    // The IsPaused logic is now handled internally by TargetSelectionService.

                    foreach (var newEffect in result.Effects)
                    {
                        var subEffectResult = await ProcessEffectAsync(newEffect, abilityContext, processedEvents);
                        if (!subEffectResult.IsSuccess)
                        {
                            return subEffectResult;
                        }
                    }
                }
            }
            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private async Task<PausableTransactionResult> ProcessEffectAsync(Effect effect, GameContext gameContext, List<IGameEvent> processedEvents)
        {
            _logger.LogDebug("Processing effect: {EffectType}", effect.Type);

            // --- BUG-025 FIX START ---
            // Create a new, precise context for this specific effect execution.
            // This ensures that DSLs and handlers use the context in which the effect was generated,
            // not the broader context of the event that might have triggered it.
            var sourceCard = effect.SourceCardId.HasValue
                ? gameContext.CurrentGameState.FindCardInstance(effect.SourceCardId.Value)
                : null;

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
                CurrentEffect = effect
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

            var effectResult = _effectExecutor.ExecuteEffect(effect, effectContext); // Use the new context
            if (!effectResult.Success)
            {
                var errorMessage = $"Effect execution failed for {effect.Type}.";
                _logger.LogError(errorMessage);
                return PausableTransactionResult.Failure(errorMessage);
            }

            // Pass the new context to chained abilities as well
            var chainedResult = await ProcessChainedAbilitiesAsync(effect, effectContext, effectResult, processedEvents);
            if (!chainedResult.IsSuccess) return chainedResult;

            foreach (var newEvent in effectResult.GeneratedEvents)
            {
                // Events generated by an effect should also be processed within the same specific context.
                var result = await ProcessEventRecursivelyAsync(effectContext, newEvent, processedEvents);
                if (!result.IsSuccess) return result;
            }

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

    }
}
