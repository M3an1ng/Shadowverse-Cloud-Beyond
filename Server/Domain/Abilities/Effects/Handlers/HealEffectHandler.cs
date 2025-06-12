using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetId, TargetIdType
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters; // For HealEffectParams
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services; // For ITargetProcessingService
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases; // Added for EffectHandlerBase
using ShadowverseBeyond.Server.Domain.Abilities.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the application of the Heal effect.
    /// </summary>
    public class HealEffectHandler : EffectHandlerBase<HealEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public HealEffectHandler(ILogger logger, ITargetProcessingService targetProcessingService)
            : base(logger) // Call base constructor with ILogger
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        /// <summary>
        /// Applies healing to the selected targets based on the provided parameters.
        /// Heal can potentially target both Players and CardInstances.
        /// </summary>
        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, HealEffectParams parameters)
        {
            const string effectName = "HealEffect";
            Logger.LogDebug("Executing {EffectName} with HealAmount: {Amount} for source card {SourceCardId}",
                             effectName, parameters.HealAmount, context.SourceCard?.InstanceId);

            // Define the action for healing a CardInstance
            Func<CardInstance, GameContext, Task<IndividualActionResult>> cardHealAction =
                async (card, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Applying heal {HealAmount} to CardInstance {InstanceId}",
                                     effectName, parameters.HealAmount, card.InstanceId);

                    // Call the existing Heal method on CardInstance
                    var generatedEvents = card.Heal(parameters.HealAmount, ctx.SourceCard);

                    await Task.CompletedTask; // Placeholder if Heal isn't async

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Card(card.InstanceId),
                        Success = true,
                        Message = $"Restored {parameters.HealAmount} health.",
                        GeneratedEvents = generatedEvents // Include the generated events
                    };
                };

            // Heal effect only targets CardInstances (including Leaders)
            Logger.LogDebug("{EffectName}: Processing targets assuming CardInstance type.", effectName);

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.CardInstance, // Expect only CardInstance targets
                null, // No PlayerState action needed
                cardHealAction,
                effectName);
        }
    }
}