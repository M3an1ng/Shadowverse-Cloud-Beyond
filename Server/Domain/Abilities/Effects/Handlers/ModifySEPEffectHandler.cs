using System;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of modifying a player's current Super Evolution Points (SEP).
    /// </summary>
    [EffectHandler(EffectType.SetSEP)]
    public class ModifySEPEffectHandler : EffectHandlerBase<ModifySEPEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ModifySEPEffectHandler(ILogger<ModifySEPEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ModifySEPEffectParams parameters)
        {
            const string effectName = "ModifySEPEffect";
            Logger.LogDebug("{EffectName}: Starting with ModifyAmount={Amount}.", effectName, parameters.ModifyAmount);

            Func<TargetInfo, GameContext, DomainOperationResult> modifySepAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != TargetType.Player || targetInfo.Player == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "ModifySEP effect can only be applied to players." };
                    }

                    var player = targetInfo.Player;
                    var newCurrent = Math.Max(0, Math.Min(player.CurrentSuperEvolutionPoints + parameters.ModifyAmount, player.MaxSuperEvolutionPoints));
                    var evt = new SepUpdateEvent(player, newCurrent);
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
                modifySepAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for modify SEP effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
