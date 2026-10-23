using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Infrastructure.Logging;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using Microsoft.AspNetCore.SignalR;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;

using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// Defines the possible outcomes of a target resolution process.
    /// </summary>
    public enum ResolutionStatus
    {
        /// <summary>
        /// All targets were resolved automatically without errors.
        /// </summary>
        Completed,
        /// <summary>
        /// Target resolution was intentionally skipped (non-fatal), so ability execution should be skipped.
        /// </summary>
        Skipped,
        /// <summary>
        /// The process was paused because player input is required.
        /// </summary>
        PendingPlayerChoice,
        /// <summary>
        /// An error occurred during the resolution process.
        /// </summary>
        Failed
    }

    /// <summary>
    /// Represents the result of a full target resolution process for an ability.
    /// </summary>
    public class TargetResolutionResult
    {
        /// <summary>
        /// The final status of the resolution process.
        /// </summary>
        public ResolutionStatus Status { get; set; }

        /// <summary>
        /// A dictionary containing all successfully resolved targets, keyed by their identifier.
        /// This is populated only when the status is 'Completed'.
        /// </summary>
        public Dictionary<string, List<TargetInfo>> ResolvedTargets { get; set; } = new();

        /// <summary>
        /// Contains details of the error if the status is 'Failed'.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Contains the details of the choice required from the player if the status is 'PendingPlayerChoice'.
        /// </summary>
        public PlayerChoiceRequest? ChoiceRequest { get; set; }

        /// <summary>
        /// Indicates the player cancelled a required choice during resolution.
        /// </summary>
        public bool WasCancelled { get; set; }
    }

    /// <summary>
    /// A service that handles the complex, multi-step process of resolving all targets for an ability,
    /// including handling dependencies between targets.
    /// </summary>
    public class TargetSelectionService
    {
        private readonly ILogger<TargetSelectionService> _logger;
        private readonly IQueryExecutor _cardQueryExecutor;
        private readonly IPendingActionService _pendingActionService;

        public TargetSelectionService(ILogger<TargetSelectionService> logger, IQueryExecutor cardQueryExecutor, IPendingActionService pendingActionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cardQueryExecutor = cardQueryExecutor ?? throw new ArgumentNullException(nameof(cardQueryExecutor));
            _pendingActionService = pendingActionService ?? throw new ArgumentNullException(nameof(pendingActionService));
        }

        // TODO: Refactor or remove ResolvePlayerChoice as its logic will be integrated into the new async resolution flow.

        /// <summary>
        /// Resolves all targets for a given ability, handling dependencies and player choices.
        /// This is the new entry point for the atomic, pre-selection process.
        /// </summary>
        public async Task<TargetResolutionResult> ResolveTargetsForAbilityAsync(IAbility ability, GameContext context, bool isCancellable)
        {
            var finalResult = new TargetResolutionResult();
            var queryExecutor = context.ServiceProvider.GetRequiredService<IQueryExecutor>();
            var triggeringEventId = context.TriggeringEvent?.EventId ?? Guid.Empty;

            var resolvedTargetsForQuery = new Dictionary<string, List<TargetInfo>>();
            var traceSettings = context.ServiceProvider.GetService<IConditionTraceSettings>();
            var traceEntries = traceSettings?.IsTargetEnabledFor(context) == true ? new List<ConditionTraceEntryDto>() : null;

            // Iterate through the ability's runtime Target objects.
            foreach (var target in ability.Targets)
            {
                if (triggeringEventId == Guid.Empty || target.LastResolvedEventId != triggeringEventId)
                {
                    target.MarkForResolution();
                }

                if (!target.Config.IsEnabled)
                {
                    target.SetResolvedTargets(new List<TargetInfo>(), triggeringEventId);
                    resolvedTargetsForQuery[target.Key] = target.ResolvedTargets;
                    continue;
                }

                // Leverage the Target's internal state. If it's already resolved, skip it.
                if (!target.NeedsResolution)
                {
                    continue;
                }

                // Execute the query to get potential targets.
                // We pass the currently resolved targets from our finalResult for dependent queries.
                var queryResult = queryExecutor.ExecuteQuery(target.Config.Query, context, resolvedTargetsForQuery);
                if (traceEntries != null && queryResult.TraceEntries.Count > 0)
                {
                    traceEntries.AddRange(queryResult.TraceEntries);
                }

                if (!queryResult.Success)
                {
                    finalResult.Status = ResolutionStatus.Failed;
                    finalResult.ErrorMessage = $"目标“{target.Key}”查询执行失败：{queryResult.ErrorMessage}";
                    _logger.LogError(finalResult.ErrorMessage);
                    SendTargetTrace(context, ability, false, traceEntries, GetResolvedTargetCount(resolvedTargetsForQuery), finalResult.ErrorMessage);
                    return finalResult; // Abort on failure.
                }

                var potentialTargets = queryResult.Results.Select(item => item switch
                {
                    CardInstance card => TargetInfo.ForCard(card),
                    _ => null
                })
                .Where(t => t != null)
                .Select(t => t!) // Add this line to explicitly cast away nullability
                .ToList();

                var candidateTargets = ApplyAbilityTargetKeywordRules(potentialTargets, target.Config, context, applyChoiceRestrictions: false);

                // Check if this specific target requires player choice.
                if (target.Config.PlayerChoiceOptions?.IsRequired == true)
                {
                    var resolutionPoolTargets = candidateTargets;
                    var choicePoolTargets = ApplyAbilityTargetKeywordRules(
                        candidateTargets,
                        target.Config,
                        context,
                        applyChoiceRestrictions: true,
                        enforceSpellWard: false);

                    var maxConfiguredSelectionCount = Math.Max(1, target.Config.PlayerChoiceOptions.MaxCount);
                    var enforceSpellWardOnSelectablePool = maxConfiguredSelectionCount <= 1;
                    var selectableTargets = ApplyAbilityTargetKeywordRules(
                        choicePoolTargets,
                        target.Config,
                        context,
                        applyChoiceRestrictions: true,
                        enforceSpellWard: enforceSpellWardOnSelectablePool);

                    var configuredMinSelectionCount = Math.Max(0, target.Config.PlayerChoiceOptions.MinCount);
                    var requiredMinSelectionCount = Math.Max(1, configuredMinSelectionCount);
                    var isFusionChoice = ability.HasKeyword(AbilityKeyword.Fusion);
                    var selectableCount = selectableTargets.Count;
                    var forceFusionChoicePrompt = isFusionChoice
                        && isCancellable
                        && selectableCount < requiredMinSelectionCount;

                    if (selectableCount < requiredMinSelectionCount && !forceFusionChoicePrompt)
                    {
                        if (target.Config.ShouldBlockAbilityExecutionOnInsufficientTargets())
                        {
                            var handlingStrategy = target.Config.GetEffectiveTargetHandlingStrategy();
                            if (handlingStrategy == TargetHandlingStrategy.BlockPlayerOperation)
                            {
                                finalResult.Status = ResolutionStatus.Failed;
                                finalResult.ErrorMessage = $"能力“{ability.KeywordSummary}”的目标“{target.Key}”可选目标不足：需要 {requiredMinSelectionCount} 个，实际仅有 {selectableCount} 个。";
                                _logger.LogInformation(finalResult.ErrorMessage);
                                SendTargetTrace(context, ability, false, traceEntries, GetResolvedTargetCount(resolvedTargetsForQuery), finalResult.ErrorMessage);
                                return finalResult;
                            }

                            finalResult.Status = ResolutionStatus.Skipped;
                            finalResult.ErrorMessage = $"能力“{ability.KeywordSummary}”的目标“{target.Key}”可选目标不足：需要 {requiredMinSelectionCount} 个，实际仅有 {selectableCount} 个。已按配置跳过能力执行。";
                            _logger.LogInformation(finalResult.ErrorMessage);
                            SendTargetTrace(context, ability, true, traceEntries, GetResolvedTargetCount(resolvedTargetsForQuery), finalResult.ErrorMessage);
                            return finalResult;
                        }

                        _logger.LogInformation(
                            "Selectable targets ({Available}) below minimum ({Min}) for ability {AbilityKeywords} (target key '{TargetKey}'). Auto-selecting all selectable targets because execution blocking is disabled.",
                            selectableCount,
                            requiredMinSelectionCount,
                            ability.KeywordSummary,
                            target.Key);
                        target.SetResolvedTargets(selectableTargets, triggeringEventId);
                        resolvedTargetsForQuery[target.Key] = target.ResolvedTargets;
                        continue;
                    }

                    var requestMinSelectionCount = forceFusionChoicePrompt && selectableCount == 0
                        ? Math.Max(1, configuredMinSelectionCount)
                        : configuredMinSelectionCount;
                    var configuredMaxSelectionCount = Math.Max(
                        requestMinSelectionCount,
                        Math.Max(1, target.Config.PlayerChoiceOptions.MaxCount));
                    // Keep the configured selection upper-bound as-is. The request should describe the
                    // authoritative choice interval, while actual selectable IDs constrain what can be chosen.
                    var maxSelectionCount = configuredMaxSelectionCount;
                    var message = forceFusionChoicePrompt && selectableCount == 0
                        ? "没有可供选择的目标。"
                        : BuildPlayerChoicePrompt(requestMinSelectionCount, maxSelectionCount);

                    if (context.InvocationOptions.AutoRandomTargetSelection)
                    {
                        var randomSelectionCount = ResolveRandomSelectionCount(
                            maxSelectionCount,
                            selectableCount);
                        var randomlySelectedTargets = SelectRandomTargets(
                            selectableTargets,
                            choicePoolTargets,
                            randomSelectionCount,
                            context);

                        var randomSelectedIdSet = new HashSet<Guid>(randomlySelectedTargets.Select(target => target.InstanceId));
                        List<TargetInfo> randomChosenTargets;
                        if (target.Config.PlayerChoiceOptions?.IsInverseSelection == true)
                        {
                            randomChosenTargets = resolutionPoolTargets
                                .Where(t => !randomSelectedIdSet.Contains(t.InstanceId))
                                .ToList();
                        }
                        else
                        {
                            randomChosenTargets = randomlySelectedTargets;
                        }

                        target.SetResolvedTargets(randomChosenTargets, triggeringEventId);
                        resolvedTargetsForQuery[target.Key] = randomChosenTargets;
                        continue;
                    }

                    var choiceRequest = new PlayerChoiceRequest
                    {
                        PendingChoiceId = Guid.NewGuid(),
                        SourceAbilityRuntimeId = ability.RuntimeId,
                        TargetKey = target.Key,
                        RequestingPlayerId = context.RequestingPlayerId,
                        SelectableTargetIds = selectableTargets.Select(t => t.InstanceId).ToList(),
                        SelectableCardInstances = selectableTargets
                            .Where(t => t.Type == TargetType.Card && t.Card != null)
                            .Select(t => context.Mapper.Map<CardInstanceDto>(t.Card!))
                            .ToList(),
                        MinSelectionCount = requestMinSelectionCount,
                        MaxSelectionCount = maxSelectionCount,
                        Message = message,
                        IsCancellable = isCancellable
                    };

                    // --- CORE REFACTOR ---
                    // Instead of returning, we now await the player's choice directly.
                    _logger.LogInformation("Awaiting player choice for ability {AbilityKeywords}, target key '{TargetKey}'.", ability.KeywordSummary, target.Key);
                    var choiceResult = await _pendingActionService.WaitForPlayerChoiceAsync(context.CurrentGameState.GameId, choiceRequest, context.CancellationToken);

                    if (choiceResult.WasCancelled)
                    {
                        finalResult.Status = ResolutionStatus.Failed;
                        finalResult.WasCancelled = true;
                        finalResult.ErrorMessage = $"已取消能力“{ability.KeywordSummary}”的目标选择。";
                        _logger.LogInformation(finalResult.ErrorMessage);
                        SendTargetTrace(context, ability, false, traceEntries, GetResolvedTargetCount(resolvedTargetsForQuery), finalResult.ErrorMessage);
                        return finalResult;
                    }

                    // Filter the potential targets based on the player's selection.
                    var chosenTargetIdSet = new HashSet<Guid>(choiceResult.ChosenTargetIds);
                    var playerSelectedTargets = selectableTargets
                        .Where(t => chosenTargetIdSet.Contains(t.InstanceId))
                        .ToList();

                    if (!SatisfiesSpellWardSelectionRule(playerSelectedTargets, choicePoolTargets, context))
                    {
                        finalResult.Status = ResolutionStatus.Failed;
                        finalResult.ErrorMessage = $"能力“{ability.KeywordSummary}”的目标选择无效：存在守护时必须优先选择守护目标。";
                        _logger.LogInformation(finalResult.ErrorMessage);
                        SendTargetTrace(context, ability, false, traceEntries, GetResolvedTargetCount(resolvedTargetsForQuery), finalResult.ErrorMessage);
                        return finalResult;
                    }

                    List<TargetInfo> chosenTargets;
                    if (target.Config.PlayerChoiceOptions?.IsInverseSelection == true)
                    {
                        chosenTargets = resolutionPoolTargets
                            .Where(t => !chosenTargetIdSet.Contains(t.InstanceId))
                            .ToList();
                    }
                    else
                    {
                        chosenTargets = playerSelectedTargets;
                    }

                    target.SetResolvedTargets(chosenTargets, triggeringEventId);
                    resolvedTargetsForQuery[target.Key] = chosenTargets;
                    // --- END REFACTOR ---
                }
                else
                {
                    // Auto-select targets and, crucially, update the state of the Target object itself.
                    target.SetResolvedTargets(candidateTargets, triggeringEventId);
                    resolvedTargetsForQuery[target.Key] = candidateTargets;
                }
            }

            // If we've looped through all targets without needing player choice, the resolution is complete.
            finalResult.Status = ResolutionStatus.Completed;
            // Collect the final results from the now-updated Target objects within the ability.
            finalResult.ResolvedTargets = new Dictionary<string, List<TargetInfo>>();
            foreach (var target in ability.Targets)
            {
                finalResult.ResolvedTargets[target.Key] = target.ResolvedTargets;
            }

            var totalCount = GetResolvedTargetCount(finalResult.ResolvedTargets);
            var successMessage = $"能力 {ability.Name} 目标解析完成，选中 {totalCount} 个目标。";
            SendTargetTrace(context, ability, true, traceEntries, totalCount, successMessage);

            return finalResult;
        }


        /// <summary>
        /// Performs a non-blocking check to see if an ability has valid targets, based on its configuration.
        /// This is used for playability checks and does NOT trigger player choice prompts.
        /// </summary>
        public async Task<TargetAvailabilityResult> CheckTargetAvailabilityAsync(IAbility ability, GameContext context)
        {
            var queryExecutor = context.ServiceProvider.GetRequiredService<IQueryExecutor>();
            var anyChoiceTarget = false;
            var hasAnyValidChoiceTarget = false;
            var canPlayWithoutTarget = false;
            var choiceValidTargetIds = new HashSet<Guid>();

            foreach (var target in ability.Targets)
            {
                if (!target.Config.IsEnabled)
                {
                    continue;
                }

                var requiresPlayerChoice = target.Config.PlayerChoiceOptions?.IsRequired == true;
                var targetHandlingStrategy = target.Config.GetEffectiveTargetHandlingStrategy();
                if (targetHandlingStrategy == TargetHandlingStrategy.DoNotIntercept && !requiresPlayerChoice)
                {
                    continue;
                }

                var queryResult = queryExecutor.ExecuteQuery(target.Config.Query, context, new Dictionary<string, List<TargetInfo>>());

                if (!queryResult.Success)
                {
                    _logger.LogWarning("Target availability check failed for ability {AbilityKeywords}, target '{TargetKey}': Query execution failed: {Error}",
                        ability.KeywordSummary, target.Key, queryResult.ErrorMessage);
                    return new TargetAvailabilityResult(
                        false,
                        $"目标查询失败：{queryResult.ErrorMessage}",
                        HasValidTargets: false,
                        ValidTargetIds: new List<Guid>());
                }

                var potentialTargets = queryResult.Results.Select(item => item switch
                    {
                        CardInstance card => TargetInfo.ForCard(card),
                        _ => null
                    })
                    .Where(t => t != null)
                    .Select(t => t!)
                    .ToList();

                var applyChoiceRestrictions = requiresPlayerChoice;
                var enforceSpellWard = !requiresPlayerChoice
                    || (target.Config.PlayerChoiceOptions?.MaxCount ?? 1) <= 1;
                potentialTargets = ApplyAbilityTargetKeywordRules(
                    potentialTargets,
                    target.Config,
                    context,
                    applyChoiceRestrictions: applyChoiceRestrictions,
                    enforceSpellWard: enforceSpellWard);

                if (requiresPlayerChoice)
                {
                    anyChoiceTarget = true;
                    if (potentialTargets.Any())
                    {
                        hasAnyValidChoiceTarget = true;
                        foreach (var targetId in potentialTargets.Select(t => t.InstanceId))
                        {
                            choiceValidTargetIds.Add(targetId);
                        }
                    }
                }

                var requiredMinSelectionCount = requiresPlayerChoice
                    ? Math.Max(1, Math.Max(0, target.Config.PlayerChoiceOptions!.MinCount))
                    : 1;

                if (potentialTargets.Count < requiredMinSelectionCount)
                {
                    _logger.LogInformation("Target availability check failed for ability {AbilityKeywords}, target '{TargetKey}': requires {Min} targets but only {Available} available.",
                        ability.KeywordSummary, target.Key, requiredMinSelectionCount, potentialTargets.Count);

                    if (targetHandlingStrategy == TargetHandlingStrategy.BlockPlayerOperation)
                    {
                        var failureReason = $"可选目标不足：需要 {requiredMinSelectionCount} 个，实际仅有 {potentialTargets.Count} 个。";
                        return new TargetAvailabilityResult(
                            false,
                            failureReason,
                            HasValidTargets: potentialTargets.Any(),
                            ValidTargetIds: potentialTargets.Select(t => t.InstanceId).ToList());
                    }

                    canPlayWithoutTarget = true;
                }
            }

            await Task.CompletedTask;
            if (!anyChoiceTarget)
            {
                return new TargetAvailabilityResult(true);
            }

            return new TargetAvailabilityResult(
                true,
                FailureReason: null,
                HasValidTargets: hasAnyValidChoiceTarget,
                ValidTargetIds: choiceValidTargetIds.ToList(),
                CanPlayWithoutTarget: canPlayWithoutTarget);
        }

        private static int GetResolvedTargetCount(Dictionary<string, List<TargetInfo>>? resolvedTargets)
        {
            if (resolvedTargets == null || resolvedTargets.Count == 0)
            {
                return 0;
            }

            var total = 0;
            foreach (var entry in resolvedTargets.Values)
            {
                total += entry?.Count ?? 0;
            }

            return total;
        }

        private void SendTargetTrace(
            GameContext context,
            IAbility ability,
            bool success,
            List<ConditionTraceEntryDto>? entries,
            int targetCount,
            string message)
        {
            if (entries == null)
            {
                return;
            }

            var traceSettings = context.ServiceProvider.GetService<IConditionTraceSettings>();
            var hubContext = context.ServiceProvider.GetService<IHubContext<GameHub, IGameClient>>();
            if (traceSettings == null || hubContext == null || context.CurrentGameState == null)
            {
                return;
            }
            var matchService = context.ServiceProvider.GetService<IMatchService>();
            var roomScopedId = matchService != null
                && matchService.TryGetRoomIdByGameId(context.CurrentGameState.GameId, out var roomId)
                && !string.IsNullOrWhiteSpace(roomId)
                    ? roomId
                    : context.CurrentGameState.GameId;

            var sourceCard = ability.SourceCard;
            var cardName = sourceCard?.Name ?? "UnknownCard";
            var cardId = sourceCard?.InstanceId.ToString() ?? "UnknownId";

            var playerIds = new[]
            {
                context.CurrentGameState.Player1?.PlayerId,
                context.CurrentGameState.Player2?.PlayerId
            };

            foreach (var playerId in playerIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct())
            {
                if (playerId == Guid.Empty || !traceSettings.IsTargetEnabled(playerId))
                {
                    continue;
                }

                var dto = new ConditionTraceFailedDto
                {
                    GameId = context.CurrentGameState.GameId,
                    AbilityRuntimeId = ability.RuntimeId,
                    AbilityName = ability.Name ?? string.Empty,
                    CardName = cardName,
                    CardId = cardId,
                    Success = success,
                    TraceKind = "Target",
                    TargetCount = targetCount,
                    ConditionId = string.Empty,
                    Expression = entries.Count > 0 ? entries[0].Expression : string.Empty,
                    Trace = entries.Count > 0 ? entries[0].Trace : string.Empty,
                    Conditions = entries,
                    Message = message
                };

                if (success)
                {
                    _ = hubContext.Clients.Group(SignalRGroupNames.Player(roomScopedId, playerId)).ConditionTraceSucceeded(dto);
                }
                else
                {
                    _ = hubContext.Clients.Group(SignalRGroupNames.Player(roomScopedId, playerId)).ConditionTraceFailed(dto);
                }
            }
        }

        private static List<TargetInfo> ApplyAbilityTargetKeywordRules(
            List<TargetInfo> potentialTargets,
            TargetConfig config,
            GameContext context,
            bool applyChoiceRestrictions,
            bool enforceSpellWard = true)
        {
            // SpellWard only limits explicit player-picked targets.
            // Auto/random target resolution should not be filtered by SpellWard.
            var abilityOwnerId = context.SourceCard?.OwnerPlayerId ?? context.RequestingPlayerId;
            var opponentPlayer = context.CurrentGameState.GetOpponentPlayerState(abilityOwnerId);
            var potentialTargetIds = new HashSet<Guid>(
                potentialTargets
                    .Where(target => target.Type == TargetType.Card)
                    .Select(target => target.InstanceId));
            var activeSpellWardFollowerIds = new HashSet<Guid>(
                (opponentPlayer?.Field ?? new List<CardInstance>())
                    .Where(card => card != null
                                   && card.IsFollower
                                   && card.HasKeyword(CardKeyword.SpellWard)
                                   && !card.HasKeyword(CardKeyword.Aura))
                    .Select(card => card.InstanceId));
            activeSpellWardFollowerIds.IntersectWith(potentialTargetIds);
            var hasActiveSpellWard = activeSpellWardFollowerIds.Any();

            // Ability targeted: only enforce for opponent target selection by default.
            return potentialTargets.Where(targetInfo =>
            {
                if (targetInfo.Type != TargetType.Card || targetInfo.Card == null)
                {
                    return true;
                }

                var card = targetInfo.Card;
                if (EffectTargetImmunityPolicy.IsImmuneTarget(card, context))
                {
                    return false;
                }

                var isOpponentCard = card.OwnerPlayerId != abilityOwnerId;
                if (enforceSpellWard && applyChoiceRestrictions && isOpponentCard && hasActiveSpellWard && !activeSpellWardFollowerIds.Contains(card.InstanceId))
                {
                    return false;
                }

                if (!applyChoiceRestrictions)
                {
                    return true;
                }

                if (!isOpponentCard)
                {
                    return true;
                }

                return !card.HasKeyword(CardKeyword.EarthSigil) && !card.HasKeyword(CardKeyword.Aura) && !card.HasKeyword(CardKeyword.Ambush);
            }).ToList();
        }

        private static bool SatisfiesSpellWardSelectionRule(
            IReadOnlyCollection<TargetInfo> chosenTargets,
            IReadOnlyCollection<TargetInfo> initialPoolTargets,
            GameContext context)
        {
            var abilityOwnerId = context.SourceCard?.OwnerPlayerId ?? context.RequestingPlayerId;
            var opponentPlayer = context.CurrentGameState.GetOpponentPlayerState(abilityOwnerId);
            var initialPoolTargetIds = new HashSet<Guid>(
                initialPoolTargets
                    .Where(target => target.Type == TargetType.Card)
                    .Select(target => target.InstanceId));
            var activeSpellWardFollowerIds = new HashSet<Guid>(
                (opponentPlayer?.Field ?? new List<CardInstance>())
                    .Where(card => card != null
                                   && card.IsFollower
                                   && card.HasKeyword(CardKeyword.SpellWard)
                                   && !card.HasKeyword(CardKeyword.Aura))
                    .Select(card => card.InstanceId));
            activeSpellWardFollowerIds.IntersectWith(initialPoolTargetIds);

            if (!activeSpellWardFollowerIds.Any())
            {
                return true;
            }

            var selectedOpponentCards = chosenTargets
                .Where(target => target.Type == TargetType.Card
                                 && target.Card != null
                                 && target.Card.OwnerPlayerId != abilityOwnerId)
                .Select(target => target.Card!)
                .ToList();

            if (!selectedOpponentCards.Any())
            {
                return true;
            }

            var selectedSpellWardCount = selectedOpponentCards.Count(card => activeSpellWardFollowerIds.Contains(card.InstanceId));
            var requiredSpellWardCount = Math.Min(selectedOpponentCards.Count, activeSpellWardFollowerIds.Count);
            return selectedSpellWardCount >= requiredSpellWardCount;
        }

        private static string BuildPlayerChoicePrompt(int minCount, int maxCount)
        {
            if (maxCount <= 0)
            {
                return "没有可供选择的目标。";
            }

            if (minCount <= 0)
            {
                return maxCount == 1 ? "请选择 1 个目标。" : $"请选择最多 {maxCount} 个目标。";
            }

            if (minCount >= maxCount)
            {
                return $"请选择 {maxCount} 个目标。";
            }

            return $"请选择 {minCount}-{maxCount} 个目标。";
        }

        private static int ResolveRandomSelectionCount(
            int maxSelectionCount,
            int availableCount)
        {
            if (availableCount <= 0)
            {
                return 0;
            }

            return Math.Min(Math.Max(1, maxSelectionCount), availableCount);
        }

        private static List<TargetInfo> SelectRandomTargets(
            IReadOnlyList<TargetInfo> selectableTargets,
            IReadOnlyCollection<TargetInfo> choicePoolTargets,
            int selectionCount,
            GameContext context)
        {
            if (selectionCount <= 0 || selectableTargets.Count == 0)
            {
                return new List<TargetInfo>();
            }

            if (selectionCount >= selectableTargets.Count)
            {
                return selectableTargets.ToList();
            }

            var rng = Random.Shared;
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var sampled = selectableTargets
                    .OrderBy(_ => rng.Next())
                    .Take(selectionCount)
                    .ToList();

                if (SatisfiesSpellWardSelectionRule(sampled, choicePoolTargets, context))
                {
                    return sampled;
                }
            }

            return BuildSpellWardSafeSelection(selectableTargets, choicePoolTargets, selectionCount, context, rng);
        }

        private static List<TargetInfo> BuildSpellWardSafeSelection(
            IReadOnlyList<TargetInfo> selectableTargets,
            IReadOnlyCollection<TargetInfo> choicePoolTargets,
            int selectionCount,
            GameContext context,
            Random rng)
        {
            var selected = new List<TargetInfo>(selectionCount);
            var abilityOwnerId = context.SourceCard?.OwnerPlayerId ?? context.RequestingPlayerId;
            var opponentPlayer = context.CurrentGameState.GetOpponentPlayerState(abilityOwnerId);
            var choicePoolIds = new HashSet<Guid>(
                choicePoolTargets
                    .Where(target => target.Type == TargetType.Card)
                    .Select(target => target.InstanceId));
            var spellWardIds = new HashSet<Guid>(
                (opponentPlayer?.Field ?? new List<CardInstance>())
                    .Where(card => card != null
                                   && card.IsFollower
                                   && card.HasKeyword(CardKeyword.SpellWard)
                                   && !card.HasKeyword(CardKeyword.Aura))
                    .Select(card => card.InstanceId));
            spellWardIds.IntersectWith(choicePoolIds);

            var selfOrAllyTargets = selectableTargets
                .Where(target => target.Card == null || target.Card.OwnerPlayerId == abilityOwnerId)
                .OrderBy(_ => rng.Next())
                .ToList();

            foreach (var target in selfOrAllyTargets)
            {
                if (selected.Count >= selectionCount)
                {
                    return selected;
                }

                selected.Add(target);
            }

            var wardTargets = selectableTargets
                .Where(target => target.Card != null
                                 && target.Card.OwnerPlayerId != abilityOwnerId
                                 && spellWardIds.Contains(target.InstanceId))
                .OrderBy(_ => rng.Next())
                .ToList();

            foreach (var target in wardTargets)
            {
                if (selected.Count >= selectionCount)
                {
                    return selected;
                }

                selected.Add(target);
            }

            var remainingTargets = selectableTargets
                .Where(target => selected.All(existing => existing.InstanceId != target.InstanceId))
                .OrderBy(_ => rng.Next())
                .ToList();

            foreach (var target in remainingTargets)
            {
                if (selected.Count >= selectionCount)
                {
                    break;
                }

                selected.Add(target);
            }

            return selected;
        }
        private void HandleSelectionFailure(FailureHandling failureHandling, Exception exception)
        {
            switch (failureHandling)
            {
                case FailureHandling.AbortAbility:
                    throw new TargetSelectionException("Target selection failed, aborting ability execution.", exception);
                case FailureHandling.SkipEffect:
                    _logger.LogWarning(exception, "Target selection failed. Skipping effect as per configuration.");
                    break;
                case FailureHandling.CompleteWithFailure:
                    _logger.LogWarning(exception, "Target selection failed. Continuing due to CompleteWithFailure.");
                    break;
                default:
                    _logger.LogWarning(exception, "Target selection failed with unknown FailureHandling value '{FailureHandling}'. Falling back to CompleteWithFailure behavior.", failureHandling);
                    break;
            }
        }
    }

    public class TargetSelectionException : Exception
    {
        public TargetSelectionException(string message, Exception? innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Represents the result of a non-blocking target availability check.
    /// </summary>
    /// <param name="IsAvailable">Indicates whether all required targets have at least one potential target.</param>
    /// <param name="FailureReason">Provides a reason if the targets are not available.</param>
    /// <param name="HasValidTargets">For player-choice targets, indicates whether any selectable targets exist.</param>
    /// <param name="ValidTargetIds">For player-choice targets, provides selectable target IDs when available.</param>
    /// <param name="CanPlayWithoutTarget">Indicates optional target selection where play can continue even without selectable targets.</param>
    public record TargetAvailabilityResult(
        bool IsAvailable,
        string? FailureReason = null,
        bool HasValidTargets = true,
        List<Guid>? ValidTargetIds = null,
        bool CanPlayWithoutTarget = false);
}
