using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Constants; // Added for GameConstants
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of modifying a player's current Play Points (PP).
    /// </summary>
    [EffectHandler(EffectType.SetPP)]
    public class ModifyPPEffectHandler : EffectHandlerBase<ModifyPPEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ModifyPPEffectHandler(ILogger<ModifyPPEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ModifyPPEffectParams parameters)
        {
            const string effectName = "ModifyPPEffect";
            Logger.LogDebug("{EffectName}: Starting with ModifyAmount={Amount}.", effectName, parameters.ModifyAmount);

            Func<TargetInfo, GameContext, DomainOperationResult> playerAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Player || targetInfo.Player == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "ModifyPP effect can only be applied to players." };
                    }

                    var player = targetInfo.Player;
                    // 仅更新当前值：由 PpCurrentUpdateEvent.Commit 执行状态变更
                    var newCurrent = Math.Max(0, Math.Min(player.CurrentPlayPoints + parameters.ModifyAmount, player.MaxPlayPoints));
                    var evt = new PpUpdateEvent(player, newCurrent);
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
                playerAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for modify PP effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
