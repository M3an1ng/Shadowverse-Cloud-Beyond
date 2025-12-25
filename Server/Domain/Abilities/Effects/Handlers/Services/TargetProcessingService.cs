using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetId, TargetIdType
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For GameStateActionResult, IndividualActionResult
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services
{
    /// <summary>
    /// Implements the service responsible for processing effect targets.
    /// </summary>
    public class TargetProcessingService : ITargetProcessingService
    {
        private readonly ILogger<TargetProcessingService> _logger;

        public TargetProcessingService(ILogger<TargetProcessingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Applies an effect action to a list of targets, enforcing the expected target type.
        /// </summary>
        public GameStateActionResult ProcessTargets(
            GameContext context,
            IReadOnlyList<TargetInfo> targets,
            Func<TargetInfo, GameContext, DomainOperationResult> action,
            string effectName)
        {
            if (targets == null)
            {
                _logger.LogWarning("{EffectName}: Targets list is null.", effectName);
                return new GameStateActionResult { Success = false, ErrorMessage = "No targets provided." };
            }

            var allResults = new List<DomainOperationResult>();
            var allGeneratedEvents = new List<IGameEvent>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            foreach (var targetInfo in targets)
            {
                _logger.LogTrace("{EffectName}: Processing target: Type={TargetType}, ID={TargetId}",
                                 effectName, targetInfo.Type, targetInfo.InstanceId);
                DomainOperationResult domainResult;

                try
                {
                    // Directly invoke the unified action
                    domainResult = action(targetInfo, context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{EffectName}: Exception occurred while processing target {TargetId} ({TargetType}).", effectName, targetInfo.InstanceId, targetInfo.Type);
                    domainResult = new DomainOperationResult { Success = false, ErrorMessage = $"Exception: {ex.Message}" };
                }

                allResults.Add(domainResult);
                if (!domainResult.Success)
                {
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = domainResult.ErrorMessage ?? "An error occurred.";
                }

                if (domainResult.GeneratedEvents != null && domainResult.GeneratedEvents.Any())
                {
                    allGeneratedEvents.AddRange(domainResult.GeneratedEvents);
                }
            }

            _logger.LogInformation("{EffectName} completed processing {TargetCount} targets. Overall Success: {Success}",
                               effectName, targets.Count, overallSuccess);

            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty,
                Results = allResults,
                GeneratedEvents = allGeneratedEvents
            };
        }
    }
}