using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetId, TargetIdType
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For GameStateActionResult, IndividualActionResult

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
        public async Task<GameStateActionResult> ProcessTargetsAsync(
            GameContext context,
            TargetIdType expectedTargetType,
            Func<PlayerState, GameContext, Task<IndividualActionResult>>? playerAction,
            Func<CardInstance, GameContext, Task<IndividualActionResult>>? cardAction,
            string effectName)
        {
            // --- This is the core logic moved from the temporary internal helper ---
            if (context.CurrentGameState == null || context.SelectedTargets == null)
            {
                _logger.LogWarning("{EffectName}: Missing required context information (CurrentGameState or SelectedTargets).", effectName);
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            var gameState = context.CurrentGameState;
            var allResults = new List<IndividualActionResult>();
            var allGeneratedEvents = new List<IGameEvent>(); // Changed type to List<IGameEvent>
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            foreach (var target in context.SelectedTargets)
            {
                _logger.LogTrace("{EffectName}: Processing target: Type={ActualType}, Expected={ExpectedType}, ID={TargetId}",
                                 effectName, target.Type, expectedTargetType, target.Id);
                IndividualActionResult individualResult;

                if (target.Type != expectedTargetType)
                {
                    _logger.LogWarning("{EffectName}: Received target of type {ActualType} but expected {ExpectedType}. Target ID: {TargetId}",
                                       effectName, target.Type, expectedTargetType, target.Id);
                    individualResult = new IndividualActionResult { TargetId = target, Success = false, Message = $"Effect expected target type {expectedTargetType} but received {target.Type}." };
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = individualResult.Message;
                    allResults.Add(individualResult);
                    continue;
                }

                try
                {
                    if (expectedTargetType == TargetIdType.Player)
                    {
                        if (playerAction != null)
                        {
                            // Use the existing GetPlayerState method
                            PlayerState? targetPlayer = gameState.GetPlayerState(target.Id); // Corrected method name
                            if (targetPlayer != null)
                            {
                                individualResult = await playerAction(targetPlayer, context);
                            }
                            else
                            {
                                _logger.LogWarning("{EffectName}: Target Player with ID {PlayerId} not found.", effectName, target.Id);
                                individualResult = new IndividualActionResult { TargetId = target, Success = false, Message = "Target player not found." };
                            }
                        }
                        else
                        {
                            _logger.LogError("{EffectName}: Expected Player target type but no playerAction was provided.", effectName);
                            individualResult = new IndividualActionResult { TargetId = target, Success = false, Message = "Internal error: Player action not implemented." };
                        }
                    }
                    else // ExpectedType is CardInstance
                    {
                        if (cardAction != null)
                        {
                            // Assuming GameContext has FindCardInstance
                            var targetInstance = context.FindCardInstance(target.Id);
                            if (targetInstance != null)
                            {
                                individualResult = await cardAction(targetInstance, context);
                            }
                            else
                            {
                                _logger.LogWarning("{EffectName}: Target CardInstance with ID {InstanceId} not found.", effectName, target.Id);
                                individualResult = new IndividualActionResult { TargetId = target, Success = false, Message = "Target card instance not found." };
                            }
                        }
                        else
                        {
                            _logger.LogError("{EffectName}: Expected CardInstance target type but no cardAction was provided.", effectName);
                            individualResult = new IndividualActionResult { TargetId = target, Success = false, Message = "Internal error: Card action not implemented." };
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{EffectName}: Exception occurred while processing target {TargetId} ({TargetType}).", effectName, target.Id, target.Type);
                    individualResult = new IndividualActionResult { TargetId = target, Success = false, Message = $"Exception: {ex.Message}" };
                }

                allResults.Add(individualResult);
                if (!individualResult.Success)
                {
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = individualResult.Message ?? "An error occurred.";
                }
                // TODO: Aggregate events if IndividualActionResult contains them.
                // If IndividualActionResult.GeneratedEvents is List<IGameEvent>, this would work:
                // if (individualResult.GeneratedEvents != null) allGeneratedEvents.AddRange(individualResult.GeneratedEvents);
                // If IndividualActionResult.GeneratedEvents is List<object>, we'd need:
                // if (individualResult.GeneratedEvents != null) allGeneratedEvents.AddRange(individualResult.GeneratedEvents.OfType<IGameEvent>());
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
            // --- End of moved logic ---
        }
    }
}