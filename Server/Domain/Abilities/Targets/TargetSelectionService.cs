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
                    finalResult.ErrorMessage = $"Failed to execute query for target '{target.Key}': {queryResult.ErrorMessage}";
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
                    var selectableTargets = ApplyAbilityTargetKeywordRules(candidateTargets, target.Config, context, applyChoiceRestrictions: true);

                    var minSelectionCount = Math.Max(0, target.Config.PlayerChoiceOptions.MinCount);

                    if (!selectableTargets.Any() || selectableTargets.Count < minSelectionCount)
                    {
                        if (target.Config.RequireValidTarget)
                        {
                            finalResult.Status = ResolutionStatus.Failed;
                            finalResult.ErrorMessage = selectableTargets.Count == 0
                                ? $"No valid selectable targets for ability '{ability.KeywordSummary}' (target key '{target.Key}')."
                                : $"Not enough selectable targets for ability '{ability.KeywordSummary}' (target key '{target.Key}'): requires {minSelectionCount}, but only {selectableTargets.Count} available.";
                            _logger.LogInformation(finalResult.ErrorMessage);
                            SendTargetTrace(context, ability, false, traceEntries, GetResolvedTargetCount(resolvedTargetsForQuery), finalResult.ErrorMessage);
                            return finalResult;
                        }

                        _logger.LogInformation(
                            "Selectable targets ({Available}) below minimum ({Min}) for ability {AbilityKeywords} (target key '{TargetKey}'), RequireValidTarget=false. Skipping target selection.",
                            selectableTargets.Count,
                            minSelectionCount,
                            ability.KeywordSummary,
                            target.Key);
                        target.SetResolvedTargets(new List<TargetInfo>(), triggeringEventId);
                        resolvedTargetsForQuery[target.Key] = target.ResolvedTargets;
                        continue;
                    }

                    var maxSelectionCount = Math.Min(selectableTargets.Count, target.Config.PlayerChoiceOptions.MaxCount);
                    var message = BuildPlayerChoicePrompt(minSelectionCount, maxSelectionCount);

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
                        MinSelectionCount = minSelectionCount,
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
                        finalResult.ErrorMessage = $"Player cancelled target selection for ability '{ability.KeywordSummary}'.";
                        _logger.LogInformation(finalResult.ErrorMessage);
                        SendTargetTrace(context, ability, false, traceEntries, GetResolvedTargetCount(resolvedTargetsForQuery), finalResult.ErrorMessage);
                        return finalResult;
                    }

                    // Filter the potential targets based on the player's selection.
                    var chosenTargetIdSet = new HashSet<Guid>(choiceResult.ChosenTargetIds);
                    var chosenTargets = selectableTargets
                        .Where(t => chosenTargetIdSet.Contains(t.InstanceId))
                        .ToList();

                    if (target.Config.PlayerChoiceOptions?.IsInverseSelection == true)
                    {
                        chosenTargets = candidateTargets
                            .Where(t => !chosenTargetIdSet.Contains(t.InstanceId))
                            .ToList();
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

            foreach (var target in ability.Targets)
            {
                if (!target.Config.IsEnabled)
                {
                    continue;
                }

                // Only check targets that are explicitly configured to require a valid target for the ability to be playable.
                if (!target.Config.RequireValidTarget)
                {
                    continue;
                }

                // Execute the query to find potential targets. We pass an empty dictionary for resolved targets
                // because this is a pre-check and no targets have been resolved yet.
                var queryResult = queryExecutor.ExecuteQuery(target.Config.Query, context, new Dictionary<string, List<TargetInfo>>());

                if (!queryResult.Success)
                {
                    _logger.LogWarning("Target availability check failed for ability {AbilityKeywords}, target '{TargetKey}': Query execution failed: {Error}",
                        ability.KeywordSummary, target.Key, queryResult.ErrorMessage);
                    // If the query itself fails, we consider targets unavailable.
                    return new TargetAvailabilityResult(false, $"目标查询失败: {queryResult.ErrorMessage}");
                }

                var potentialTargets = queryResult.Results.Select(item => item switch
                    {
                        CardInstance card => TargetInfo.ForCard(card),
                        _ => null
                    })
                    .Where(t => t != null)
                    .Select(t => t!)
                    .ToList();

                var applyChoiceRestrictions = target.Config.PlayerChoiceOptions?.IsRequired == true;
                potentialTargets = ApplyAbilityTargetKeywordRules(
                    potentialTargets,
                    target.Config,
                    context,
                    applyChoiceRestrictions: applyChoiceRestrictions);

                if (applyChoiceRestrictions)
                {
                    var minSelectionCount = Math.Max(0, target.Config.PlayerChoiceOptions!.MinCount);
                    if (potentialTargets.Count < minSelectionCount)
                    {
                        _logger.LogInformation("Target availability check failed for ability {AbilityKeywords}, target '{TargetKey}': requires {Min} targets but only {Available} available.",
                            ability.KeywordSummary, target.Key, minSelectionCount, potentialTargets.Count);
                        return new TargetAvailabilityResult(false, $"目标数量不足，至少需要{minSelectionCount}个目标。");
                    }
                }

                if (!potentialTargets.Any())
                {
                    _logger.LogInformation("Target availability check failed for ability {AbilityKeywords}, target '{TargetKey}': No valid targets found.",
                        ability.KeywordSummary, target.Key);
                    // If the query returns no results, and a target is required, the ability is not available.
                    return new TargetAvailabilityResult(false, "没有有效的目标。");
                }
            }

            await Task.CompletedTask;
            // If all required targets have at least one potential target, the ability is available to be played.
            return new TargetAvailabilityResult(true);
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
                    _ = hubContext.Clients.Group($"Player_{playerId}").ConditionTraceSucceeded(dto);
                }
                else
                {
                    _ = hubContext.Clients.Group($"Player_{playerId}").ConditionTraceFailed(dto);
                }
            }
        }

        private static List<TargetInfo> ApplyAbilityTargetKeywordRules(
            List<TargetInfo> potentialTargets,
            TargetConfig config,
            GameContext context,
            bool applyChoiceRestrictions)
        {
            var abilityOwnerId = context.SourceCard?.OwnerPlayerId ?? context.RequestingPlayerId;

            // Ability targeted: only enforce for opponent target selection by default.
            return potentialTargets.Where(targetInfo =>
            {
                if (targetInfo.Type != TargetType.Card || targetInfo.Card == null)
                {
                    return true;
                }

                var card = targetInfo.Card;
                if (card.OwnerPlayerId != abilityOwnerId && card.HasKeyword(CardKeyword.EarthSigil))
                {
                    return false;
                }

                if (!applyChoiceRestrictions)
                {
                    return true;
                }

                if (card.OwnerPlayerId == abilityOwnerId)
                {
                    return true;
                }

                return !card.HasKeyword(CardKeyword.Aura) && !card.HasKeyword(CardKeyword.Ambush);
            }).ToList();
        }

        private static string BuildPlayerChoicePrompt(int minCount, int maxCount)
        {
            if (maxCount <= 0)
            {
                return "请选择目标";
            }

            if (minCount <= 0)
            {
                return maxCount == 1 ? "请选择1个目标" : $"请选择最多{maxCount}个目标";
            }

            if (minCount >= maxCount)
            {
                return $"请选择{maxCount}个目标";
            }

            return $"请选择{minCount}-{maxCount}个目标";
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
    public record TargetAvailabilityResult(bool IsAvailable, string? FailureReason = null);
}
