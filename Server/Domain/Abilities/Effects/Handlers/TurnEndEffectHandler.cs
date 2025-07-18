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

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    public class TurnEndEffectHandler : EffectHandlerBase<TurnEndEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly ITargetProcessingService _targetProcessingService;

        public TurnEndEffectHandler(
            ILogger<TurnEndEffectHandler> logger,
            IMapper mapper,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, TurnEndEffectParams parameters)
        {
            const string effectName = "TurnEndEffect";
            Logger.LogDebug("{EffectName}: Starting for player {PlayerId}.", effectName, parameters.EndingPlayerId);

            Func<PlayerState, GameContext, Task<DomainOperationResult>> turnEndAction =
                async (player, ctx) =>
                {
                    // Delegate the core logic to the domain entity
                    var operationResult = player.EndTurn();
                    await Task.CompletedTask;
                    return operationResult;
                };

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                turnEndAction,
                null,
                effectName);
        }
    }

}
