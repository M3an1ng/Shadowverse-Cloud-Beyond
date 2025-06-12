using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants; // Added for GameConstants
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of modifying a player's maximum Play Points (PP).
    /// This is often used for effects like "Gain an empty Play Point orb".
    /// </summary>
    public class ModifyPPMaxEffectHandler : EffectHandlerBase<ModifyPPMaxEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ModifyPPMaxEffectHandler(ILogger logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, ModifyPPMaxEffectParams parameters)
        {
            const string effectName = "ModifyPPMaxEffect";
            Logger.LogDebug("{EffectName}: Starting with ModifyAmount={Amount}.", effectName, parameters.ModifyAmount);

            // Define the action for modifying a player's max PP
            Func<PlayerState, GameContext, Task<IndividualActionResult>> playerAction =
                async (player, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Modifying Player {PlayerId} max PP by {Amount}.",
                                   effectName, player.PlayerId, parameters.ModifyAmount);

                    int originalMaxPP = player.MaxPlayPoints;
                    // Clamp the new Max PP between 0 and the game's absolute maximum
                    int newMaxPP = Math.Max(0, Math.Min(originalMaxPP + parameters.ModifyAmount, GameConstants.PlayPoints.MaxPlayPoints));

                    if (newMaxPP == originalMaxPP)
                    {
                        Logger.LogInformation("{EffectName}: Player {PlayerId} max PP unchanged at {MaxPP}.",
                                             effectName, player.PlayerId, originalMaxPP);

                        await Task.CompletedTask;
                        return new IndividualActionResult
                        {
                            TargetId = TargetId.Player(player.PlayerId),
                            Success = true,
                            Message = $"Max PP unchanged at {originalMaxPP}.",
                            GeneratedEvents = new List<IGameEvent>()
                        };
                    }

                    player.MaxPlayPoints = newMaxPP;

                    // TODO: Generate PpUpdateEvent when event is defined
                    // var ppUpdateEvent = new PpUpdateEvent(player.PlayerId, player.CurrentPlayPoints, newMaxPP);
                    // var individualEvents = new List<IGameEvent> { ppUpdateEvent };

                    Logger.LogTrace("{EffectName}: Player {PlayerId} max PP updated from {OriginalMaxPP} to {NewMaxPP}.",
                                   effectName, player.PlayerId, originalMaxPP, player.MaxPlayPoints);

                    await Task.CompletedTask; // Max PP modification is synchronous

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Player(player.PlayerId),
                        Success = true,
                        Message = $"Modified max PP from {originalMaxPP} to {newMaxPP} (Amount: {parameters.ModifyAmount}).",
                        GeneratedEvents = new List<IGameEvent>() // TODO: Add events when implemented
                    };
                };

            // ModifyPPMax effect only targets Players
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