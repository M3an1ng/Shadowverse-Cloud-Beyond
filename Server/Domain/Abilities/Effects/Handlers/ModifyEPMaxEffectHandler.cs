using System;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of modifying a player's maximum Evolution Points (EP).
    /// </summary>
    [EffectHandler(EffectType.SetEPMax)]
    public class ModifyEPMaxEffectHandler : EffectHandlerBase<ModifyEPMaxEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ModifyEPMaxEffectHandler(
            ILogger<ModifyEPMaxEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue.Effect effect, GameContext context, ModifyEPMaxEffectParams parameters)
        {
            const string effectName = "ModifyEPMaxEffect";
            Logger.LogDebug("{EffectName}: Starting with ModifyAmount={Amount}.", effectName, parameters.ModifyAmount);

            Func<TargetInfo, GameContext, DomainOperationResult> modifyEpMaxAction = (targetInfo, ctx) =>
            {
                if (targetInfo.Type != Common.TargetType.Player || targetInfo.Player == null)
                {
                    Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                    return new DomainOperationResult { Success = false, ErrorMessage = "ModifyEPMax effect can only be applied to players." };
                }

                var player = targetInfo.Player;
                var targetMax = Math.Max(0, player.MaxEvolutionPoints + parameters.ModifyAmount);
                var evt = new EpMaxUpdateEvent(player, targetMax);
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
                modifyEpMaxAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for modify max EP effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
