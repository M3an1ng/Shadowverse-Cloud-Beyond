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
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Events.Services;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Options;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Reflection;
using System.Linq.Expressions;
using System.Globalization;
using System.Text.Json;
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
        private readonly IWinrateStatsService _winrateStatsService;
        private readonly IMatchService? _matchService;
        private readonly ReplayOptions _replayOptions;
        private readonly Queue<Effect> _effectQueue = new();
        private readonly HashSet<Guid> _activeEffectExecutionIds = new();
        private readonly HashSet<SameNameTriggerKey> _sameNameTriggeredInTransaction = new();
        private readonly Dictionary<DeferredTargetResolutionKey, IReadOnlyDictionary<string, List<TargetInfo>>> _deferredTargetResolutionCache = new();
        private PreflightSession? _activePreflightSession;
        private static readonly ConcurrentDictionary<EventType, Type?> ReplacementEventTypeCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<ReplacementEventParameter>> ReplacementEventParameterCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<ReplacementConstructorBinding>> ReplacementEventConstructorCache = new();
        private static readonly ConcurrentDictionary<Type, Func<object?, (bool Ok, object? Value, string? ErrorMessage)>> ValueCoercerCache = new();
        private enum DecisionResolutionPhase
        {
            Preflight,
            Commit
        }

        private enum InterceptExecutionPhase
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

        private readonly record struct TargetSignatureKey(
            Guid AbilityRuntimeId,
            Guid TriggerEventId,
            string TargetKey);

        private readonly record struct SameNameTriggerKey(
            Guid OwnerPlayerId,
            long AbilityDefinitionId);

        private readonly record struct DeferredTargetResolutionKey(
            Guid AbilityRuntimeId,
            Guid TriggerEventId);

        private readonly record struct ChainedAbilitySnapshotKey(
            Guid TriggerEventId,
            Guid EffectId,
            int ExecutionIndex);

        private sealed class CommitPlanItem
        {
            public Guid RootEventId { get; init; }
            public string Step { get; init; } = string.Empty;
        }

        private sealed class CommitPlan
        {
            public List<CommitPlanItem> PlanItems { get; init; } = new();
            public Dictionary<DecisionCacheKey, DecisionInputSignature> DecisionSignatures { get; init; } = new();
            public Dictionary<TargetSignatureKey, string> TargetSignatures { get; init; } = new();
            public string StateFingerprint { get; init; } = string.Empty;
        }

        private sealed class TargetSignatureSnapshot
        {
            public Ability Ability { get; init; } = null!;
            public Guid TriggerEventId { get; init; }
            public string TargetKey { get; init; } = string.Empty;
            public string Signature { get; init; } = string.Empty;
        }

        private sealed class PreflightSession
        {
            public Guid TransactionId { get; init; }
            public IReadOnlyList<Guid> ActionRootEventIds { get; init; } = Array.Empty<Guid>();
            public Dictionary<DecisionCacheKey, PreflightDecisionState> DecisionSnapshots { get; } = new();
            public HashSet<SameNameTriggerKey> SameNameTriggeredInPreflight { get; } = new();
            public Dictionary<TargetSignatureKey, TargetSignatureSnapshot> TargetSignatures { get; } = new();
            public Dictionary<ChainedAbilitySnapshotKey, List<IAbility>> ChainedAbilitySnapshots { get; } = new();
            public Dictionary<Guid, List<IAbility>> AlternatePlayAbilitiesByEvent { get; } = new();
            public CommitPlan CommitPlan { get; set; } = new();
            public string StateFingerprint { get; init; } = string.Empty;
            public bool CommitStarted { get; set; }
            public bool HasIncompletePreflight { get; set; }
            public string? IncompleteReason { get; set; }
        }

        private sealed class PreflightDecisionState
        {
            public DecisionInputSignature Signature { get; init; }
            public Ability.DecisionInput? PreparedInput { get; init; }
            public List<int> SelectedIndexes { get; init; } = new();
            public List<IAbility> BranchAbilities { get; init; } = new();
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
            IWinrateStatsService winrateStatsService,
            IOptions<ReplayOptions> replayOptions,
            IMatchService? matchService = null)
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
            _winrateStatsService = winrateStatsService;
            _matchService = matchService;
            _replayOptions = replayOptions.Value;
        }

        private string ResolveRoomScopedId(string gameId)
        {
            if (!string.IsNullOrWhiteSpace(gameId)
                && _matchService != null
                && _matchService.TryGetRoomIdByGameId(gameId, out var roomId)
                && !string.IsNullOrWhiteSpace(roomId))
            {
                return roomId;
            }

            return gameId;
        }

        /// <inheritdoc />
        public async Task<PausableTransactionResult> ProcessTransactionAsync(GameContext gameContext, IEnumerable<IGameEvent> initialEvents)
        {
            var initialEventList = initialEvents.ToList();
            _logger.LogInformation("--- Transaction Start: Processing {EventCount} initial events. ---", initialEventList.Count);

            try
            {
                ClearActivePreflightSession();
                ResetSameNameTriggerLimitState();
                ResetAbilityEventFlags(gameContext);
                _deferredTargetResolutionCache.Clear();
                _activeEffectExecutionIds.Clear();

                var expansionResult = ExpandRequestToActionRoots(gameContext, initialEventList);
                if (expansionResult.Result.ShouldStop)
                {
                    return expansionResult.Result;
                }

                _activePreflightSession = BuildPreflightSession(gameContext, expansionResult.ActionRootEvents);

                // Phase 1: Analyze and resolve primary abilities (Cancellable Part)
                var analysisResult = await AnalyzePrimaryAbilitiesAsync(gameContext, expansionResult.ActionRootEvents);
                if (analysisResult.ShouldStop)
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

                var commitPlanBuildResult = BuildCommitPlan();
                if (commitPlanBuildResult.ShouldStop)
                {
                    return commitPlanBuildResult;
                }

                var commitValidationResult = ValidateCommitPreflightConsistency(gameContext);
                if (commitValidationResult.ShouldStop)
                {
                    return commitValidationResult;
                }

                // Phase 3: Commit events and handle subsequent effects
                var commitResult = await CommitTransactionAsync(gameContext, expansionResult.CommitEvents);

                return commitResult;
            }
            finally
            {
                ResetAbilityEventFlags(gameContext);
                ClearActivePreflightSession();
                ResetSameNameTriggerLimitState();
                _deferredTargetResolutionCache.Clear();
                _activeEffectExecutionIds.Clear();
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

        private void ClearActivePreflightSession()
        {
            _activePreflightSession = null;
        }

        private void ResetSameNameTriggerLimitState()
        {
            _sameNameTriggeredInTransaction.Clear();
        }

        private PreflightSession BuildPreflightSession(GameContext gameContext, IReadOnlyList<IGameEvent> actionRootEvents)
        {
            return new PreflightSession
            {
                TransactionId = Guid.NewGuid(),
                ActionRootEventIds = actionRootEvents.Select(evt => evt.EventId).ToList(),
                StateFingerprint = BuildStateFingerprint(gameContext.CurrentGameState)
            };
        }

        private PausableTransactionResult BuildCommitPlan()
        {
            if (_activePreflightSession == null)
            {
                return PausableTransactionResult.Failure("Preflight session is missing.");
            }

            if (_activePreflightSession.HasIncompletePreflight)
            {
                return PausableTransactionResult.Failure(
                    $"PreflightIncomplete: {_activePreflightSession.IncompleteReason ?? "unresolved preflight steps remain."}");
            }

            var decisionSignatures = _activePreflightSession.DecisionSnapshots
                .ToDictionary(pair => pair.Key, pair => pair.Value.Signature);

            var targetSignatures = _activePreflightSession.TargetSignatures
                .ToDictionary(pair => pair.Key, pair => pair.Value.Signature);

            _activePreflightSession.CommitPlan = new CommitPlan
            {
                PlanItems = _activePreflightSession.ActionRootEventIds
                    .Select(rootId => new CommitPlanItem
                    {
                        RootEventId = rootId,
                        Step = "ExecuteActionRoot"
                    })
                    .ToList(),
                DecisionSignatures = decisionSignatures,
                TargetSignatures = targetSignatures,
                StateFingerprint = _activePreflightSession.StateFingerprint
            };

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private PausableTransactionResult ValidateCommitPreflightConsistency(GameContext gameContext)
        {
            if (_activePreflightSession == null)
            {
                return PausableTransactionResult.Failure("Preflight session is missing.");
            }

            var currentFingerprint = BuildStateFingerprint(gameContext.CurrentGameState);
            if (!string.Equals(currentFingerprint, _activePreflightSession.CommitPlan.StateFingerprint, StringComparison.Ordinal))
            {
                return PausableTransactionResult.Failure("Preflight consistency check failed: state fingerprint mismatch.");
            }

            foreach (var pair in _activePreflightSession.CommitPlan.DecisionSignatures)
            {
                if (!_activePreflightSession.DecisionSnapshots.TryGetValue(pair.Key, out var snapshot))
                {
                    return PausableTransactionResult.Failure(
                        $"Preflight consistency check failed: missing decision snapshot for ability {pair.Key.AbilityRuntimeId}, effect {pair.Key.EffectIndex}, execution {pair.Key.ExecutionIndex}.");
                }

                if (snapshot.Signature != pair.Value)
                {
                    return PausableTransactionResult.Failure(
                        $"Preflight consistency check failed: decision signature mismatch for ability {pair.Key.AbilityRuntimeId}, effect {pair.Key.EffectIndex}, execution {pair.Key.ExecutionIndex}.");
                }
            }

            foreach (var pair in _activePreflightSession.CommitPlan.TargetSignatures)
            {
                if (!_activePreflightSession.TargetSignatures.TryGetValue(pair.Key, out var snapshot))
                {
                    return PausableTransactionResult.Failure(
                        $"Preflight consistency check failed: missing target snapshot for ability {pair.Key.AbilityRuntimeId}, key '{pair.Key.TargetKey}'.");
                }

                if (!string.Equals(snapshot.Signature, pair.Value, StringComparison.Ordinal))
                {
                    return PausableTransactionResult.Failure(
                        $"Preflight consistency check failed: target signature mismatch for ability {pair.Key.AbilityRuntimeId}, key '{pair.Key.TargetKey}'.");
                }

                var target = snapshot.Ability.Targets.FirstOrDefault(t => string.Equals(t.Key, snapshot.TargetKey, StringComparison.Ordinal));
                if (target == null)
                {
                    return PausableTransactionResult.Failure(
                        $"Preflight consistency check failed: target '{snapshot.TargetKey}' is missing for ability {snapshot.Ability.RuntimeId}.");
                }

                var currentSignature = BuildTargetSignature(target.ResolvedTargets);
                if (!string.Equals(currentSignature, snapshot.Signature, StringComparison.Ordinal))
                {
                    return PausableTransactionResult.Failure(
                        $"Preflight consistency check failed: target signature mismatch for ability {snapshot.Ability.RuntimeId}, key '{snapshot.TargetKey}'.");
                }
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private static string BuildStateFingerprint(GameState gameState)
        {
            var player1Field = string.Join(",",
                gameState.Player1.Field
                    .Where(card => card != null)
                    .Select(card => card.InstanceId.ToString("N"))
                    .OrderBy(id => id));
            var player2Field = string.Join(",",
                gameState.Player2.Field
                    .Where(card => card != null)
                    .Select(card => card.InstanceId.ToString("N"))
                    .OrderBy(id => id));

            return string.Join("|",
                gameState.StateRevision.ToString(CultureInfo.InvariantCulture),
                gameState.CurrentPlayerId?.ToString("N") ?? string.Empty,
                gameState.Player1.CurrentHealth.ToString(CultureInfo.InvariantCulture),
                gameState.Player2.CurrentHealth.ToString(CultureInfo.InvariantCulture),
                gameState.Player1.Hand.Count.ToString(CultureInfo.InvariantCulture),
                gameState.Player2.Hand.Count.ToString(CultureInfo.InvariantCulture),
                player1Field,
                player2Field);
        }

        private void CaptureAbilityTargetSignatures(Ability ability, Guid triggeringEventId)
        {
            if (_activePreflightSession == null || ability == null || triggeringEventId == Guid.Empty)
            {
                return;
            }

            foreach (var target in ability.Targets.Where(target => target.Config.IsEnabled))
            {
                var key = new TargetSignatureKey(ability.RuntimeId, triggeringEventId, target.Key);
                _activePreflightSession.TargetSignatures[key] = new TargetSignatureSnapshot
                {
                    Ability = ability,
                    TriggerEventId = triggeringEventId,
                    TargetKey = target.Key,
                    Signature = BuildTargetSignature(target.ResolvedTargets)
                };
            }
        }

        private static string BuildTargetSignature(IReadOnlyList<TargetInfo> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(",",
                targets
                    .Select(target => target?.InstanceId ?? Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .OrderBy(id => id)
                    .Select(id => id.ToString("N")));
        }

        private bool TryGetPreflightDecisionState(DecisionCacheKey cacheKey, out PreflightDecisionState state)
        {
            if (_activePreflightSession != null
                && _activePreflightSession.DecisionSnapshots.TryGetValue(cacheKey, out state!))
            {
                return true;
            }

            state = null!;
            return false;
        }

        private bool HasPreflightDecisionState(DecisionCacheKey cacheKey)
        {
            return _activePreflightSession != null
                && _activePreflightSession.DecisionSnapshots.ContainsKey(cacheKey);
        }

        private HashSet<SameNameTriggerKey>? GetPreflightSameNameState()
        {
            return _activePreflightSession?.SameNameTriggeredInPreflight;
        }

        private void UpsertPreflightDecisionState(
            DecisionCacheKey cacheKey,
            DecisionInputSignature signature,
            IReadOnlyList<int> selectedIndexes)
        {
            if (_activePreflightSession == null)
            {
                return;
            }

            _activePreflightSession.DecisionSnapshots[cacheKey] = new PreflightDecisionState
            {
                Signature = signature,
                PreparedInput = _activePreflightSession.DecisionSnapshots.TryGetValue(cacheKey, out var existing)
                    ? existing.PreparedInput
                    : null,
                SelectedIndexes = selectedIndexes.ToList(),
                BranchAbilities = _activePreflightSession.DecisionSnapshots.TryGetValue(cacheKey, out existing)
                    ? existing.BranchAbilities
                    : new List<IAbility>()
            };
        }

        private void SetPreflightPreparedDecisionInput(DecisionCacheKey cacheKey, Ability.DecisionInput preparedInput)
        {
            if (_activePreflightSession == null)
            {
                return;
            }

            _activePreflightSession.DecisionSnapshots[cacheKey] = new PreflightDecisionState
            {
                Signature = _activePreflightSession.DecisionSnapshots.TryGetValue(cacheKey, out var existing)
                    ? existing.Signature
                    : default,
                PreparedInput = preparedInput,
                SelectedIndexes = _activePreflightSession.DecisionSnapshots.TryGetValue(cacheKey, out existing)
                    ? existing.SelectedIndexes
                    : new List<int>(),
                BranchAbilities = _activePreflightSession.DecisionSnapshots.TryGetValue(cacheKey, out existing)
                    ? existing.BranchAbilities
                    : new List<IAbility>()
            };
        }

        private void SetPreflightDecisionBranchAbilities(DecisionCacheKey cacheKey, IReadOnlyList<IAbility> branchAbilities)
        {
            if (_activePreflightSession == null
                || !_activePreflightSession.DecisionSnapshots.TryGetValue(cacheKey, out var state))
            {
                return;
            }

            _activePreflightSession.DecisionSnapshots[cacheKey] = new PreflightDecisionState
            {
                Signature = state.Signature,
                PreparedInput = state.PreparedInput,
                SelectedIndexes = state.SelectedIndexes,
                BranchAbilities = branchAbilities.ToList()
            };
        }

        private static bool TryCreateChainedAbilitySnapshotKey(
            Effect effect,
            out ChainedAbilitySnapshotKey key)
        {
            key = default;
            if (effect == null || effect.Id == Guid.Empty || effect.TriggeredEventId == Guid.Empty)
            {
                return false;
            }

            key = new ChainedAbilitySnapshotKey(
                effect.TriggeredEventId,
                effect.Id,
                Math.Max(0, effect.ExecutionIndex));
            return true;
        }

        private void UpsertPreflightChainedAbilitySnapshot(
            ChainedAbilitySnapshotKey cacheKey,
            IReadOnlyList<IAbility> chainedAbilities)
        {
            if (_activePreflightSession == null)
            {
                return;
            }

            _activePreflightSession.ChainedAbilitySnapshots[cacheKey] = chainedAbilities.ToList();
        }

        private bool TryTakePreflightChainedAbilitySnapshot(
            ChainedAbilitySnapshotKey cacheKey,
            out IReadOnlyList<IAbility> chainedAbilities)
        {
            if (_activePreflightSession != null
                && _activePreflightSession.ChainedAbilitySnapshots.TryGetValue(cacheKey, out var snapshot))
            {
                _activePreflightSession.ChainedAbilitySnapshots.Remove(cacheKey);
                chainedAbilities = snapshot;
                return true;
            }

            chainedAbilities = Array.Empty<IAbility>();
            return false;
        }

        private void MarkPreflightIncomplete(string reason)
        {
            if (_activePreflightSession == null)
            {
                return;
            }

            _activePreflightSession.HasIncompletePreflight = true;
            if (string.IsNullOrWhiteSpace(_activePreflightSession.IncompleteReason))
            {
                _activePreflightSession.IncompleteReason = reason;
            }
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
                CaptureEventCurrentPlayerSnapshot(gameContext, gameEvent);

                // A primary ability is linked to a direct player action.
                if (gameEvent is IPlayerActionEvent)
                {
                    var primarySourceCard = ResolvePrimarySourceCard(gameEvent);
                    var primaryContext = gameContext with { TriggeringEvent = gameEvent, SourceCard = primarySourceCard, IsCancellable = true };

                    var triggeredAbilities = await gameContext.AbilityManager.GetTriggeredAbilitiesAsync(gameEvent, primaryContext, TriggerTiming.On)
                        ?? new List<IAbility>();

                    var alternatePlayAbilitiesResult = await ResolveAlternatePlayAbilitiesAsync(primaryContext, gameEvent, TriggerTiming.On);
                    if (!alternatePlayAbilitiesResult.Result.IsSuccess)
                    {
                        return alternatePlayAbilitiesResult.Result;
                    }

                    if (alternatePlayAbilitiesResult.Abilities.Count > 0)
                    {
                        triggeredAbilities = RemovePrimaryAbilitiesForSource(triggeredAbilities, primarySourceCard);
                        triggeredAbilities.AddRange(alternatePlayAbilitiesResult.Abilities);
                        if (_activePreflightSession != null)
                        {
                            _activePreflightSession.AlternatePlayAbilitiesByEvent[gameEvent.EventId] = alternatePlayAbilitiesResult.Abilities;
                        }
                    }

                    triggeredAbilities = FilterTriggeredAbilitiesByTransactionSameNameLimit(
                        triggeredAbilities,
                        "preflight-primary",
                        GetPreflightSameNameState());

                    // According to the design doc, a "Primary Ability" is one where the ability's source
                    // is the same as the event's source. The user has clarified to use gameContext.SourceCard.
                    var primaryAbilities = triggeredAbilities.Where(ability =>
                        ability.SourceCard != null &&
                        primarySourceCard != null &&
                        ability.SourceCard.InstanceId == primarySourceCard.InstanceId
                    ).ToList();

                    if (!primaryAbilities.Any())
                    {
                        _logger.LogInformation("No primary abilities found for event {EventType}.", gameEvent.GetType().Name);
                        continue;
                    }

                    _logger.LogInformation("Found {Count} primary abilities for event {EventType}.", primaryAbilities.Count, gameEvent.GetType().Name);

                    foreach (var ability in primaryAbilities)
                    {
                        var requiresPreflight = RequiresAbilityPreflight(
                            ability,
                            executionIsCancellable: true,
                            out var requiresTargetChoicePreflight,
                            out var requiresDecisionPreflight,
                            out var requiresChainedTargetChoicePreflight);

                        // If no target-choice preflight is needed and there is no Choose flow, skip preflight and handle it during submit.
                        if (!requiresPreflight)
                        {
                            _logger.LogDebug(
                                "Skipping preflight for primary ability '{AbilityKeywords}' (RuntimeId: {RuntimeId}) because it requires neither target choice nor decision preflight.",
                                ability.KeywordSummary,
                                ability.RuntimeId);
                            continue;
                        }

                        // Decision-only preflight should not lock targets before commit.
                        var deferTargetResolution = !requiresTargetChoicePreflight && requiresDecisionPreflight;

                        // Preflight primary abilities are cancellable.
                        var abilityContext = gameContext with { SourceCard = ability.SourceCard, TriggeringEvent = gameEvent, IsCancellable = true };
                        var abilityResult = await gameContext.AbilityManager.ProcessSingleAbilityAsync(
                            ability,
                            abilityContext,
                            true,
                            deferTargetResolution,
                            recordTrigger: false);
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

                        if (ability is Ability resolvedAbility)
                        {
                            CaptureAbilityTargetSignatures(resolvedAbility, gameEvent.EventId);
                        }

                        if (requiresDecisionPreflight && ability is Ability decisionConcreteAbility)
                        {
                            var preflightDecisionResult = await PreflightDecisionEffectsAsync(abilityContext, decisionConcreteAbility, gameEvent, abilityResult.Effects);
                            if (preflightDecisionResult.IsCancelled)
                            {
                                decisionConcreteAbility.ClearDecisionCache(gameEvent.EventId);
                                return preflightDecisionResult;
                            }

                            if (preflightDecisionResult.ShouldStop)
                            {
                                return preflightDecisionResult;
                            }
                        }

                        if (requiresChainedTargetChoicePreflight)
                        {
                            var chainedPreflightResult = await PreflightChainedTargetChoiceEffectsAsync(
                                abilityContext,
                                gameEvent,
                                abilityResult.Effects);
                            if (chainedPreflightResult.IsCancelled)
                            {
                                if (ability is Ability concreteAbility)
                                {
                                    concreteAbility.ClearDecisionCache(gameEvent.EventId);
                                }
                                return chainedPreflightResult;
                            }

                            if (chainedPreflightResult.ShouldStop)
                            {
                                return chainedPreflightResult;
                            }
                        }

                        if (ability is Ability concretePrimaryAbility)
                        {
                            concretePrimaryAbility.CachePreflightPrimaryAbilityResult(
                                gameEvent.EventId,
                                abilityResult.Effects,
                                abilityResult.GeneratedEvents,
                                abilityResult.ShouldEmitAbilityTriggeredEvent);
                        }
                    }
                }
            }

            _logger.LogInformation("--- Phase 1: Primary Ability analysis completed successfully. ---");
            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private async Task<(PausableTransactionResult Result, List<IAbility> Abilities)> ResolveAlternatePlayAbilitiesAsync(
            GameContext gameContext,
            IGameEvent gameEvent,
            TriggerTiming timing)
        {
            if (gameEvent is not PlayCardEvent playCardEvent
                || playCardEvent.PlayMode is not (PlayMode.Accelerate or PlayMode.Crystalize)
                || !playCardEvent.AlternateCardDefinitionId.HasValue)
            {
                return (PausableTransactionResult.Success(new List<IGameEvent>()), new List<IAbility>());
            }

            var sourceCard = playCardEvent.PlayedCard;
            if (sourceCard == null)
            {
                return (PausableTransactionResult.Failure("Alternate play preflight requires a source card."), new List<IAbility>());
            }

            var dataProvider = gameContext.ServiceProvider.GetService<IGameDataProvider>();
            if (dataProvider == null)
            {
                return (PausableTransactionResult.Failure("Alternate play preflight requires IGameDataProvider."), new List<IAbility>());
            }

            var alternateDefinition = dataProvider.GetCardDefinition(playCardEvent.AlternateCardDefinitionId.Value);
            if (alternateDefinition == null)
            {
                return (PausableTransactionResult.Failure($"Alternate card definition {playCardEvent.AlternateCardDefinitionId.Value} not found."), new List<IAbility>());
            }

            if (playCardEvent.PlayMode == PlayMode.Accelerate && alternateDefinition.Type != CardType.Spell)
            {
                return (PausableTransactionResult.Failure("Accelerate must play as a Spell card."), new List<IAbility>());
            }

            if (playCardEvent.PlayMode == PlayMode.Crystalize && alternateDefinition.Type != CardType.Amulet)
            {
                return (PausableTransactionResult.Failure("Crystalize must play as an Amulet card."), new List<IAbility>());
            }

            var abilityFactory = gameContext.ServiceProvider.GetService<IAbilityFactory>();
            if (abilityFactory == null)
            {
                return (PausableTransactionResult.Failure("Alternate play preflight requires IAbilityFactory."), new List<IAbility>());
            }

            var abilityConfigs = dataProvider.GetAbilityConfigs(alternateDefinition.Id) ?? new List<AbilityConfig>();
            if (abilityConfigs.Count == 0)
            {
                return (PausableTransactionResult.Success(new List<IGameEvent>()), new List<IAbility>());
            }

            var abilities = abilityFactory
                .CreateAbilities(sourceCard, abilityConfigs)
                .Cast<IAbility>()
                .ToList();

            var triggeredAbilities = new List<IAbility>();
            foreach (var ability in abilities)
            {
                if (ability.TriggerConfig.Timing == timing && await ability.CanTrigger(gameEvent, gameContext))
                {
                    triggeredAbilities.Add(ability);
                }
            }

            return (PausableTransactionResult.Success(new List<IGameEvent>()), triggeredAbilities);
        }

        private static List<IAbility> RemovePrimaryAbilitiesForSource(IEnumerable<IAbility> abilities, CardInstance? sourceCard)
        {
            if (sourceCard == null)
            {
                return abilities.ToList();
            }

            return abilities
                .Where(ability => ability.SourceCard?.InstanceId != sourceCard.InstanceId)
                .ToList();
        }

        private static bool RequiresTargetChoicePreflight(IAbility ability)
        {
            if (ability?.Targets == null || ability.Targets.Count == 0)
            {
                return false;
            }

            return ability.Targets.Any(target =>
                target?.Config?.IsEnabled == true &&
                target.Config.PlayerChoiceOptions?.IsRequired == true);
        }

        private static bool RequiresDecisionPreflight(IAbility ability)
        {
            if (ability is not Ability concreteAbility)
            {
                return false;
            }

            var effects = concreteAbility.Config?.Effects;
            if (effects == null || effects.Count == 0)
            {
                return false;
            }

            return effects.Any(effect =>
                effect != null &&
                effect.Type == EffectType.Choose);
        }

        private static bool RequiresChainedTargetChoicePreflight(IAbility ability)
        {
            if (ability is not Ability concreteAbility)
            {
                return false;
            }

            return concreteAbility.Config?.Effects?.Any(effect => HasTargetChoiceInChainedAbilities(effect)) == true;
        }

        private static bool HasTargetChoiceInChainedAbilities(EffectConfig? effectConfig)
        {
            if (effectConfig == null || !effectConfig.IsEnabled || effectConfig.ChainedAbilities.Count == 0)
            {
                return false;
            }

            return effectConfig.ChainedAbilities.Any(HasTargetChoiceInAbilityConfig);
        }

        private static bool HasTargetChoiceInAbilityConfig(AbilityConfig? abilityConfig)
        {
            if (abilityConfig == null || !abilityConfig.IsEnabled)
            {
                return false;
            }

            if (abilityConfig.Targets.Any(pair =>
                    pair.Value?.IsEnabled == true &&
                    pair.Value.PlayerChoiceOptions?.IsRequired == true))
            {
                return true;
            }

            return abilityConfig.Effects.Any(effect => HasTargetChoiceInChainedAbilities(effect));
        }

        private static bool RequiresAbilityPreflight(
            IAbility ability,
            bool executionIsCancellable,
            out bool requiresTargetChoicePreflight,
            out bool requiresDecisionPreflight,
            out bool requiresChainedTargetChoicePreflight)
        {
            requiresTargetChoicePreflight = RequiresTargetChoicePreflight(ability);
            requiresDecisionPreflight = RequiresDecisionPreflight(ability);
            requiresChainedTargetChoicePreflight = RequiresChainedTargetChoicePreflight(ability);

            if (!executionIsCancellable)
            {
                return false;
            }

            return requiresTargetChoicePreflight
                || requiresDecisionPreflight
                || requiresChainedTargetChoicePreflight;
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
                FusionInvokedEvent fusionInvokedEvent => fusionInvokedEvent.FusionCard,
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
                if (effect.Type != EffectType.Choose)
                {
                    continue;
                }

                var sourceCard = effect.SourceCardId.HasValue
                    ? gameContext.CurrentGameState.FindCardInstance(effect.SourceCardId.Value)
                    : decisionAbility.SourceCard;

                var preflightEffectContext = preflightContext with
                {
                    SourceCard = sourceCard,
                    TriggeringEvent = triggeringEvent,
                    CurrentEffect = effect,
                    ResolvedTargets = effect.ResolvedTargets
                };

                var repeatCountResult = ResolveEffectRepeatCount(effect, preflightEffectContext);
                if (!repeatCountResult.Success)
                {
                    return PausableTransactionResult.Failure(repeatCountResult.ErrorMessage ?? "Failed to resolve repeat count.");
                }

                var totalExecutions = repeatCountResult.RepeatCount + 1;
                var preflightExecutionCount = GetPreflightDecisionExecutionCount(effect, preflightEffectContext, repeatCountResult.RepeatCount);
                if (preflightExecutionCount < totalExecutions)
                {
                    MarkPreflightIncomplete(
                        $"decision effect {effect.Type} ({effect.Id}) requires {totalExecutions} executions but only {preflightExecutionCount} can be preflighted deterministically.");
                }

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
                    if (decisionPreparation.Result.ShouldStop)
                    {
                        return decisionPreparation.Result;
                    }

                    if (decisionPreparation.DecisionEvent == null)
                    {
                        continue;
                    }

                    if (decisionPreparation.DecisionAbility != null
                        && decisionPreparation.PreparedInput != null
                        && TryCreateDecisionCacheKey(
                            effect,
                            decisionPreparation.DecisionAbility,
                            effect.Type == EffectType.Random,
                            out var preflightDecisionCacheKey))
                    {
                        SetPreflightPreparedDecisionInput(preflightDecisionCacheKey, decisionPreparation.PreparedInput);
                    }

                    var (interceptResult, interceptedEvent, _, _, preflightPrevented) = await ApplyBeforeInterceptsAsync(
                        effectContext,
                        decisionPreparation.DecisionEvent,
                        processedEvents,
                        phase: InterceptExecutionPhase.Preflight);
                    if (interceptResult.ShouldStop)
                    {
                        return interceptResult;
                    }

                    if (preflightPrevented)
                    {
                        continue;
                    }

                    var decisionResolveResult = await ResolveDecisionEventIfNeededAsync(effectContext, interceptedEvent, DecisionResolutionPhase.Preflight);
                    if (decisionResolveResult.IsCancelled)
                    {
                        return decisionResolveResult;
                    }

                    if (decisionResolveResult.ShouldStop)
                    {
                        return decisionResolveResult;
                    }

                    processedEvents.Add(interceptedEvent);

                    if (decisionPreparation.DecisionAbility == null)
                    {
                        continue;
                    }

                    var branchResolveResult = ResolveDecisionAbilitiesFromEvent(
                        effect,
                        effectContext,
                        decisionPreparation.DecisionAbility,
                        interceptedEvent,
                        out var branchAbilities);
                    if (branchResolveResult.ShouldStop)
                    {
                        return branchResolveResult;
                    }

                    if (TryCreateDecisionCacheKey(
                            effect,
                            decisionPreparation.DecisionAbility,
                            effect.Type == EffectType.Random,
                            out var cacheKey))
                    {
                        SetPreflightDecisionBranchAbilities(cacheKey, branchAbilities);
                    }

                    if (branchAbilities.Count == 0)
                    {
                        continue;
                    }

                    var branchPreflightResult = await PreflightDecisionBranchAbilitiesAsync(
                        effectContext,
                        triggeringEvent,
                        effect,
                        branchAbilities);
                    if (branchPreflightResult.ShouldStop)
                    {
                        return branchPreflightResult;
                    }
                }
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private async Task<PausableTransactionResult> PreflightDecisionBranchAbilitiesAsync(
            GameContext effectContext,
            IGameEvent triggeringEvent,
            Effect parentEffect,
            IReadOnlyList<IAbility> branchAbilities)
        {
            foreach (var branchAbility in branchAbilities)
            {
                var requiresPreflight = RequiresAbilityPreflight(
                    branchAbility,
                    executionIsCancellable: true,
                    out var requiresTargetChoicePreflight,
                    out var requiresDecisionPreflight,
                    out _);
                if (!requiresPreflight)
                {
                    continue;
                }

                var deferTargetResolution = !requiresTargetChoicePreflight && requiresDecisionPreflight;

                var branchContext = effectContext with
                {
                    SourceCard = branchAbility.SourceCard,
                    TriggeringEvent = triggeringEvent,
                    CurrentEffect = parentEffect,
                    ResolvedTargets = parentEffect.ResolvedTargets,
                    IsCancellable = true
                };

                var branchResult = await effectContext.AbilityManager.ProcessSingleAbilityAsync(
                    branchAbility,
                    branchContext,
                    true,
                    deferTargetResolution,
                    recordTrigger: false);
                if (branchResult == null)
                {
                    return PausableTransactionResult.Failure(
                        $"Preflight failed for decision branch ability {branchAbility.RuntimeId}: null process result.");
                }

                if (branchResult.IsCancelled)
                {
                    return PausableTransactionResult.Cancelled();
                }

                if (!branchResult.IsSuccess)
                {
                    return PausableTransactionResult.Failure(
                        branchResult.ErrorMessage ?? $"Preflight failed for decision branch ability {branchAbility.RuntimeId}.");
                }

                if (branchAbility is Ability concreteBranchAbility)
                {
                    CaptureAbilityTargetSignatures(concreteBranchAbility, triggeringEvent.EventId);
                    if (RequiresDecisionPreflight(branchAbility))
                    {
                        var nestedResult = await PreflightDecisionEffectsAsync(
                            branchContext,
                            concreteBranchAbility,
                            triggeringEvent,
                            branchResult.Effects);
                        if (nestedResult.ShouldStop)
                        {
                            return nestedResult;
                        }
                    }
                }
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private async Task<PausableTransactionResult> PreflightChainedTargetChoiceEffectsAsync(
            GameContext gameContext,
            IGameEvent triggeringEvent,
            IReadOnlyList<Effect> effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            var preflightContext = gameContext with { IsCancellable = true };

            foreach (var effect in effects)
            {
                if (effect?.EffectConfig == null || !effect.EffectConfig.IsEnabled || effect.EffectConfig.ChainedAbilities.Count == 0)
                {
                    continue;
                }

                var sourceCard = effect.SourceCardId.HasValue
                    ? gameContext.CurrentGameState.FindCardInstance(effect.SourceCardId.Value)
                    : gameContext.SourceCard;

                var repeatContext = preflightContext with
                {
                    SourceCard = sourceCard,
                    TriggeringEvent = triggeringEvent,
                    CurrentEffect = effect,
                    ResolvedTargets = effect.ResolvedTargets
                };

                var repeatCountResult = ResolveEffectRepeatCount(effect, repeatContext);
                if (!repeatCountResult.Success)
                {
                    return PausableTransactionResult.Failure(repeatCountResult.ErrorMessage ?? "Failed to resolve repeat count.");
                }

                var preflightExecutionCount = GetPreflightChainedAbilityExecutionCount(
                    effect,
                    repeatContext,
                    repeatCountResult.RepeatCount);

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

                    var chainedAbilityResult = CreateChainedAbilitiesForExecution(effect, effectContext);
                    if (!chainedAbilityResult.Success)
                    {
                        return PausableTransactionResult.Failure(
                            chainedAbilityResult.ErrorMessage ?? $"Failed to instantiate chained abilities for effect {effect.Type}.");
                    }

                    var chainedAbilities = chainedAbilityResult.Abilities;
                    if (TryCreateChainedAbilitySnapshotKey(effect, out var snapshotKey))
                    {
                        UpsertPreflightChainedAbilitySnapshot(snapshotKey, chainedAbilities);
                    }

                    if (chainedAbilities.Count == 0)
                    {
                        continue;
                    }

                    foreach (var chainedAbility in chainedAbilities)
                    {
                        if (!RequiresTargetChoicePreflight(chainedAbility))
                        {
                            continue;
                        }

                        var chainedContext = effectContext with
                        {
                            SourceCard = chainedAbility.SourceCard,
                            TriggeringEvent = triggeringEvent,
                            CurrentEffect = effect,
                            ResolvedTargets = effect.ResolvedTargets,
                            IsCancellable = true
                        };

                        var chainedResult = await gameContext.AbilityManager.ProcessSingleAbilityAsync(
                            chainedAbility,
                            chainedContext,
                            true,
                            deferTargetResolution: false,
                            recordTrigger: false);

                        if (chainedResult == null)
                        {
                            return PausableTransactionResult.Failure(
                                $"Preflight failed for chained ability {chainedAbility.RuntimeId}: null process result.");
                        }

                        if (chainedResult.IsCancelled)
                        {
                            return PausableTransactionResult.Cancelled();
                        }

                        if (!chainedResult.IsSuccess)
                        {
                            return PausableTransactionResult.Failure(
                                chainedResult.ErrorMessage ?? $"Preflight failed for chained ability {chainedAbility.RuntimeId}.");
                        }

                        if (chainedAbility is Ability concreteChainedAbility)
                        {
                            CaptureAbilityTargetSignatures(concreteChainedAbility, triggeringEvent.EventId);
                        }
                    }
                }
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private int GetPreflightChainedAbilityExecutionCount(
            Effect effect,
            GameContext effectContext,
            int? resolvedRepeatCount = null)
        {
            var repeatCount = resolvedRepeatCount ?? ResolveEffectRepeatCount(effect, effectContext).RepeatCount;
            if (repeatCount == 0)
            {
                return 1;
            }

            if (effect.EffectConfig.RepeatCondition?.IsEnabled == true)
            {
                _logger.LogDebug(
                    "Effect {EffectType} ({EffectId}) has RepeatCondition; chained target-choice preflight executes first iteration only.",
                    effect.Type,
                    effect.Id);
                return 1;
            }

            return repeatCount + 1;
        }

        private int GetPreflightDecisionExecutionCount(Effect effect, GameContext effectContext, int? resolvedRepeatCount = null)
        {
            var repeatCount = resolvedRepeatCount ?? ResolveEffectRepeatCount(effect, effectContext).RepeatCount;
            if (repeatCount == 0)
            {
                return 1;
            }

            if (effect.EffectConfig.RepeatCondition != null)
            {
                if (!effect.EffectConfig.RepeatCondition.IsEnabled && HasStableDecisionInputForRepeatPreflight(effect.EffectConfig))
                {
                    return repeatCount + 1;
                }

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

        private (bool Success, int RepeatCount, string? ErrorMessage) ResolveEffectRepeatCount(Effect effect, GameContext context)
        {
            var fallback = Math.Max(0, effect.EffectConfig.RepeatCount);
            var repeatCountValue = effect.EffectConfig.RepeatCountValue;
            if (repeatCountValue == null)
            {
                return (true, fallback, null);
            }

            try
            {
                var resolver = context.ServiceProvider.GetRequiredService<IValueSourceResolver>();
                var resolved = resolver.Resolve(repeatCountValue, context);
                if (!TryCoerceNonNegativeRepeatCount(resolved, out var repeatCount))
                {
                    return (false, 0, $"RepeatCountValue for effect {effect.Type} ({effect.Id}) must resolve to a non-negative number.");
                }

                return (true, repeatCount, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve RepeatCountValue for effect {EffectType} ({EffectId}).", effect.Type, effect.Id);
                return (false, 0, $"Failed to resolve RepeatCountValue for effect {effect.Type} ({effect.Id}): {ex.Message}");
            }
        }

        private static bool TryCoerceNonNegativeRepeatCount(object? value, out int repeatCount)
        {
            switch (value)
            {
                case null:
                    repeatCount = 0;
                    return false;
                case int i:
                    repeatCount = Math.Max(0, i);
                    return true;
                case long l when l >= int.MinValue && l <= int.MaxValue:
                    repeatCount = Math.Max(0, (int)l);
                    return true;
                case short s:
                    repeatCount = Math.Max(0, (int)s);
                    return true;
                case byte b:
                    repeatCount = Math.Max(0, (int)b);
                    return true;
                case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                    repeatCount = Math.Max(0, (int)Math.Floor(d));
                    return true;
                case float f when !float.IsNaN(f) && !float.IsInfinity(f):
                    repeatCount = Math.Max(0, (int)Math.Floor(f));
                    return true;
                case decimal m:
                    repeatCount = Math.Max(0, (int)Math.Floor(m));
                    return true;
                case bool b:
                    repeatCount = b ? 1 : 0;
                    return true;
                case string s when int.TryParse(s, out var parsed):
                    repeatCount = Math.Max(0, parsed);
                    return true;
                case JsonElement jsonElement:
                    if (jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out var jsonNumber))
                    {
                        repeatCount = Math.Max(0, jsonNumber);
                        return true;
                    }

                    if (jsonElement.ValueKind == JsonValueKind.String
                        && int.TryParse(jsonElement.GetString(), out var jsonStringInt))
                    {
                        repeatCount = Math.Max(0, jsonStringInt);
                        return true;
                    }

                    break;
            }

            repeatCount = 0;
            return false;
        }

        private async Task<(PausableTransactionResult Result, List<Effect> Effects, List<IGameEvent> GeneratedEvents)> GetTriggerAbilityEffectsAsync(
            GameContext gameContext,
            IGameEvent gameEvent,
            TriggerTiming timing)
        {
            CaptureEventCurrentPlayerSnapshot(gameContext, gameEvent);

            var generatedEffects = new List<Effect>();
            var generatedEvents = new List<IGameEvent>();
            var abilityTriggerContext = ShouldSuppressAuraRefreshForEvent(gameContext, gameEvent)
                ? gameContext with { IsAuraRefresh = true }
                : gameContext;
            var triggeredAbilities = await gameContext.AbilityManager.GetTriggeredAbilitiesAsync(gameEvent, abilityTriggerContext, timing);

            if (timing == TriggerTiming.On
                && _activePreflightSession?.AlternatePlayAbilitiesByEvent.TryGetValue(gameEvent.EventId, out var alternatePlayAbilities) == true
                && alternatePlayAbilities.Count > 0)
            {
                var primarySourceCard = ResolvePrimarySourceCard(gameEvent);
                triggeredAbilities = RemovePrimaryAbilitiesForSource(triggeredAbilities, primarySourceCard);
                triggeredAbilities.AddRange(alternatePlayAbilities);
            }

            triggeredAbilities = FilterTriggeredAbilitiesByTransactionSameNameLimit(
                triggeredAbilities,
                $"trigger-{timing.ToString().ToLowerInvariant()}");

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
                if (!TryAcquireSameNameTriggerSlot(ability, $"trigger-{timing.ToString().ToLowerInvariant()}"))
                {
                    continue;
                }

                var abilityContext = abilityTriggerContext with { SourceCard = ability.SourceCard, TriggeringEvent = gameEvent };

                if (timing == TriggerTiming.On
                    && gameEvent is IPlayerActionEvent
                    && ability is Ability concreteAbility
                    && concreteAbility.TryConsumePreflightPrimaryAbilityResult(
                        gameEvent.EventId,
                        out var preflightEffects,
                        out var preflightGeneratedEvents,
                        out var preflightShouldEmitAbilityTriggeredEvent))
                {
                    if (preflightShouldEmitAbilityTriggeredEvent)
                    {
                        ability.RecordTrigger(abilityContext);
                        if (ShouldEmitAbilityTriggeredEvent(ability, abilityContext))
                        {
                            generatedEvents.Add(new AbilityTriggeredEvent(ability, abilityContext.TriggeringEvent!));
                        }
                    }
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

        private bool ShouldSuppressAuraRefreshForEvent(GameContext gameContext, IGameEvent gameEvent)
        {
            if (gameEvent is BuffAppliedEvent appliedEvent)
            {
                var sourceAbilityId = appliedEvent.AppliedBuff.SourceAbilityId;
                if (sourceAbilityId == Guid.Empty)
                {
                    return false;
                }

                var sourceAbility = gameContext.AbilityManager.GetAbilityByRuntimeId(sourceAbilityId);
                return sourceAbility is Ability appliedAbility && appliedAbility.Config.Auras.Any();
            }
            else if (gameEvent is BuffRemovedEvent removedEvent)
            {
                foreach (var removedBuff in removedEvent.RemovedBuffs)
                {
                    if (removedBuff.SourceAbilityId == Guid.Empty)
                    {
                        continue;
                    }

                    var sourceAbility = gameContext.AbilityManager.GetAbilityByRuntimeId(removedBuff.SourceAbilityId);
                    if (sourceAbility is Ability removedAbility && removedAbility.Config.Auras.Any())
                    {
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        private List<IAbility> FilterTriggeredAbilitiesByTransactionSameNameLimit(
            IEnumerable<IAbility> triggeredAbilities,
            string phaseTag,
            HashSet<SameNameTriggerKey>? sameNameTriggered = null)
        {
            var candidates = triggeredAbilities?.ToList() ?? new List<IAbility>();
            if (candidates.Count == 0)
            {
                return candidates;
            }

            sameNameTriggered ??= _sameNameTriggeredInTransaction;

            var filtered = new List<IAbility>(candidates.Count);
            var seenInBatch = new HashSet<SameNameTriggerKey>();

            foreach (var ability in candidates)
            {
                if (!TryGetSameNameTriggerKey(ability, out var key))
                {
                    filtered.Add(ability);
                    continue;
                }

                if (sameNameTriggered.Contains(key))
                {
                    _logger.LogDebug(
                        "Suppressed same-name trigger candidate in phase {PhaseTag}: owner={OwnerPlayerId}, definition={DefinitionId}, runtime={RuntimeId}",
                        phaseTag,
                        key.OwnerPlayerId,
                        key.AbilityDefinitionId,
                        ability.RuntimeId);
                    continue;
                }

                if (!seenInBatch.Add(key))
                {
                    _logger.LogDebug(
                        "Suppressed duplicate same-name trigger candidate in phase {PhaseTag}: owner={OwnerPlayerId}, definition={DefinitionId}, runtime={RuntimeId}",
                        phaseTag,
                        key.OwnerPlayerId,
                        key.AbilityDefinitionId,
                        ability.RuntimeId);
                    continue;
                }

                filtered.Add(ability);
            }

            return filtered;
        }

        private bool TryAcquireSameNameTriggerSlot(
            IAbility ability,
            string phaseTag,
            HashSet<SameNameTriggerKey>? sameNameTriggered = null)
        {
            if (!TryGetSameNameTriggerKey(ability, out var key))
            {
                return true;
            }

            sameNameTriggered ??= _sameNameTriggeredInTransaction;

            if (sameNameTriggered.Contains(key))
            {
                _logger.LogDebug(
                    "Suppressed same-name trigger execution in phase {PhaseTag}: owner={OwnerPlayerId}, definition={DefinitionId}, runtime={RuntimeId}",
                    phaseTag,
                    key.OwnerPlayerId,
                    key.AbilityDefinitionId,
                    ability.RuntimeId);
                return false;
            }

            sameNameTriggered.Add(key);
            return true;
        }

        private static bool TryGetSameNameTriggerKey(IAbility ability, out SameNameTriggerKey key)
        {
            key = default;
            if (ability?.TriggerConfig?.LimitSameNameOncePerTransaction != true)
            {
                return false;
            }

            var ownerPlayerId = ability.SourceCard?.OwnerPlayerId ?? Guid.Empty;
            if (ownerPlayerId == Guid.Empty)
            {
                return false;
            }

            key = new SameNameTriggerKey(ownerPlayerId, ability.DefinitionId);
            return true;
        }

        private static bool IsInterceptEffect(Effect effect)
        {
            return IsInterceptEffectType(effect.Type);
        }

        private static bool ShouldEmitAbilityTriggeredEvent(IAbility ability, GameContext context)
        {
            if (context.TriggeringEvent == null)
            {
                return false;
            }

            if (ability is Ability concreteAbility)
            {
                return concreteAbility.BroadcastOnTrigger;
            }

            return true;
        }

        private static bool IsInterceptEffectType(EffectType effectType)
        {
            return effectType == EffectType.RewriteEventParams
                || effectType == EffectType.ReplaceEvent
                || effectType == EffectType.PreventEvent;
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

        private async Task<PausableTransactionResult> DrainStagedEventsForBarrierAsync(
            GameContext context,
            List<IGameEvent> processedEvents,
            Dictionary<string, int> eventSignatureCounts,
            Queue<IGameEvent> eventStagingArea,
            Dictionary<Guid, Queue<IGameEvent>> delayedEventsByParent,
            Queue<IGameEvent> queueDrainedDelayedEvents,
            HashSet<Guid> readyQueueDrainedEvents,
            Dictionary<Guid, AttackFlowContext> attackFlowsByEventId,
            int recursionDepth)
        {
            // Barrier events should be evaluated outside the currently executing effect scope;
            // otherwise parent-delay checks may immediately re-queue them.
            var barrierContext = context with { CurrentEffect = null };

            while (eventStagingArea.Count > 0)
            {
                var stagedEvent = eventStagingArea.Dequeue();
                var stagedResult = await ProcessEventRecursivelyAsync(
                    barrierContext,
                    stagedEvent,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,
                    attackFlowsByEventId,
                    recursionDepth + 1);

                if (stagedResult.ShouldStop)
                {
                    return stagedResult;
                }
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
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

        private async Task<(PausableTransactionResult Result, IGameEvent FinalEvent, List<IGameEvent> GeneratedEvents, List<Effect> DeferredEffects, bool IsPrevented)> ApplyBeforeInterceptsAsync(
            GameContext gameContext,
            IGameEvent initialEvent,
            List<IGameEvent> processedEvents,
            Dictionary<string, int>? eventSignatureCounts = null,
            Queue<IGameEvent>? eventStagingArea = null,
            Dictionary<Guid, Queue<IGameEvent>>? delayedEventsByParent = null,
            Queue<IGameEvent>? queueDrainedDelayedEvents = null,
            HashSet<Guid>? readyQueueDrainedEvents = null,
            Dictionary<Guid, AttackFlowContext>? attackFlowsByEventId = null,
            int recursionDepth = 0,
            InterceptExecutionPhase phase = InterceptExecutionPhase.Commit)
        {
            var currentEvent = initialEvent;
            var interceptIteration = 0;
            var interceptedAbilityIds = new HashSet<Guid>();
            var maxIterations = GameConstants.AbilityTrigger.BeforeInterceptMaxIterations;
            var generatedEvents = new List<IGameEvent>();
            var generatedEventIds = new HashSet<Guid>();
            var deferredEffects = new List<Effect>();
            var sameNameTriggered = phase == InterceptExecutionPhase.Preflight
                ? GetPreflightSameNameState()
                : null;
            var shouldRecordRuntimeState = phase == InterceptExecutionPhase.Commit;
            var canExecuteFollowUps =
                eventSignatureCounts != null &&
                eventStagingArea != null &&
                delayedEventsByParent != null &&
                queueDrainedDelayedEvents != null &&
                readyQueueDrainedEvents != null &&
                attackFlowsByEventId != null;

            while (interceptIteration < maxIterations)
            {
                CaptureEventCurrentPlayerSnapshot(gameContext, currentEvent);

                bool eventChanged = false;

                var triggeredAbilities = await gameContext.AbilityManager.GetTriggeredAbilitiesAsync(currentEvent, gameContext, TriggerTiming.Before);
                triggeredAbilities = FilterTriggeredAbilitiesByTransactionSameNameLimit(
                    triggeredAbilities,
                    phase == InterceptExecutionPhase.Preflight ? "before-intercept-preflight" : "before-intercept",
                    sameNameTriggered);

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
                        return (PausableTransactionResult.Failure(abilityResult.ErrorMessage ?? "Before intercept ability processing failed."), currentEvent, generatedEvents, deferredEffects, false);
                    }

                    var interceptEffects = abilityResult.Effects.Where(IsInterceptEffect).ToList();
                    if (interceptEffects.Count == 0)
                    {
                        continue;
                    }

                    if (!TryAcquireSameNameTriggerSlot(
                        ability,
                        phase == InterceptExecutionPhase.Preflight ? "before-intercept-preflight" : "before-intercept",
                        sameNameTriggered))
                    {
                        continue;
                    }

                    interceptedAbilityIds.Add(ability.RuntimeId);
                    deferredEffects.AddRange(abilityResult.Effects.Where(effect => !IsInterceptEffect(effect)));
                    if (shouldRecordRuntimeState)
                    {
                        ability.RecordTrigger(abilityContext);
                        if (ShouldEmitAbilityTriggeredEvent(ability, abilityContext))
                        {
                            generatedEvents.Add(new AbilityTriggeredEvent(ability, abilityContext.TriggeringEvent!));
                        }
                    }

                    AppendGeneratedEvents(generatedEvents, generatedEventIds, abilityResult.GeneratedEvents);

                    foreach (var interceptEffect in interceptEffects)
                    {
                        var (effectResult, actionResult) = await ExecuteInterceptEffectAsync(interceptEffect, abilityContext, processedEvents);
                        if (effectResult.ShouldStop)
                        {
                            return (effectResult, currentEvent, generatedEvents, deferredEffects, false);
                        }

                        if (actionResult != null)
                        {
                            AppendGeneratedEvents(generatedEvents, generatedEventIds, actionResult.GeneratedEvents);

                            if (canExecuteFollowUps)
                            {
                                var effectContext = abilityContext with
                                {
                                    CurrentEffect = interceptEffect,
                                    ResolvedTargets = interceptEffect.ResolvedTargets
                                };

                                var chainedResult = await ProcessChainedAbilitiesAsync(
                                    interceptEffect,
                                    effectContext,
                                    actionResult,
                                    processedEvents,
                                    eventSignatureCounts!,
                                    eventStagingArea!,
                                    delayedEventsByParent!,
                                    queueDrainedDelayedEvents!,
                                    readyQueueDrainedEvents!,
                                    attackFlowsByEventId!,
                                    recursionDepth + 1);
                                if (chainedResult.ShouldStop)
                                {
                                    return (chainedResult, currentEvent, generatedEvents, deferredEffects, false);
                                }
                            }
                        }

                        var intercept = actionResult?.EventIntercept;
                        if (intercept == null)
                        {
                            continue;
                        }

                        if (!TryApplyEventIntercept(gameContext, currentEvent, intercept, out var updatedEvent, out var changed, out var prevented, out var errorMessage))
                        {
                            return (PausableTransactionResult.Failure(errorMessage ?? "Before intercept failed to apply."), currentEvent, generatedEvents, deferredEffects, false);
                        }

                        if (prevented)
                        {
                            ResetResolvedStateAfterInterceptChange(gameContext, currentEvent.EventId);
                            return (PausableTransactionResult.Success(new List<IGameEvent>()), currentEvent, generatedEvents, deferredEffects, true);
                        }

                        if (changed)
                        {
                            ResetResolvedStateAfterInterceptChange(gameContext, currentEvent.EventId);
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

            return (PausableTransactionResult.Success(new List<IGameEvent>()), currentEvent, generatedEvents, deferredEffects, false);
        }

        private static void AppendGeneratedEvents(
            List<IGameEvent> destination,
            HashSet<Guid> eventIds,
            IEnumerable<IGameEvent>? source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var gameEvent in source)
            {
                if (gameEvent == null)
                {
                    continue;
                }

                if (eventIds.Add(gameEvent.EventId))
                {
                    destination.Add(gameEvent);
                }
            }
        }

        private void ResetResolvedStateAfterInterceptChange(GameContext gameContext, Guid triggerEventId)
        {
            if (triggerEventId == Guid.Empty)
            {
                return;
            }

            // Intercept rewrites/replacements can alter event semantics while keeping the same EventId.
            // Reset event-scoped caches so subsequent Before/On evaluation resolves against the updated event.
            if (gameContext.AbilityManager is AbilityManager abilityManager)
            {
                foreach (var ability in abilityManager.GetActiveAbilities().OfType<Ability>())
                {
                    ability.ClearDecisionCache(triggerEventId);

                    foreach (var target in ability.Targets)
                    {
                        if (target.LastResolvedEventId == triggerEventId)
                        {
                            target.MarkForResolution();
                        }
                    }
                }
            }

            var deferredKeys = _deferredTargetResolutionCache.Keys
                .Where(key => key.TriggerEventId == triggerEventId)
                .ToList();
            foreach (var key in deferredKeys)
            {
                _deferredTargetResolutionCache.Remove(key);
            }

            if (_activePreflightSession == null)
            {
                return;
            }

            var decisionKeys = _activePreflightSession.DecisionSnapshots.Keys
                .Where(key => key.TriggerEventId == triggerEventId)
                .ToList();
            foreach (var key in decisionKeys)
            {
                _activePreflightSession.DecisionSnapshots.Remove(key);
            }

            var targetKeys = _activePreflightSession.TargetSignatures.Keys
                .Where(key => key.TriggerEventId == triggerEventId)
                .ToList();
            foreach (var key in targetKeys)
            {
                _activePreflightSession.TargetSignatures.Remove(key);
            }

            var chainedSnapshotKeys = _activePreflightSession.ChainedAbilitySnapshots.Keys
                .Where(key => key.TriggerEventId == triggerEventId)
                .ToList();
            foreach (var key in chainedSnapshotKeys)
            {
                _activePreflightSession.ChainedAbilitySnapshots.Remove(key);
            }
        }

        private async Task<PausableTransactionResult> ResolveDecisionEventIfNeededAsync(
            GameContext gameContext,
            IGameEvent gameEvent,
            DecisionResolutionPhase phase = DecisionResolutionPhase.Commit)
        {
            var resolvedChoose = gameEvent as ChooseEvent;
            var resolvedRandom = gameEvent as RandomEvent;
            var resolvedSequence = gameEvent as SequenceEvent;

            if (resolvedChoose == null && resolvedRandom == null && resolvedSequence == null)
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

            if (resolvedSequence?.SelectedOptionIds.Count > 0)
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

            var decisionMode = GetDecisionModeFromDecisionEvent(gameEvent);
            var executionIndex = Math.Max(0, currentEffect.ExecutionIndex);
            var optionIds = gameEvent switch
            {
                RandomEvent randomEvent => randomEvent.OptionIds,
                ChooseEvent chooseEvent => chooseEvent.OptionIds,
                SequenceEvent sequenceEvent => sequenceEvent.OptionIds,
                _ => Array.Empty<Guid>()
            };

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
                else if (resolvedSequence != null)
                {
                    resolvedSequence.SetSelectedOptionIndexes(Array.Empty<int>());
                }
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            IReadOnlyList<Ability.DecisionOptionSnapshot> availableOptions;
            try
            {
                availableOptions = ability.GetDecisionOptions(currentEffect.TriggeredEventId, currentEffect.EffectIndex, decisionMode, executionIndex);
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

            var usePreflightDecisionCache = decisionMode == Ability.DecisionMode.Choose;
            DecisionInputSignature signature = default;
            DecisionCacheKey cacheKey = default;
            if (usePreflightDecisionCache)
            {
                if (!TryCreateDecisionCacheKey(currentEffect, ability, decisionMode == Ability.DecisionMode.Random, out cacheKey))
                {
                    return PausableTransactionResult.Failure("Decision cache key is invalid.");
                }

                signature = BuildDecisionInputSignature(optionIds, resolvedChoose?.SelectionCount ?? resolvedRandom?.SelectionCount ?? 0, resolvedRandom?.AllowRepeat ?? false, resolvedRandom?.Weights);
                if (phase == DecisionResolutionPhase.Commit)
                {
                    if (!TryGetPreflightDecisionState(cacheKey, out var cachedDecision))
                    {
                        _logger.LogWarning(
                            "Preflight decision snapshot missing for ability {AbilityRuntimeId}, effect {EffectIndex}, execution {ExecutionIndex}. Falling back to commit-time decision resolution.",
                            ability.RuntimeId,
                            currentEffect.EffectIndex,
                            executionIndex);
                    }
                    else
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
                }
            }

            if (resolvedSequence != null)
            {
                var selectionCount = Math.Max(0, resolvedSequence.SelectionCount);
                var sequenceScopePath = EnsureSequenceScopePath(currentEffect, ability);
                var selectedIndexes = ability.ResolveSequenceSelection(
                    sequenceScopePath,
                    optionIds.Count,
                    selectionCount,
                    resolvedSequence.LoopSelection,
                    resolvedSequence.SelectionOrder);
                resolvedSequence.SetSelectedOptionIndexes(selectedIndexes);
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

                if (phase == DecisionResolutionPhase.Preflight && usePreflightDecisionCache)
                {
                    UpsertPreflightDecisionState(
                        cacheKey,
                        signature,
                        selectedIndexes);
                }
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            if (resolvedChoose != null)
            {
                var selectionCount = Math.Max(0, resolvedChoose.SelectionCount);
                selectionCount = Math.Min(selectionCount, optionIds.Count);
                if (selectionCount == 0)
                {
                    resolvedChoose.SetSelectedOptionIndexes(Array.Empty<int>());
                    return PausableTransactionResult.Success(new List<IGameEvent>());
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
                if (phase == DecisionResolutionPhase.Preflight && usePreflightDecisionCache)
                {
                    UpsertPreflightDecisionState(
                        cacheKey,
                        signature,
                        selectedIndexes);
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

        private string EnsureSequenceScopePath(Effect effect, Ability? ability = null)
        {
            if (!string.IsNullOrWhiteSpace(effect.SequenceScopePath))
            {
                return effect.SequenceScopePath;
            }

            var scopePrefix = ability?.SequenceScopePathPrefix;
            if (string.IsNullOrWhiteSpace(scopePrefix) && effect.SourceAbility is Ability sourceAbility)
            {
                scopePrefix = sourceAbility.SequenceScopePathPrefix;
            }

            if (string.IsNullOrWhiteSpace(scopePrefix) && effect.sourceAbilityId.HasValue && effect.sourceAbilityId.Value != Guid.Empty)
            {
                scopePrefix = $"ability/{effect.sourceAbilityId.Value:N}";
            }

            if (string.IsNullOrWhiteSpace(scopePrefix))
            {
                scopePrefix = $"effect/{effect.Id:N}";
            }

            effect.SequenceScopePath = $"{scopePrefix}/effect/{Math.Max(0, effect.EffectIndex)}";
            return effect.SequenceScopePath;
        }

        private static string BuildChildSequenceScopePath(string parentScopePath, string segment, int index)
        {
            var normalizedParent = string.IsNullOrWhiteSpace(parentScopePath)
                ? "effect/unknown"
                : parentScopePath.TrimEnd('/');
            var normalizedSegment = string.IsNullOrWhiteSpace(segment)
                ? "child"
                : segment.Trim();
            return $"{normalizedParent}/{normalizedSegment}/{Math.Max(0, index)}";
        }

        private static void AssignInlineDefinitionId(
            Ability inlineAbility,
            Ability? parentAbility,
            Effect parentEffect,
            string segment,
            int index)
        {
            if (inlineAbility.Config.Id > 0)
            {
                return;
            }

            var parentDefinitionId = parentAbility?.DefinitionId ?? 0;
            var parentRuntimeId = parentAbility?.RuntimeId;
            var effectIndex = Math.Max(0, parentEffect.EffectIndex);
            var normalizedSegment = string.IsNullOrWhiteSpace(segment) ? "inline" : segment.Trim();
            var path = $"effect/{effectIndex}/{normalizedSegment}/{Math.Max(0, index)}";
            var derivedId = AbilityDefinitionIdResolver.CreateInlineDefinitionId(parentDefinitionId, path, parentRuntimeId);
            inlineAbility.AssignDerivedDefinitionId(derivedId);
        }

        private static bool IsDecisionEffectType(EffectType effectType)
        {
            return effectType == EffectType.Choose
                || effectType == EffectType.Random
                || effectType == EffectType.Sequence;
        }

        private static Ability.DecisionMode GetDecisionModeFromEffectType(EffectType effectType)
        {
            return effectType switch
            {
                EffectType.Random => Ability.DecisionMode.Random,
                EffectType.Sequence => Ability.DecisionMode.Sequence,
                _ => Ability.DecisionMode.Choose
            };
        }

        private static Ability.DecisionMode GetDecisionModeFromDecisionEvent(IGameEvent decisionEvent)
        {
            return decisionEvent switch
            {
                RandomEvent => Ability.DecisionMode.Random,
                SequenceEvent => Ability.DecisionMode.Sequence,
                _ => Ability.DecisionMode.Choose
            };
        }

        private static string BuildDecisionChoicePrompt(int selectionCount)
        {
            if (selectionCount <= 1)
            {
                return "选择一个【模式】。";
            }

            return $"选择 {selectionCount} 个【模式】";
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

        private bool ShouldSkipQueuedEffectExecution(
            Effect effect,
            GameContext gameContext,
            CardInstance? sourceCard,
            bool isInterceptEffect)
        {
            if (effect.ForceExecution || !effect.sourceAbilityId.HasValue)
            {
                return false;
            }

            var abilityRuntimeId = effect.sourceAbilityId.Value;
            var activeAbility = gameContext.AbilityManager.GetAbilityByRuntimeId(abilityRuntimeId);
            AbilitySnapshot? snapshot = null;
            var hasSnapshot = activeAbility == null
                && gameContext.AbilityManager.TryGetAbilitySnapshot(abilityRuntimeId, out snapshot);

            if (effect.SourceAbilityStateVersion.HasValue)
            {
                var abilityForVersionCheck = activeAbility as Ability ?? effect.SourceAbility as Ability;
                if (abilityForVersionCheck != null
                    && abilityForVersionCheck.QueuedEffectStateVersion > effect.SourceAbilityStateVersion.Value)
                {
                    _logger.LogDebug(
                        "Skipping {EffectPipeline} effect {EffectType} ({EffectId}) because source ability {AbilityRuntimeId} advanced from state version {CapturedVersion} to {CurrentVersion}. InvalidationReason={InvalidationReason}",
                        isInterceptEffect ? "intercept" : "queued",
                        effect.Type,
                        effect.Id,
                        abilityForVersionCheck.RuntimeId,
                        effect.SourceAbilityStateVersion.Value,
                        abilityForVersionCheck.QueuedEffectStateVersion,
                        abilityForVersionCheck.LastQueuedEffectInvalidationReason);
                    return true;
                }

                if (hasSnapshot
                    && snapshot!.QueuedEffectInvalidationVersion > effect.SourceAbilityStateVersion.Value)
                {
                    _logger.LogDebug(
                        "Skipping {EffectPipeline} effect {EffectType} ({EffectId}) because source ability {AbilityRuntimeId} was unregistered after advancing from state version {CapturedVersion} to {CurrentVersion}. InvalidationReason={InvalidationReason}",
                        isInterceptEffect ? "intercept" : "queued",
                        effect.Type,
                        effect.Id,
                        abilityRuntimeId,
                        effect.SourceAbilityStateVersion.Value,
                        snapshot.QueuedEffectInvalidationVersion,
                        snapshot.QueuedEffectInvalidationReason);
                    return true;
                }
            }

            if (hasSnapshot && snapshot!.ShouldCancelQueuedEffects)
            {
                _logger.LogDebug(
                    "Skipping {EffectPipeline} effect {EffectType} ({EffectId}) because source ability {AbilityRuntimeId} was unregistered with cancellation flag. RemovalReason={RemovalReason}",
                    isInterceptEffect ? "intercept" : "queued",
                    effect.Type,
                    effect.Id,
                    abilityRuntimeId,
                    snapshot.RemovalReason);
                return true;
            }

            var abilityForSuppression = activeAbility ?? effect.SourceAbility;
            if (abilityForSuppression != null && gameContext.AbilityManager.IsAbilitySuppressed(abilityForSuppression, gameContext))
            {
                _logger.LogDebug(
                    "Skipping {EffectPipeline} effect {EffectType} ({EffectId}) because source ability {AbilityRuntimeId} is currently suppressed.",
                    isInterceptEffect ? "intercept" : "queued",
                    effect.Type,
                    effect.Id,
                    abilityForSuppression.RuntimeId);
                return true;
            }

            var abilityForZoneRevalidation = activeAbility ?? effect.SourceAbility;
            if (abilityForZoneRevalidation?.TriggerConfig?.RevalidateZoneBeforeExecution == true)
            {
                var cardToCheck = sourceCard ?? abilityForZoneRevalidation.SourceCard;
                if (cardToCheck != null && !abilityForZoneRevalidation.TriggerConfig.IsValidZone(cardToCheck.CurrentZone))
                {
                    _logger.LogDebug(
                        "Skipping {EffectPipeline} effect {EffectType} ({EffectId}) because source card {CardId} is now in zone {Zone}, which is outside ValidZones.",
                        isInterceptEffect ? "intercept" : "queued",
                        effect.Type,
                        effect.Id,
                        cardToCheck.InstanceId,
                        cardToCheck.CurrentZone);
                    return true;
                }
            }

            return false;
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

            if (ShouldSkipQueuedEffectExecution(effect, gameContext, sourceCard, isInterceptEffect: true))
            {
                return (PausableTransactionResult.Success(new List<IGameEvent>()), null);
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
            if (deferredResolutionResult.ShouldStop)
            {
                return (deferredResolutionResult, null);
            }

            effectContext = effectContext with { ResolvedTargets = effect.ResolvedTargets };

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
                        var errorMessage = $"Intercept effect execution failed for {effect.Type}.";
                        _logger.LogError(errorMessage);
                        return (PausableTransactionResult.Failure(errorMessage), null);
                    default:
                        _logger.LogWarning(
                            "Intercept effect {EffectType} failed with unknown OnFailure value '{OnFailure}'. Falling back to CompleteWithFailure.",
                            effect.Type,
                            effect.EffectConfig.OnFailure);
                        return (PausableTransactionResult.Success(new List<IGameEvent>()), actionResult);
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
            out bool prevented,
            out string? errorMessage)
        {
            updatedEvent = currentEvent;
            changed = false;
            prevented = false;
            errorMessage = null;

            switch (intercept.Kind)
            {
                case EventInterceptKind.RewriteParameters:
                    return TryApplyEventRewrite(currentEvent, intercept, out updatedEvent, out changed, out errorMessage);
                case EventInterceptKind.ReplaceEvent:
                    return TryApplyEventReplacement(gameContext, currentEvent, intercept, out updatedEvent, out changed, out errorMessage);
                case EventInterceptKind.PreventEvent:
                    prevented = true;
                    return true;
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
            if (_activePreflightSession != null)
            {
                _activePreflightSession.CommitStarted = true;
            }
            _effectQueue.Clear();
            var eventStagingArea = new Queue<IGameEvent>(initialEvents);
            var delayedEventsByParent = new Dictionary<Guid, Queue<IGameEvent>>();
            var queueDrainedDelayedEvents = new Queue<IGameEvent>();
            var readyQueueDrainedEvents = new HashSet<Guid>();
            var attackFlowsByEventId = new Dictionary<Guid, AttackFlowContext>();
            var processedEvents = new List<IGameEvent>();
            var eventSignatureCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var bundleId = Guid.NewGuid();
            var cardUpdateEventAdded = false;

            async Task<(List<(IGameEvent Event, string? Announcement, IReadOnlyList<GameEventDto> Dtos)> BroadcastPlan, List<CommittedEventWriteDto> RecordedEvents)> BuildBroadcastArtifactsAsync(
                IReadOnlyList<IGameEvent> eventsToBroadcast)
            {
                var broadcastPlan = new List<(IGameEvent Event, string? Announcement, IReadOnlyList<GameEventDto> Dtos)>(eventsToBroadcast.Count);
                var recordedEvents = new List<CommittedEventWriteDto>(eventsToBroadcast.Count);

                foreach (var gameEvent in eventsToBroadcast)
                {
                    var announcement = (gameEvent as ILogAnnounceableEvent)?.GetAnnouncement(gameContext.CurrentGameState);
                    if (!string.IsNullOrWhiteSpace(announcement))
                    {
                        announcement = AnnouncementTokens.WithTurn(gameContext.CurrentGameState, announcement);
                    }
                    List<GameEventDto> dtos;
                    try
                    {
                        var processorResult = await _eventProcessor.ProcessEventAsync(gameEvent, gameContext);
                        dtos = processorResult.ResultingDtos.ToList();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to map processed event {EventType} ({EventId}) to client DTOs during transaction finalization.",
                            gameEvent.EventType,
                            gameEvent.EventId);

                        var sourcePlayerId = gameEvent.Source?.Player?.PlayerId ?? Guid.Empty;
                        var fallbackMessage = !string.IsNullOrWhiteSpace(announcement)
                            ? announcement
                            : $"event-mapping-failed: {gameEvent.EventType}";
                        dtos = new List<GameEventDto>
                        {
                            new LogEventDto(gameEvent.EventId, gameEvent.EventType.ToString(), fallbackMessage, sourcePlayerId)
                        };
                    }

                    if (dtos.Count == 0 && !string.IsNullOrWhiteSpace(announcement))
                    {
                        var sourcePlayerId = gameEvent.Source?.Player?.PlayerId ?? Guid.Empty;
                        dtos.Add(new LogEventDto(gameEvent.EventId, gameEvent.EventType.ToString(), announcement, sourcePlayerId));
                    }

                    var envelopes = _replayOptions.PersistClientDtos
                        ? _recordedEventMapper.MapClientDtos(dtos)
                        : Array.Empty<RecordedClientDtoEnvelopeDto>();
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

                return (broadcastPlan, recordedEvents);
            }

            async Task BroadcastPlanAsync(
                string groupName,
                IReadOnlyList<(IGameEvent Event, string? Announcement, IReadOnlyList<GameEventDto> Dtos)> broadcastPlan)
            {
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

                    if (!hasDto && !string.IsNullOrWhiteSpace(announcement))
                    {
                        var sourcePlayerId = item.Event.Source?.Player?.PlayerId ?? Guid.Empty;
                        var logDto = new LogEventDto(item.Event.EventId, item.Event.EventType.ToString(), announcement, sourcePlayerId);
                        await _clientMethodMapper.BroadcastEventToGroupAllAsync(groupName, logDto);
                    }
                }
            }

            async Task<PausableTransactionResult> FinalizeFailedTransactionAsync(PausableTransactionResult failureResult)
            {
                var failureMessage = failureResult.ErrorMessage ?? "Transaction commit failed.";

                if (!cardUpdateEventAdded)
                {
                    var updatedCards = CollectUpdatedCardInstances(gameContext, processedEvents);
                    if (updatedCards.Count > 0)
                    {
                        var cardUpdateEvent = new CardInstanceUpdatedEvent(updatedCards);
                        processedEvents.Add(cardUpdateEvent);
                        await RecordEventHistoryAsync(gameContext, cardUpdateEvent);
                    }

                    cardUpdateEventAdded = true;
                }

                gameContext.BuffService.RefreshDerivedStateCache(gameContext, "transaction_failed_partial");

                var groupName = SignalRGroupNames.Game(ResolveRoomScopedId(gameContext.CurrentGameState.GameId));

                if (processedEvents.Count > 0)
                {
                    var (broadcastPlan, _) = await BuildBroadcastArtifactsAsync(processedEvents);
                    _logger.LogWarning(
                        "Transaction failed after partial commit. Broadcasting {Count} processed events. Error: {Error}",
                        processedEvents.Count,
                        failureMessage);
                    await BroadcastPlanAsync(groupName, broadcastPlan);
                }

                var failureLog = new LogEventDto(
                    Guid.NewGuid(),
                    "TransactionFailed",
                    $"transaction-failed: {failureMessage}",
                    Guid.Empty);
                await _clientMethodMapper.BroadcastEventToGroupAllAsync(groupName, failureLog);

                return failureResult;
            }

            try
            {
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
                            attackFlowsByEventId,
                            recursionDepth: 0);
                        if (result.ShouldStop) return await FinalizeFailedTransactionAsync(result);
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
                            attackFlowsByEventId,
                            recursionDepth: 0);
                        if (result.ShouldStop) return await FinalizeFailedTransactionAsync(result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in CommitTransactionAsync. Falling back to partial-event broadcast.");
                return await FinalizeFailedTransactionAsync(
                    PausableTransactionResult.Failure($"Transaction commit crashed: {ex}"));
            }

            if (!cardUpdateEventAdded)
            {
                var updatedCards = CollectUpdatedCardInstances(gameContext, processedEvents);
                if (updatedCards.Count > 0)
                {
                    var cardUpdateEvent = new CardInstanceUpdatedEvent(updatedCards);
                    processedEvents.Add(cardUpdateEvent);
                    await RecordEventHistoryAsync(gameContext, cardUpdateEvent);
                }

                cardUpdateEventAdded = true;
            }

            // Ensure all CardInstanceDto mappings in this transaction observe the latest derived state
            // (attack/health/cost/keywords) before materializing broadcast DTOs.
            gameContext.BuffService.RefreshDerivedStateCache(gameContext, "transaction_end");

            var (broadcastPlan, recordedEvents) = await BuildBroadcastArtifactsAsync(processedEvents);

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
                _logger.LogDebug("Skipping replay persistence for game {GameId} (disabled by config or AI match).", gameContext.CurrentGameState.GameId);
            }

            // --- Broadcast all processed events AFTER the entire transaction is complete ---
            // Apply meta-progression updates (economy/profile/quests) after state commit, before broadcasting.
            await _metaProgressionService.ApplyPostCommitAsync(gameContext, processedEvents, gameContext.CancellationToken);
            await _winrateStatsService.ApplyPostCommitAsync(gameContext, processedEvents, gameContext.CancellationToken);

            _logger.LogInformation("Broadcasting {Count} processed events at the end of the transaction.", processedEvents.Count);
            string groupName = SignalRGroupNames.Game(ResolveRoomScopedId(gameContext.CurrentGameState.GameId));
            await BroadcastPlanAsync(groupName, broadcastPlan);

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
                    foreach (var removedBuff in buffRemoved.RemovedBuffs)
                    {
                        var targetId = removedBuff.TargetCardInstanceId;
                        if (targetId != Guid.Empty)
                        {
                            updatedIds.Add(targetId);
                        }
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

        private bool ShouldPersistReplay(GameState gameState)
        {
            if (!_replayOptions.EnableJournalWrites)
            {
                return false;
            }

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
            Dictionary<Guid, AttackFlowContext> attackFlowsByEventId,
            int recursionDepth)
        {
            if (gameContext.CurrentGameState.IsGameOver && gameEvent is GameEndEvent)
            {
                _logger.LogDebug(
                    "Skipping GameEndEvent ({EventId}) because game {GameId} is already over.",
                    gameEvent.EventId,
                    gameContext.CurrentGameState.GameId);
                return PausableTransactionResult.Success(new List<IGameEvent>());
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

            CaptureEventCurrentPlayerSnapshot(gameContext, gameEvent);

            var resolvedAttackFlow = ResolveAttackFlowContext(gameContext.AttackFlow, gameEvent, attackFlowsByEventId);
            if (!ReferenceEquals(resolvedAttackFlow, gameContext.AttackFlow))
            {
                gameContext = gameContext with { AttackFlow = resolvedAttackFlow };
            }

            if (gameContext.AttackFlow != null)
            {
                attackFlowsByEventId[gameEvent.EventId] = gameContext.AttackFlow;
            }

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
                        if (IsDelayParentPending(gameContext, delayConfig.ParentEventId))
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
                var cleanupEvent = new TurnBoundaryCleanupEvent(
                    turnStartEvent.StartingPlayer,
                    Domain.GameFlow.TurnBoundaryCleanupTiming.TurnStart,
                    DelayConfig.Disabled);
                var cleanupResult = await ProcessEventRecursivelyAsync(
                    gameContext,
                    cleanupEvent,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,
                    attackFlowsByEventId,
                    recursionDepth + 1);
                if (cleanupResult.ShouldStop) return cleanupResult;
            }

            // 1a. Before phase (intercept sub-phase): allow rewrites/replacements before commit.
            var (interceptResult, interceptedEvent, interceptGeneratedEvents, deferredBeforeEffects, eventPrevented) = await ApplyBeforeInterceptsAsync(
                gameContext,
                gameEvent,
                processedEvents,
                eventSignatureCounts,
                eventStagingArea,
                delayedEventsByParent,
                queueDrainedDelayedEvents,
                readyQueueDrainedEvents,

                attackFlowsByEventId,

                recursionDepth);
            if (interceptResult.ShouldStop) return interceptResult;
            gameEvent = interceptedEvent;

            if (eventPrevented)
            {
                var deferredInterceptAbilityTriggeredEvents = new List<IGameEvent>();
                foreach (var abilityEvent in interceptGeneratedEvents)
                {
                    if (abilityEvent is AbilityTriggeredEvent)
                    {
                        deferredInterceptAbilityTriggeredEvents.Add(abilityEvent);
                        continue;
                    }

                    var eventResult = await ProcessEventRecursivelyAsync(
                        gameContext,
                        abilityEvent,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,

                        attackFlowsByEventId,

                        recursionDepth + 1);
                    if (eventResult.ShouldStop) return eventResult;
                }

                foreach (var abilityEvent in deferredInterceptAbilityTriggeredEvents)
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

                        attackFlowsByEventId,

                        recursionDepth + 1);
                    if (eventResult.ShouldStop) return eventResult;
                }

                ResetAbilityEventFlags(gameContext);
                ReleaseDelayedEventsIfReady(gameEvent.EventId, eventStagingArea, delayedEventsByParent);
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            var decisionResolveResult = await ResolveDecisionEventIfNeededAsync(gameContext, gameEvent, DecisionResolutionPhase.Commit);
            if (decisionResolveResult.ShouldStop) return decisionResolveResult;

            processedEvents.Add(gameEvent);

            // 1b. Before phase: Effects are executed immediately and their events are processed recursively.
            var (beforeResult, beforeEffects, beforeEvents) = await GetTriggerAbilityEffectsAsync(gameContext, gameEvent, TriggerTiming.Before);
            if (beforeResult.ShouldStop) return beforeResult;
            var beforePhaseEvents = new List<IGameEvent>(interceptGeneratedEvents.Count + beforeEvents.Count);
            if (interceptGeneratedEvents.Count > 0)
            {
                beforePhaseEvents.AddRange(interceptGeneratedEvents);
            }
            if (beforeEvents.Count > 0)
            {
                beforePhaseEvents.AddRange(beforeEvents);
            }

            var deferredBeforeAbilityTriggeredEvents = new List<IGameEvent>();
            foreach (var abilityEvent in beforePhaseEvents)
            {
                if (abilityEvent is AbilityTriggeredEvent)
                {
                    deferredBeforeAbilityTriggeredEvents.Add(abilityEvent);
                    continue;
                }

                var eventResult = await ProcessEventRecursivelyAsync(
                    gameContext,
                    abilityEvent,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,

                    attackFlowsByEventId,

                    recursionDepth + 1);
                if (eventResult.ShouldStop) return eventResult;
            }
            var immediateBeforeEffects = new List<Effect>(deferredBeforeEffects.Count + beforeEffects.Count);
            if (deferredBeforeEffects.Count > 0)
            {
                immediateBeforeEffects.AddRange(deferredBeforeEffects);
            }
            if (beforeEffects.Count > 0)
            {
                immediateBeforeEffects.AddRange(beforeEffects.Where(effect => !IsInterceptEffect(effect)));
            }

            foreach (var effect in immediateBeforeEffects)
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

                    attackFlowsByEventId,

                    recursionDepth + 1);
                if (result.ShouldStop) return result;
            }

            foreach (var abilityEvent in deferredBeforeAbilityTriggeredEvents)
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

                    attackFlowsByEventId,

                    recursionDepth + 1);
                if (eventResult.ShouldStop) return eventResult;
            }

            // 2. 'On' phase: Effects are enqueued for later processing.
            var (onResult, onEffects, onEvents) = await GetTriggerAbilityEffectsAsync(gameContext, gameEvent, TriggerTiming.On);
            if (onResult.ShouldStop) return onResult;
            var deferredOnAbilityTriggeredEvents = new List<IGameEvent>();
            foreach (var abilityEvent in onEvents)
            {
                if (abilityEvent is AbilityTriggeredEvent)
                {
                    deferredOnAbilityTriggeredEvents.Add(abilityEvent);
                    continue;
                }

                var eventResult = await ProcessEventRecursivelyAsync(
                    gameContext,
                    abilityEvent,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,

                    attackFlowsByEventId,

                    recursionDepth + 1);
                if (eventResult.ShouldStop) return eventResult;
            }
            foreach (var effect in onEffects)
            {
                _effectQueue.Enqueue(effect);
            }

            foreach (var abilityEvent in deferredOnAbilityTriggeredEvents)
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

                    attackFlowsByEventId,

                    recursionDepth + 1);
                if (eventResult.ShouldStop) return eventResult;
            }

            // 3. 'Commit' phase: The event's state change is applied, and resulting events are processed recursively.
            var historyRecorded = false;
            if (gameEvent is ICommittableEvent committableEvent)
            {
                _logger.LogInformation("Committing state changes for {EventType}.", gameEvent.GetType().Name);
                DomainOperationResult commitResult;
                try
                {
                    commitResult = committableEvent.Commit(gameContext);
                }
                catch (Exception ex)
                {
                    processedEvents.RemoveAll(e => e.EventId == gameEvent.EventId);
                    var message = $"Commit threw for {gameEvent.GetType().Name}: {ex.Message}";
                    _logger.LogError(ex, message);
                    return PausableTransactionResult.Failure(message);
                }
                if (!commitResult.Success)
                {
                    processedEvents.RemoveAll(e => e.EventId == gameEvent.EventId);
                    return PausableTransactionResult.Failure(commitResult.ErrorMessage ?? $"Commit failed for {gameEvent.GetType().Name}.");
                }
                gameContext.CurrentGameState.IncrementStateRevision();
                await RecordEventHistoryAsync(gameContext, gameEvent);
                historyRecorded = true;
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

                        attackFlowsByEventId,

                        recursionDepth + 1);
                    if (recursiveResult.ShouldStop) return recursiveResult;
                }

                if (gameEvent is TurnBoundaryCleanupEvent cleanupEvent)
                {
                    gameContext.BuffService.RefreshDerivedStateCache(
                        gameContext,
                        cleanupEvent.Timing == Domain.GameFlow.TurnBoundaryCleanupTiming.TurnStart
                            ? "turn_start_cleanup"
                            : "turn_end_cleanup");
                }
            }

            if (!historyRecorded)
            {
                await RecordEventHistoryAsync(gameContext, gameEvent);
            }

            // 4. 'After' phase: Effects are enqueued for later processing.
            var (afterResult, afterEffects, afterEvents) = await GetTriggerAbilityEffectsAsync(gameContext, gameEvent, TriggerTiming.After);
            if (afterResult.ShouldStop) return afterResult;
            var deferredAfterAbilityTriggeredEvents = new List<IGameEvent>();
            foreach (var abilityEvent in afterEvents)
            {
                if (abilityEvent is AbilityTriggeredEvent)
                {
                    deferredAfterAbilityTriggeredEvents.Add(abilityEvent);
                    continue;
                }

                var eventResult = await ProcessEventRecursivelyAsync(
                    gameContext,
                    abilityEvent,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,

                    attackFlowsByEventId,

                    recursionDepth + 1);
                if (eventResult.ShouldStop) return eventResult;
            }
            foreach (var effect in afterEffects)
            {
                _effectQueue.Enqueue(effect);
            }

            foreach (var abilityEvent in deferredAfterAbilityTriggeredEvents)
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

                    attackFlowsByEventId,

                    recursionDepth + 1);
                if (eventResult.ShouldStop) return eventResult;
            }

            ResetAbilityEventFlags(gameContext);
            ReleaseDelayedEventsIfReady(gameEvent.EventId, eventStagingArea, delayedEventsByParent);
            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private static void CaptureEventCurrentPlayerSnapshot(GameContext gameContext, IGameEvent? gameEvent)
        {
            if (gameEvent is GameEventBase eventBase)
            {
                eventBase.CaptureCurrentPlayerSnapshot(gameContext);
            }
        }

        private async Task RecordEventHistoryAsync(GameContext gameContext, IGameEvent gameEvent)
        {
            if (gameContext.CurrentGameState == null)
            {
                _logger.LogWarning(
                    "[GameActionTransactionService] Skipped history write for event {EventType} (ID: {EventId}): missing game state.",
                    gameEvent.EventType,
                    gameEvent.EventId);
                return;
            }

            var historyService = gameContext.ServiceProvider.GetService<IEventHistoryService>();
            if (historyService == null)
            {
                _logger.LogWarning(
                    "[GameActionTransactionService] Skipped history write for event {EventType} (ID: {EventId}): IEventHistoryService unavailable.",
                    gameEvent.EventType,
                    gameEvent.EventId);
                return;
            }

            try
            {
                await historyService.StoreEventAsync(gameContext.CurrentGameState.GameId, gameEvent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[GameActionTransactionService] Failed to persist event history for {EventType} (ID: {EventId}) in game {GameId}.",
                    gameEvent.EventType,
                    gameEvent.EventId,
                    gameContext.CurrentGameState.GameId);
            }
        }

        private static void ResetAbilityEventFlags(GameContext gameContext)
        {
            if (gameContext.AbilityManager is AbilityManager abilityManager)
            {
                abilityManager.ResetEventFlagsForAllAbilities();
            }
        }

        private bool IsDelayParentPending(GameContext gameContext, Guid parentEventId)
        {
            if (parentEventId == Guid.Empty)
            {
                return false;
            }

            if (_activeEffectExecutionIds.Contains(parentEventId))
            {
                return true;
            }

            if (gameContext.CurrentEffect?.Id == parentEventId)
            {
                return true;
            }

            if (gameContext.CurrentEffect?.TriggeredEventId == parentEventId)
            {
                return true;
            }

            return _effectQueue.Any(effect => effect.Id == parentEventId || effect.TriggeredEventId == parentEventId);
        }

        private IDisposable BeginEffectExecutionScope(Guid effectId)
        {
            _activeEffectExecutionIds.Add(effectId);
            return new EffectExecutionScope(_activeEffectExecutionIds, effectId);
        }

        private sealed class EffectExecutionScope : IDisposable
        {
            private readonly HashSet<Guid> _activeIds;
            private readonly Guid _effectId;
            private bool _disposed;

            public EffectExecutionScope(HashSet<Guid> activeIds, Guid effectId)
            {
                _activeIds = activeIds;
                _effectId = effectId;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _activeIds.Remove(_effectId);
                _disposed = true;
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

        private static AttackFlowContext? ResolveAttackFlowContext(
            AttackFlowContext? currentFlow,
            IGameEvent gameEvent,
            IReadOnlyDictionary<Guid, AttackFlowContext> attackFlowsByEventId)
        {
            if (TryResolveAttackPair(gameEvent, out var attackerId, out var defenderId))
            {
                if (currentFlow != null && currentFlow.Matches(attackerId, defenderId))
                {
                    return currentFlow;
                }

                if (attackFlowsByEventId.TryGetValue(gameEvent.EventId, out var mappedFlow)
                    && mappedFlow.Matches(attackerId, defenderId))
                {
                    return mappedFlow;
                }

                return new AttackFlowContext(gameEvent.EventId, attackerId, defenderId);
            }

            if (currentFlow != null)
            {
                return currentFlow;
            }

            if (attackFlowsByEventId.TryGetValue(gameEvent.EventId, out var existingFlow))
            {
                return existingFlow;
            }

            return null;
        }

        private static bool TryResolveAttackPair(IGameEvent gameEvent, out Guid attackerId, out Guid defenderId)
        {
            attackerId = Guid.Empty;
            defenderId = Guid.Empty;

            switch (gameEvent)
            {
                case AttackRequestedEvent attackRequested:
                    attackerId = attackRequested.ActionDto.AttackerUid;
                    defenderId = attackRequested.ActionDto.TargetUid;
                    break;
                case AttackEvent attackEvent when attackEvent.Attacker != null && attackEvent.TargetCard != null:
                    attackerId = attackEvent.Attacker.InstanceId;
                    defenderId = attackEvent.TargetCard.InstanceId;
                    break;
                case ClashEvent clashEvent when clashEvent.Attacker != null && clashEvent.Defender != null:
                    attackerId = clashEvent.Attacker.InstanceId;
                    defenderId = clashEvent.Defender.InstanceId;
                    break;
                case CombatResolutionEvent combatResolutionEvent when combatResolutionEvent.Attacker != null && combatResolutionEvent.Defender != null:
                    attackerId = combatResolutionEvent.Attacker.InstanceId;
                    defenderId = combatResolutionEvent.Defender.InstanceId;
                    break;
                case CombatDestroyResolutionEvent destroyResolutionEvent when destroyResolutionEvent.Attacker != null && destroyResolutionEvent.Defender != null:
                    attackerId = destroyResolutionEvent.Attacker.InstanceId;
                    defenderId = destroyResolutionEvent.Defender.InstanceId;
                    break;
            }

            return attackerId != Guid.Empty && defenderId != Guid.Empty;
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
            Dictionary<Guid, AttackFlowContext> attackFlowsByEventId,
            int recursionDepth)
        {
            var sourceCard = executedEffect.SourceAbility?.SourceCard;
            if (sourceCard == null && executedEffect.SourceCardId.HasValue)
            {
                sourceCard = context.CurrentGameState.FindCardInstance(executedEffect.SourceCardId.Value);
            }

            if (ShouldSkipQueuedEffectExecution(executedEffect, context, sourceCard, isInterceptEffect: false))
            {
                _logger.LogDebug(
                    "Skipping chained abilities for effect {EffectType} ({EffectId}) because the source ability was invalidated.",
                    executedEffect.Type,
                    executedEffect.Id);
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            IReadOnlyList<IAbility> chainedAbilities;
            var usingPreflightSnapshot = false;
            if (TryCreateChainedAbilitySnapshotKey(executedEffect, out var snapshotKey)
                && TryTakePreflightChainedAbilitySnapshot(snapshotKey, out var snapshotAbilities))
            {
                chainedAbilities = snapshotAbilities;
                usingPreflightSnapshot = true;
            }
            else
            {
                var chainedAbilityResult = CreateChainedAbilitiesForExecution(executedEffect, context);
                if (!chainedAbilityResult.Success)
                {
                    return PausableTransactionResult.Failure(chainedAbilityResult.ErrorMessage ?? "Failed to create chained abilities from templates.");
                }

                chainedAbilities = chainedAbilityResult.Abilities;
            }

            if (chainedAbilities.Count > 0)
            {
                _logger.LogInformation("Effect {EffectType} has {Count} chained abilities. Processing them...", executedEffect.Type, chainedAbilities.Count);

                // Find the EffectCompletedEvent emitted by the current effect itself.
                // GeneratedEvents may already include nested EffectCompletedEvent entries from decision/chained execution.
                // The current effect's own EffectCompletedEvent is appended last by ProcessEffectAsync.
                var effectCompletedEvent = effectResult.GeneratedEvents.OfType<EffectCompletedEvent>().LastOrDefault();
                if (effectCompletedEvent == null)
                {
                    _logger.LogWarning("Could not find EffectCompletedEvent for chained ability processing. Context may be incomplete for ability on card {SourceCardId}.", executedEffect.SourceCardId);
                }

                foreach (var chainedAbility in chainedAbilities)
                {
                    // Settlement barrier: make sure previously released delayed events are committed
                    // before resolving the next chained ability.
                    var preBarrierResult = await DrainStagedEventsForBarrierAsync(
                        context,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,
                        attackFlowsByEventId,
                        recursionDepth);
                    if (preBarrierResult.ShouldStop)
                    {
                        return preBarrierResult;
                    }

                    if (ShouldSkipQueuedEffectExecution(executedEffect, context, sourceCard, isInterceptEffect: false))
                    {
                        _logger.LogDebug(
                            "Stopping chained abilities for effect {EffectType} ({EffectId}) because the source ability was invalidated during execution.",
                            executedEffect.Type,
                            executedEffect.Id);
                        return PausableTransactionResult.Success(new List<IGameEvent>());
                    }

                    // Create the precise context for this chained ability.
                    if (usingPreflightSnapshot && effectCompletedEvent != null)
                    {
                        RebindResolvedTargetsForCommit(chainedAbility, effectCompletedEvent.EventId);
                    }

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
                    var generatedEventResult = await ProcessAbilityGeneratedEventsAsync(
                        abilityContext,
                        result.GeneratedEvents,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,

                        attackFlowsByEventId,

                        recursionDepth + 1);
                    if (generatedEventResult.ShouldStop)
                    {
                        return generatedEventResult;
                    }

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

                            attackFlowsByEventId,

                            recursionDepth + 1);
                        if (subEffectResult.ShouldStop)
                        {
                            return subEffectResult;
                        }
                    }

                    // Settlement barrier: process newly released delayed events (including destroy)
                    // before moving to the next chained ability.
                    var postBarrierResult = await DrainStagedEventsForBarrierAsync(
                        context,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,
                        attackFlowsByEventId,
                        recursionDepth);
                    if (postBarrierResult.ShouldStop)
                    {
                        return postBarrierResult;
                    }
                }
            }
            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private static void RebindResolvedTargetsForCommit(IAbility ability, Guid triggeringEventId)
        {
            if (ability == null || triggeringEventId == Guid.Empty)
            {
                return;
            }

            foreach (var target in ability.Targets)
            {
                if (target == null || target.NeedsResolution)
                {
                    continue;
                }

                target.SetResolvedTargets(target.ResolvedTargets.ToList(), triggeringEventId);
            }
        }

        private (bool Success, string? ErrorMessage, IReadOnlyList<IAbility> Abilities) CreateChainedAbilitiesForExecution(Effect executedEffect, GameContext context)
        {
            if (!executedEffect.EffectConfig.ChainedAbilities.Any())
            {
                return (true, null, Array.Empty<IAbility>());
            }

            var abilityFactory = context.ServiceProvider.GetService<IAbilityFactory>();
            if (abilityFactory == null)
            {
                return (false, "Effect requires IAbilityFactory to instantiate chained ability templates.", Array.Empty<IAbility>());
            }

            var sourceCard = executedEffect.SourceAbility?.SourceCard;
            if (sourceCard == null && executedEffect.SourceCardId.HasValue)
            {
                sourceCard = context.CurrentGameState.FindCardInstance(executedEffect.SourceCardId.Value);
            }

            if (sourceCard == null)
            {
                return (false, $"Cannot instantiate chained abilities for effect {executedEffect.Type}: source card is missing.", Array.Empty<IAbility>());
            }

            try
            {
                var sourceBuffId = executedEffect.SourceAbility?.SourceBuffId;
                var abilities = abilityFactory
                    .CreateAbilities(sourceCard, executedEffect.EffectConfig.ChainedAbilities, sourceBuffId)
                    .Cast<IAbility>()
                    .ToList();

                var summaryRootAbilityId = executedEffect.SummaryRootAbilityId;
                if (!summaryRootAbilityId.HasValue || summaryRootAbilityId.Value == Guid.Empty)
                {
                    if (executedEffect.SourceAbility is Ability sourceAbility)
                    {
                        summaryRootAbilityId = sourceAbility.SummaryRootAbilityId;
                    }
                    else if (executedEffect.sourceAbilityId.HasValue && executedEffect.sourceAbilityId.Value != Guid.Empty)
                    {
                        summaryRootAbilityId = executedEffect.sourceAbilityId.Value;
                    }
                }

                if (summaryRootAbilityId.HasValue && summaryRootAbilityId.Value != Guid.Empty)
                {
                    foreach (var chainedAbility in abilities.OfType<Ability>())
                    {
                        chainedAbility.SummaryRootAbilityId = summaryRootAbilityId.Value;
                    }
                }

                var parentScopePath = EnsureSequenceScopePath(executedEffect, executedEffect.SourceAbility as Ability);
                for (var abilityIndex = 0; abilityIndex < abilities.Count; abilityIndex++)
                {
                    if (abilities[abilityIndex] is Ability chainedConcreteAbility)
                    {
                        chainedConcreteAbility.SequenceScopePathPrefix = BuildChildSequenceScopePath(parentScopePath, "chain", abilityIndex);
                        AssignInlineDefinitionId(chainedConcreteAbility, executedEffect.SourceAbility as Ability, executedEffect, "chain", abilityIndex);
                    }
                }

                return (true, null, abilities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to instantiate chained ability templates for effect {EffectType} ({EffectId}).", executedEffect.Type, executedEffect.Id);
                return (false, $"Failed to instantiate chained abilities for effect {executedEffect.Type}.", Array.Empty<IAbility>());
            }
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
            Dictionary<Guid, AttackFlowContext> attackFlowsByEventId,
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
                // Settlement barrier: make sure previously released delayed events are committed
                // before resolving the next decision branch.
                var preBarrierResult = await DrainStagedEventsForBarrierAsync(
                    context,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,
                    attackFlowsByEventId,
                    recursionDepth);
                if (preBarrierResult.ShouldStop)
                {
                    return preBarrierResult;
                }

                var sourceCard = effect.SourceAbility?.SourceCard;
                if (sourceCard == null && effect.SourceCardId.HasValue)
                {
                    sourceCard = context.CurrentGameState.FindCardInstance(effect.SourceCardId.Value);
                }

                if (ShouldSkipQueuedEffectExecution(effect, context, sourceCard, isInterceptEffect: false))
                {
                    _logger.LogDebug(
                        "Skipping remaining decision branch abilities for effect {EffectType} ({EffectId}) because the source ability was invalidated.",
                        effect.Type,
                        effect.Id);
                    return PausableTransactionResult.Success(new List<IGameEvent>());
                }

                var branchProcessedStart = processedEvents.Count;
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

                var generatedEventResult = await ProcessAbilityGeneratedEventsAsync(
                    abilityContext,
                    result.GeneratedEvents,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,
                    attackFlowsByEventId,
                    recursionDepth + 1,
                    decisionEvents,
                    capturedEventIds);
                if (generatedEventResult.ShouldStop)
                {
                    return generatedEventResult;
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

                        attackFlowsByEventId,

                        recursionDepth + 1);
                    if (subEffectResult.ShouldStop)
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

                // Settlement barrier: process newly released delayed events (including destroy)
                // before moving to the next decision branch.
                var postBarrierResult = await DrainStagedEventsForBarrierAsync(
                    context,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,
                    attackFlowsByEventId,
                    recursionDepth);
                if (postBarrierResult.ShouldStop)
                {
                    return postBarrierResult;
                }

                for (var i = branchProcessedStart; i < processedEvents.Count; i++)
                {
                    var newEvent = processedEvents[i];
                    if (capturedEventIds.Add(newEvent.EventId))
                    {
                        decisionEvents.Add(newEvent);
                    }
                }
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private async Task<PausableTransactionResult> ExecuteTriggeredAbilitiesAsync(
            Effect effect,
            GameContext context,
            TriggerAbilityEffectParams parameters,
            EffectCompletedEvent triggeringEvent,
            List<IGameEvent> processedEvents,
            Dictionary<string, int> eventSignatureCounts,
            Queue<IGameEvent> eventStagingArea,
            Dictionary<Guid, Queue<IGameEvent>> delayedEventsByParent,
            Queue<IGameEvent> queueDrainedDelayedEvents,
            HashSet<Guid> readyQueueDrainedEvents,
            Dictionary<Guid, AttackFlowContext> attackFlowsByEventId,
            int recursionDepth)
        {
            if (!effect.SelectedTargets.Any())
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            var triggeredAbilities = ResolveTriggeredAbilities(effect, context, parameters);
            if (triggeredAbilities.Count == 0)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            var invocationOptions = parameters.AutoRandomTargetSelection
                ? new AbilityInvocationOptions
                {
                    AutoRandomTargetSelection = parameters.AutoRandomTargetSelection
                }
                : AbilityInvocationOptions.None;

            foreach (var triggeredAbility in triggeredAbilities)
            {
                var abilityContext = context with
                {
                    SourceCard = triggeredAbility.SourceCard,
                    TriggeringEvent = triggeringEvent,
                    CurrentEffect = effect,
                    ResolvedTargets = effect.ResolvedTargets,
                    InvocationOptions = invocationOptions
                };

                var result = await context.AbilityManager.ProcessSingleAbilityAsync(triggeredAbility, abilityContext, false, true);
                if (!result.IsSuccess)
                {
                    var error = result.ErrorMessage ?? $"Triggered ability {triggeredAbility.KeywordSummary} failed.";
                    _logger.LogError(
                        "Triggered ability {AbilityKeywords} on card {CardId} failed: {Error}",
                        triggeredAbility.KeywordSummary,
                        triggeredAbility.SourceCardId,
                        error);
                    return PausableTransactionResult.Failure(error);
                }

                var generatedEventResult = await ProcessAbilityGeneratedEventsAsync(
                    abilityContext,
                    result.GeneratedEvents,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,
                    attackFlowsByEventId,
                    recursionDepth + 1);
                if (generatedEventResult.ShouldStop)
                {
                    return generatedEventResult;
                }

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
                        attackFlowsByEventId,
                        recursionDepth + 1);
                    if (subEffectResult.ShouldStop)
                    {
                        return subEffectResult;
                    }
                }
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private List<IAbility> ResolveTriggeredAbilities(
            Effect effect,
            GameContext context,
            TriggerAbilityEffectParams parameters)
        {
            var matchedAbilities = new List<IAbility>();
            var seenRuntimeIds = new HashSet<Guid>();

            foreach (var selectedTarget in effect.SelectedTargets)
            {
                var targetCard = selectedTarget.AsCard;
                if (targetCard == null)
                {
                    continue;
                }

                var targetAbilities = context.AbilityManager
                    .GetEffectiveAbilitiesForCard(targetCard.InstanceId, context)
                    .Where(ability => MatchesTriggeredAbility(ability, parameters));

                foreach (var targetAbility in targetAbilities)
                {
                    if (!seenRuntimeIds.Add(targetAbility.RuntimeId))
                    {
                        continue;
                    }

                    matchedAbilities.Add(targetAbility);
                }
            }

            return matchedAbilities;
        }

        private static bool MatchesTriggeredAbility(IAbility ability, TriggerAbilityEffectParams parameters)
        {
            return parameters.TriggerMode switch
            {
                TriggerAbilityMode.ByKeyword => parameters.AbilityKeyword != AbilityKeyword.None
                    && ability.HasKeyword(parameters.AbilityKeyword),
                TriggerAbilityMode.ByAbilityDefinitionId => parameters.AbilityDefinitionIds.Count > 0
                    && parameters.AbilityDefinitionIds.Contains(ability.DefinitionId),
                TriggerAbilityMode.ByEventType => parameters.TriggerEventType != EventType.Unknown
                    && ability.TriggerConfig?.EventTypes?.Contains(parameters.TriggerEventType) == true,
                _ => false
            };
        }

        private async Task<PausableTransactionResult> ProcessAbilityGeneratedEventsAsync(
            GameContext gameContext,
            IEnumerable<IGameEvent>? generatedEvents,
            List<IGameEvent> processedEvents,
            Dictionary<string, int> eventSignatureCounts,
            Queue<IGameEvent> eventStagingArea,
            Dictionary<Guid, Queue<IGameEvent>> delayedEventsByParent,
            Queue<IGameEvent> queueDrainedDelayedEvents,
            HashSet<Guid> readyQueueDrainedEvents,
            Dictionary<Guid, AttackFlowContext> attackFlowsByEventId,
            int recursionDepth,
            List<IGameEvent>? captureEvents = null,
            HashSet<Guid>? captureEventIds = null)
        {
            if (generatedEvents == null)
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            var deferredAbilityTriggeredEvents = new List<IGameEvent>();
            var seenGeneratedEventIds = new HashSet<Guid>();

            foreach (var generatedEvent in generatedEvents)
            {
                if (generatedEvent == null)
                {
                    continue;
                }

                if (!seenGeneratedEventIds.Add(generatedEvent.EventId))
                {
                    continue;
                }

                if (generatedEvent is AbilityTriggeredEvent)
                {
                    deferredAbilityTriggeredEvents.Add(generatedEvent);
                    continue;
                }

                if (captureEvents != null && captureEventIds != null && captureEventIds.Add(generatedEvent.EventId))
                {
                    captureEvents.Add(generatedEvent);
                }

                if (processedEvents.Any(e => e.EventId == generatedEvent.EventId))
                {
                    continue;
                }

                var recursiveResult = await ProcessEventRecursivelyAsync(
                    gameContext,
                    generatedEvent,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,

                    attackFlowsByEventId,

                    recursionDepth);
                if (recursiveResult.ShouldStop)
                {
                    return recursiveResult;
                }
            }

            foreach (var abilityTriggeredEvent in deferredAbilityTriggeredEvents)
            {
                if (captureEvents != null && captureEventIds != null && captureEventIds.Add(abilityTriggeredEvent.EventId))
                {
                    captureEvents.Add(abilityTriggeredEvent);
                }

                if (processedEvents.Any(e => e.EventId == abilityTriggeredEvent.EventId))
                {
                    continue;
                }

                var recursiveResult = await ProcessEventRecursivelyAsync(
                    gameContext,
                    abilityTriggeredEvent,
                    processedEvents,
                    eventSignatureCounts,
                    eventStagingArea,
                    delayedEventsByParent,
                    queueDrainedDelayedEvents,
                    readyQueueDrainedEvents,

                    attackFlowsByEventId,

                    recursionDepth);
                if (recursiveResult.ShouldStop)
                {
                    return recursiveResult;
                }
            }

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private Task<(PausableTransactionResult Result, Ability? DecisionAbility, IGameEvent? DecisionEvent, Ability.DecisionInput? PreparedInput)> PrepareDecisionEventAsync(
            Effect effect,
            GameContext effectContext,
            int executionIndex = 0)
        {
            if (!IsDecisionEffectType(effect.Type))
            {
                return Task.FromResult((PausableTransactionResult.Success(new List<IGameEvent>()), (Ability?)null, (IGameEvent?)null, (Ability.DecisionInput?)null));
            }

            var ability = effect.SourceAbility as Ability;
            if (ability == null && effect.sourceAbilityId.HasValue)
            {
                ability = effectContext.AbilityManager.GetAbilityByRuntimeId(effect.sourceAbilityId.Value) as Ability;
            }

            if (ability == null)
            {
                return Task.FromResult((PausableTransactionResult.Failure("Decision effect requires a source ability."), (Ability?)null, (IGameEvent?)null, (Ability.DecisionInput?)null));
            }

            var decisionMode = GetDecisionModeFromEffectType(effect.Type);
            Ability.DecisionInput input;
            if (decisionMode == Ability.DecisionMode.Choose
                && _activePreflightSession?.CommitStarted == true
                && TryCreateDecisionCacheKey(effect, ability, isRandom: false, out var cacheKey)
                && TryGetPreflightDecisionState(cacheKey, out var cachedDecision)
                && cachedDecision.PreparedInput != null)
            {
                input = cachedDecision.PreparedInput;
            }
            else
            {
                try
                {
                    input = ability.PrepareDecisionInput(
                        effectContext,
                        effect.TriggeredEventId,
                        effect.EffectIndex,
                        effect.EffectConfig,
                        mode: decisionMode,
                        executionIndex: executionIndex);
                }
                catch (GameLogicException ex)
                {
                    return Task.FromResult((PausableTransactionResult.Failure(ex.Message), (Ability?)null, (IGameEvent?)null, (Ability.DecisionInput?)null));
                }
            }

            IGameEvent decisionEvent = effect.Type switch
            {
                EffectType.Choose => new ChooseEvent(
                    input.OptionIds,
                    input.SelectionCount,
                    ability.SourceCard),
                EffectType.Random => new RandomEvent(
                    input.OptionIds,
                    input.SelectionCount,
                    input.AllowRepeat,
                    input.Weights.ToList(),
                    ability.SourceCard),
                EffectType.Sequence => new SequenceEvent(
                    input.OptionIds,
                    input.SelectionCount,
                    input.LoopSelection,
                    input.SequenceOrder,
                    ability.SourceCard),
                _ => throw new GameLogicException($"Unsupported decision effect type: {effect.Type}")
            };

            return Task.FromResult((PausableTransactionResult.Success(new List<IGameEvent>()), (Ability?)ability, (IGameEvent?)decisionEvent, (Ability.DecisionInput?)input));
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
                SequenceEvent sequenceEvent => sequenceEvent.OptionIds,
                _ => Array.Empty<Guid>()
            };

            IReadOnlyList<int> selectedIndexes = decisionEvent switch
            {
                ChooseEvent chooseEvent => chooseEvent.SelectedOptionIds,
                RandomEvent randomEvent => randomEvent.SelectedOptionIds,
                SequenceEvent sequenceEvent => sequenceEvent.SelectedOptionIds,
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

            var decisionMode = GetDecisionModeFromDecisionEvent(decisionEvent);
            IReadOnlyList<Ability.DecisionOptionSnapshot> options;
            try
            {
                options = ability.GetDecisionOptions(effect.TriggeredEventId, effect.EffectIndex, decisionMode, effect.ExecutionIndex);
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

            var summaryRootAbilityId = effect.SummaryRootAbilityId;
            if (!summaryRootAbilityId.HasValue || summaryRootAbilityId.Value == Guid.Empty)
            {
                summaryRootAbilityId = ability.SummaryRootAbilityId != Guid.Empty
                    ? ability.SummaryRootAbilityId
                    : ability.RuntimeId;
            }

            if (summaryRootAbilityId.HasValue && summaryRootAbilityId.Value != Guid.Empty)
            {
                foreach (var decisionAbility in decisionAbilities.OfType<Ability>())
                {
                    decisionAbility.SummaryRootAbilityId = summaryRootAbilityId.Value;
                }
            }

            var parentScopePath = EnsureSequenceScopePath(effect, ability);
            for (var branchIndex = 0; branchIndex < decisionAbilities.Count; branchIndex++)
            {
                if (decisionAbilities[branchIndex] is Ability decisionConcreteAbility)
                {
                    var selectedOptionIndex = branchIndex < selectedIndexes.Count
                        ? selectedIndexes[branchIndex]
                        : branchIndex;
                    decisionConcreteAbility.SequenceScopePathPrefix = BuildChildSequenceScopePath(parentScopePath, "decision", selectedOptionIndex);
                    AssignInlineDefinitionId(decisionConcreteAbility, ability, effect, "decision", selectedOptionIndex);
                }
            }

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

            if (TryBuildDeferredTargetResolutionKey(effect, ability, out var cacheKey)
                && _deferredTargetResolutionCache.TryGetValue(cacheKey, out var cachedTargets))
            {
                ApplyResolvedTargetsToEffect(effect, cachedTargets);
                effect.ResolveTargetsOnExecution = false;
                return PausableTransactionResult.Success(new List<IGameEvent>());
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

            ApplyResolvedTargetsToEffect(effect, targetResult.ResolvedTargets);
            if (TryBuildDeferredTargetResolutionKey(effect, ability, out cacheKey))
            {
                _deferredTargetResolutionCache[cacheKey] = effect.ResolvedTargets;
            }
            effect.ResolveTargetsOnExecution = false;

            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        private static bool TryBuildDeferredTargetResolutionKey(
            Effect effect,
            IAbility? ability,
            out DeferredTargetResolutionKey key)
        {
            var abilityRuntimeId = ability?.RuntimeId
                ?? (effect.sourceAbilityId.HasValue && effect.sourceAbilityId.Value != Guid.Empty
                    ? effect.sourceAbilityId.Value
                    : Guid.Empty);
            if (abilityRuntimeId == Guid.Empty)
            {
                key = default;
                return false;
            }

            key = new DeferredTargetResolutionKey(abilityRuntimeId, effect.TriggeredEventId);
            return true;
        }

        private static void ApplyResolvedTargetsToEffect(
            Effect effect,
            IReadOnlyDictionary<string, List<TargetInfo>> resolvedTargets)
        {
            effect.ResolvedTargets = resolvedTargets;
            effect.SelectedTargets = ResolveEffectTargets(effect.EffectConfig, resolvedTargets);
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
            Dictionary<Guid, AttackFlowContext> attackFlowsByEventId,
            int recursionDepth)
        {
            _logger.LogDebug("Processing effect: {EffectType}", effect.Type);
            using var effectExecutionScope = BeginEffectExecutionScope(effect.Id);

            if (IsInterceptEffectType(effect.Type))
            {
                _logger.LogDebug("Skipping intercept effect {EffectType} outside of the Before intercept pipeline.", effect.Type);
                return PausableTransactionResult.Success(new List<IGameEvent>());
            }

            // Create a context specific to this effect execution.
            var sourceCard = effect.SourceCardId.HasValue
                ? gameContext.CurrentGameState.FindCardInstance(effect.SourceCardId.Value)
                : null;

            if (ShouldSkipQueuedEffectExecution(effect, gameContext, sourceCard, isInterceptEffect: false))
            {
                return PausableTransactionResult.Success(new List<IGameEvent>());
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

            AttackFlowContext? effectAttackFlow = gameContext.AttackFlow;
            if (triggeringEvent != null && attackFlowsByEventId.TryGetValue(triggeringEvent.EventId, out var mappedAttackFlow))
            {
                effectAttackFlow = mappedAttackFlow;
            }
            else if (effect.TriggeredEventId != Guid.Empty && attackFlowsByEventId.TryGetValue(effect.TriggeredEventId, out mappedAttackFlow))
            {
                effectAttackFlow = mappedAttackFlow;
            }

            var effectContext = gameContext with
            {
                SourceCard = sourceCard,
                TriggeringEvent = triggeringEvent,
                CurrentEffect = effect,
                AttackFlow = effectAttackFlow,
                ResolvedTargets = effect.ResolvedTargets
            };

            var deferredResolutionResult = await ResolveDeferredTargetsIfNeededAsync(effect, effectContext);
            if (deferredResolutionResult.ShouldStop)
            {
                return deferredResolutionResult;
            }

            effectContext = effectContext with { ResolvedTargets = effect.ResolvedTargets };

            var repeatCountResult = ResolveEffectRepeatCount(effect, effectContext);
            if (!repeatCountResult.Success)
            {
                return PausableTransactionResult.Failure(repeatCountResult.ErrorMessage ?? "Failed to resolve repeat count.");
            }

            var repeatCount = repeatCountResult.RepeatCount;
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
                        shouldClearDecisionCache = !HasPreflightDecisionState(cacheKey);
                    }

                    if (shouldClearDecisionCache)
                    {
                        decisionAbility?.ClearDecisionCache(effect.TriggeredEventId, effect.EffectIndex, executionIndex);
                    }
                }

                var decisionPreparation = await PrepareDecisionEventAsync(effect, effectContext, executionIndex);
                if (decisionPreparation.Result.ShouldStop) return decisionPreparation.Result;

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

                        attackFlowsByEventId,

                        recursionDepth + 1);
                    if (decisionEventResult.ShouldStop) return decisionEventResult;
                }

                var decisionAbilities = new List<IAbility>();
                if (decisionPreparation.DecisionEvent != null && decisionPreparation.DecisionAbility != null)
                {
                    if (TryCreateDecisionCacheKey(
                            effect,
                            decisionPreparation.DecisionAbility,
                            effect.Type == EffectType.Random,
                            out var decisionCacheKey)
                        && TryGetPreflightDecisionState(decisionCacheKey, out var preflightDecisionState)
                        && preflightDecisionState.BranchAbilities.Count > 0)
                    {
                        decisionAbilities = preflightDecisionState.BranchAbilities.ToList();
                    }
                    else
                    {
                        var abilityResult = ResolveDecisionAbilitiesFromEvent(
                            effect,
                            effectContext,
                            decisionPreparation.DecisionAbility,
                            decisionPreparation.DecisionEvent,
                            out decisionAbilities);
                        if (abilityResult.ShouldStop) return abilityResult;
                    }
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
                        attackFlowsByEventId,
                        recursionDepth + 1);
                    if (decisionResult.ShouldStop) return decisionResult;
                }

                effect.GeneratedEvents = decisionEvents;

                var effectResult = _effectExecutor.ExecuteEffect(effect, effectContext); // Use the new context

                var generatedEvents = (effectResult.GeneratedEvents ?? new List<IGameEvent>())
                    .Where(e => e is not EffectCompletedEvent)
                    .ToList();
                effectResult.GeneratedEvents = generatedEvents;
                var generatedCards = CollectGeneratedCards(generatedEvents);
                var generatedSnapshots = generatedCards.Select(CardInstanceSnapshot.From).ToList();
                var eventProcessingSucceeded = true;
                string? eventProcessingError = null;
                EffectCompletedEvent? effectCompletedEvent = null;

                foreach (var newEvent in generatedEvents)
                {
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

                        attackFlowsByEventId,

                        recursionDepth + 1);
                    if (result.ShouldStop)
                    {
                        eventProcessingSucceeded = false;
                        eventProcessingError = result.ErrorMessage ?? "Generated event processing failed.";
                        break;
                    }
                }

                var completionOutputs = effectResult.Outputs?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    ?? new Dictionary<string, object?>();
                completionOutputs["effectFailed"] = !(effectResult.Success && eventProcessingSucceeded);

                var completionResolvedParameters = effectResult.ResolvedParameters?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    ?? new Dictionary<string, object>();

                var generatedEventsSnapshot = generatedEvents.ToList().AsReadOnly();

                effectCompletedEvent = new EffectCompletedEvent(
                    effect.EffectConfig.Type,
                    completionResolvedParameters.AsReadOnly(),
                    effect.SelectedTargets.AsReadOnly(),
                    generatedEventsSnapshot,
                    generatedCards,
                    generatedSnapshots,
                    completionOutputs.AsReadOnly(),
                    effectResult.Success && eventProcessingSucceeded,
                    effectContext.SourceCard);

                generatedEvents.Add(effectCompletedEvent);
                effectResult.GeneratedEvents = generatedEvents;

                if (eventProcessingSucceeded)
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

                            attackFlowsByEventId,

                            recursionDepth + 1);
                        if (result.ShouldStop)
                        {
                            eventProcessingSucceeded = false;
                            eventProcessingError = result.ErrorMessage ?? "Generated event processing failed.";
                        }
                    }
                }

                if (eventProcessingSucceeded
                    && effect.Type == EffectType.TriggerAbility
                    && effectResult.ReturnValue is TriggerAbilityEffectParams triggerAbilityParams
                    && effectCompletedEvent != null)
                {
                    var triggerAbilityResult = await ExecuteTriggeredAbilitiesAsync(
                        effect,
                        effectContext,
                        triggerAbilityParams,
                        effectCompletedEvent,
                        processedEvents,
                        eventSignatureCounts,
                        eventStagingArea,
                        delayedEventsByParent,
                        queueDrainedDelayedEvents,
                        readyQueueDrainedEvents,
                        attackFlowsByEventId,
                        recursionDepth + 1);
                    if (triggerAbilityResult.ShouldStop)
                    {
                        eventProcessingSucceeded = false;
                        eventProcessingError = triggerAbilityResult.ErrorMessage ?? "Triggered ability execution failed.";
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
                            var errorMessage = eventProcessingError
                                ?? effectResult.ErrorMessage
                                ?? $"Effect execution failed for {effect.Type}.";
                            _logger.LogError(errorMessage);
                            return PausableTransactionResult.Failure(errorMessage);
                        default:
                            _logger.LogWarning(
                                "Effect {EffectType} failed with unknown OnFailure value '{OnFailure}'. Falling back to CompleteWithFailure. Error: {Error}",
                                effect.Type,
                                effect.EffectConfig.OnFailure,
                                eventProcessingError ?? effectResult.ErrorMessage ?? "Unknown");
                            break;
                    }
                }

                if (ShouldSkipQueuedEffectExecution(effect, effectContext, sourceCard, isInterceptEffect: false))
                {
                    _logger.LogDebug(
                        "Stopping remaining executions for effect {EffectType} ({EffectId}) because the source ability was invalidated during execution.",
                        effect.Type,
                        effect.Id);
                    break;
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

                    attackFlowsByEventId,

                    recursionDepth + 1);
                if (chainedResult.ShouldStop) return chainedResult;
            }

            if (IsDecisionEffectType(effect.Type))
            {
                if (effect.SourceAbility is Ability ability)
                {
                    for (var executionIndex = 0; executionIndex < totalExecutions; executionIndex++)
                    {
                        ability.ClearDecisionCache(effect.TriggeredEventId, effect.EffectIndex, executionIndex);
                    }
                }
            }

            ReleaseDelayedEventsIfReady(effect.Id, eventStagingArea, delayedEventsByParent);
            ReleaseDelayedEventsIfReady(effect.TriggeredEventId, eventStagingArea, delayedEventsByParent);
            return PausableTransactionResult.Success(new List<IGameEvent>());
        }

        /// <inheritdoc />
        public Task<PausableTransactionResult> CancelPendingActionAsync(GameContext gameContext)
        {
            _logger.LogInformation("Attempting to cancel pending action for player: {PlayerId}", gameContext.RequestingPlayerId);

            if (_activePreflightSession?.CommitStarted == true)
            {
                return Task.FromResult(PausableTransactionResult.Failure("Cannot cancel action after commit has started."));
            }

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
