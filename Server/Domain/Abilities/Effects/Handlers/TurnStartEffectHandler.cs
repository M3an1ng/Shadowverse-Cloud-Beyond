
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Exceptions;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.TurnStart)]
    public class TurnStartEffectHandler : EffectHandlerBase<TurnStartEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public TurnStartEffectHandler(
            ILogger<TurnStartEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, TurnStartEffectParams parameters)
        {
            const string effectName = "TurnStartEffect";
            Logger.LogDebug("{EffectName}: Starting for player {PlayerId}.",
                           effectName, parameters.StartingPlayerId);

            Func<TargetInfo, GameContext, DomainOperationResult> turnStartAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Player || targetInfo.Player == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "TurnStart effect can only be applied to players." };
                    }

                    var result = new DomainOperationResult();
                    var evt = new TurnStartEvent(targetInfo.Player);
                    result.GeneratedEvents.Add(evt);
                    result.Success = true;
                    return result;
                };

            return _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                turnStartAction,
                effectName);
        }
    }

}
