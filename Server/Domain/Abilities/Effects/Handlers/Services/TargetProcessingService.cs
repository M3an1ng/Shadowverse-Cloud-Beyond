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
            Func<PlayerState, GameContext, DomainOperationResult>? playerAction,
            Func<CardInstance, GameContext, DomainOperationResult>? cardAction,
            string effectName)
        {
            if (context.SelectedTargets == null)
            {
                _logger.LogWarning("{EffectName}: SelectedTargets is null in the current context.", effectName);
                return new GameStateActionResult { Success = false, ErrorMessage = "No targets selected." };
            }

            var allResults = new List<DomainOperationResult>();
            var allGeneratedEvents = new List<IGameEvent>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            foreach (var targetInfo in context.SelectedTargets)
            {
                _logger.LogTrace("{EffectName}: Processing target: Type={TargetType}, ID={TargetId}",
                                 effectName, targetInfo.Type, targetInfo.Id);
                DomainOperationResult domainResult;

                try
                {
                    if (targetInfo.Type == Common.TargetType.Player && playerAction != null)
                    {
                        if (targetInfo.Player != null)
                        {
                            domainResult = playerAction(targetInfo.Player, context);
                        }
                        else
                        {
                            _logger.LogWarning("{EffectName}: TargetInfo of type Player has a null Player object. ID: {PlayerId}", effectName, targetInfo.Id);
                            domainResult = new DomainOperationResult { Success = false, ErrorMessage = "Target player data is missing." };
                        }
                    }
                    else if (targetInfo.Type == Common.TargetType.Card && cardAction != null)
                    {
                        if (targetInfo.Card != null)
                        {
                            domainResult = cardAction(targetInfo.Card, context);
                        }
                        else
                        {
                            _logger.LogWarning("{EffectName}: TargetInfo of type Card has a null Card object. ID: {CardId}", effectName, targetInfo.Id);
                            domainResult = new DomainOperationResult { Success = false, ErrorMessage = "Target card data is missing." };
                        }
                    }
                    else
                    {
                        _logger.LogWarning("{EffectName}: No appropriate action provided for target type {TargetType} or action was null.", effectName, targetInfo.Type);
                        domainResult = new DomainOperationResult { Success = true };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{EffectName}: Exception occurred while processing target {TargetId} ({TargetType}).", effectName, targetInfo.Id, targetInfo.Type);
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
                               effectName, context.SelectedTargets.Count, overallSuccess);

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