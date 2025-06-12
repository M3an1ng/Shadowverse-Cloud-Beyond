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
    /// Handles the effect of modifying a player's current Play Points (PP).
    /// </summary>
    public class ModifyPPEffectHandler : EffectHandlerBase<ModifyPPEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ModifyPPEffectHandler(ILogger logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, ModifyPPEffectParams parameters)
        {
            const string effectName = "ModifyPPEffect";
            Logger.LogDebug("{EffectName}: Starting with ModifyAmount={Amount}.", effectName, parameters.ModifyAmount);

            // Define the action for modifying a player's PP
            Func<PlayerState, GameContext, Task<IndividualActionResult>> playerAction =
                async (player, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Modifying Player {PlayerId} current PP by {Amount} using domain method.",
                                   effectName, player.PlayerId, parameters.ModifyAmount);

                    int originalPP = player.CurrentPlayPoints;

                    // 使用PlayerState领域方法处理PP修改
                    var ppEvents = player.ModifyPlayPoints(parameters.ModifyAmount);
                    // TODO: 在未来版本中，这些事件应该被添加到事件队列中进行处理

                    Logger.LogTrace("{EffectName}: Player {PlayerId} current PP updated from {OriginalPP} to {NewPP}/{MaxPP} using domain method.",
                                   effectName, player.PlayerId, originalPP, player.CurrentPlayPoints, player.MaxPlayPoints);

                    await Task.CompletedTask; // PP modification is synchronous

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Player(player.PlayerId),
                        Success = true,
                        Message = $"Modified current PP from {originalPP} to {player.CurrentPlayPoints} (Amount: {parameters.ModifyAmount}) using domain method.",
                        GeneratedEvents = ppEvents // Use events from domain method
                    };
                };

            // ModifyPP effect only targets Players
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