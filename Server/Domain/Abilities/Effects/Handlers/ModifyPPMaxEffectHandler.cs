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
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of modifying a player's maximum Play Points (PP).
    /// This is often used for effects like "Gain an empty Play Point orb".
    /// </summary>
    [EffectHandler(EffectType.Ramp)]
    public class ModifyPPMaxEffectHandler : EffectHandlerBase<ModifyPPMaxEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ModifyPPMaxEffectHandler(ILogger<ModifyPPMaxEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ModifyPPMaxEffectParams parameters)
        {
            const string effectName = "ModifyPPMaxEffect";
            Logger.LogDebug("{EffectName}: Starting with ModifyAmount={Amount}.", effectName, parameters.ModifyAmount);

            Func<TargetInfo, GameContext, DomainOperationResult> modifyPPMaxAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Player || targetInfo.Player == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "ModifyPPMax effect can only be applied to players." };
                    }

                    var player = targetInfo.Player;
                    // 仅更新上限：由 PpMaxUpdateEvent.Commit 执行上限变更（内部会在必要时下修当前值）
                    var targetMax = Math.Max(0, Math.Min(player.MaxPlayPoints + parameters.ModifyAmount, GameConstants.PlayPoints.MaxPlayPoints));
                    var evt = new PpMaxUpdateEvent(player, targetMax);
                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(evt);
                    return result;
                };

            var playerTargets = PlayerScopeResolver.ResolvePlayerTargets(
                context,
                parameters.PlayerScope);

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                playerTargets,
                modifyPPMaxAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for modify max PP effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
