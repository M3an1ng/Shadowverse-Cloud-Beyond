using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using System.Linq; // Added for OfType, FirstOrDefault, Any, Select
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue; // Added for DestroyEffectRequest
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the application of the Damage effect.
    /// </summary>
    // Inherit from EffectHandlerBase instead of IEffectHandler directly
    [EffectHandler(EffectType.Damage)]
    public class DamageEffectHandler : EffectHandlerBase<DamageEffectParams>
    {
        // Remove private _logger, use base.Logger or Logger directly
        private readonly ITargetProcessingService _targetProcessingService; // Inject the service

        // Adjust constructor signature to accept ILogger and call base(logger) correctly
        public DamageEffectHandler(ILogger<DamageEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, DamageEffectParams parameters)
        {
            const string effectName = "DamageEffect";
            Logger.LogDebug("{EffectName}: Starting with DamageAmount={Amount}.", effectName, parameters.DamageAmount);

            if (parameters.DamageAmount <= 0)
            {
                Logger.LogInformation("{EffectName}: DamageAmount is {DamageAmount}. No damage will be dealt.", effectName, parameters.DamageAmount);
                return new GameStateActionResult { Success = true };
            }

            Func<TargetInfo, GameContext, DomainOperationResult> damageAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Damage effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;
                    var operationResult = card.TakeDamage(parameters.DamageAmount, ctx.SourceCard);

                    // After dealing damage, check if the card is destroyed.
                    if (operationResult.Success && card.CurrentHealth <= 0)
                    {
                        // Find the owner of the card to call the correct DestroyCard method.
                        var ownerPlayer = ctx.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                        if (ownerPlayer != null)
                        {
                            if (ctx.SourceCard == null)
                            {
                                Logger.LogError("SourceCard is null in DamageEffectHandler when trying to destroy a card. This should not happen.");
                                // Even if source is null, we should probably still destroy the card as it has 0 health.
                                // However, we cannot call DestroyCard without a source. This indicates a deeper issue.
                                // For now, we will fail gracefully for this target.
                                operationResult.Success = false;
                                operationResult.ErrorMessage = "Could not determine damage source for destruction.";
                            }
                            else
                            {
                                var destroyResult = ownerPlayer.DestroyCard(card, ctx.SourceCard, "DestroyedByDamage");
                                // Merge the events from the destroy operation.
                                operationResult.GeneratedEvents.AddRange(destroyResult.GeneratedEvents);
                            }
                        }
                        else
                        {
                            // Log an error if the owner can't be found, which would be a critical state issue.
                            Logger.LogError("Could not find owner player with ID {PlayerId} for card {CardId} to process destruction.", card.OwnerPlayerId, card.InstanceId);
                        }
                    }
                    return operationResult;
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                damageAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for damage effect, or no valid targets found.";
            }

            return serviceResult;
        }

        // CreateEffectCompletedEvent method is removed as this responsibility has moved to EffectExecutor.
    }
}