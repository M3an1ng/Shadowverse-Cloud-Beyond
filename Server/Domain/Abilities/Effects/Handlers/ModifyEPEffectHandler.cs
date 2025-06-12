using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants; // Added for GameConstants (potentially for max EP)
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of modifying a player's current Evolution Points (EP).
    /// </summary>
    public class ModifyEPEffectHandler : EffectHandlerBase<ModifyEPEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ModifyEPEffectHandler(ILogger logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, ModifyEPEffectParams parameters)
        {
            const string effectName = "ModifyEPEffect";
            Logger.LogDebug("{EffectName}: Starting with ModifyAmount={Amount}.", effectName, parameters.ModifyAmount);

            // Define the action for modifying a player's EP
            Func<PlayerState, GameContext, Task<IndividualActionResult>> playerAction =
                async (player, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Modifying Player {PlayerId} current EP by {Amount}.",
                                   effectName, player.PlayerId, parameters.ModifyAmount);

                    int originalEP = player.CurrentEvolutionPoints;
                    // Clamp the new EP between 0 and the player's MaxEvolutionPoints
                    int newEP = Math.Max(0, Math.Min(originalEP + parameters.ModifyAmount, player.MaxEvolutionPoints));
                    player.CurrentEvolutionPoints = newEP;

                    // TODO: Generate EpUpdateEvent when event is defined
                    // var epUpdateEvent = new EpUpdateEvent(player.PlayerId, newEP, player.MaxEvolutionPoints);
                    // var individualEvents = new List<IGameEvent> { epUpdateEvent };

                    Logger.LogTrace("{EffectName}: Player {PlayerId} current EP updated from {OriginalEP} to {NewEP}/{MaxEP}.",
                                   effectName, player.PlayerId, originalEP, player.CurrentEvolutionPoints, player.MaxEvolutionPoints);

                    await Task.CompletedTask; // EP modification is synchronous

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Player(player.PlayerId),
                        Success = true,
                        Message = $"Modified current EP from {originalEP} to {newEP} (Amount: {parameters.ModifyAmount}).",
                        GeneratedEvents = new List<IGameEvent>() // TODO: Add events when implemented
                    };
                };

            // ModifyEP effect only targets Players
            Logger.LogDebug("{EffectName}: Processing targets assuming Player type.", effectName);

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.Player, // Expect only Player targets
                playerAction,
                null, // No CardInstance action needed
                effectName);
        }
    }
}