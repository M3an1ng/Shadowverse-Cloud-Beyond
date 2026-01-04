using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.TurnEnd)]
    public class TurnEndEffectHandler : EffectHandlerBase<TurnEndEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public TurnEndEffectHandler(
            ILogger<TurnEndEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, TurnEndEffectParams parameters)
        {
            const string effectName = "TurnEndEffect";
            Logger.LogDebug("{EffectName}: Starting for player scope {PlayerScope}.", effectName, parameters.PlayerScope);

            Func<TargetInfo, GameContext, DomainOperationResult> turnEndAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Player || targetInfo.Player == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "TurnEnd effect can only be applied to players." };
                    }

                    var result = new DomainOperationResult();
                    var evt = new TurnEndEvent(targetInfo.Player);
                    result.GeneratedEvents.Add(evt);
                    result.Success = true;
                    return result;
                };

            var playerTargets = PlayerScopeResolver.ResolvePlayerTargets(
                context.CurrentGameState,
                context.RequestingPlayerId,
                parameters.PlayerScope);

            return _targetProcessingService.ProcessTargets(
                context,
                playerTargets,
                turnEndAction,
                effectName);
        }
    }

}
