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

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    public class TurnStartEffectHandler : EffectHandlerBase<TurnStartEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly ITargetProcessingService _targetProcessingService;

        public TurnStartEffectHandler(
            ILogger<TurnStartEffectHandler> logger,
            IMapper mapper,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, TurnStartEffectParams parameters)
        {
            const string effectName = "TurnStartEffect";
            Logger.LogDebug("{EffectName}: Starting for player {PlayerId}, turn {TurnNumber}.",
                           effectName, parameters.StartingPlayerId, parameters.TurnNumber);

            Func<PlayerState, GameContext, Task<DomainOperationResult>> turnStartAction =
                async (player, ctx) =>
                {
                    // 【架构重构】只负责调用领域实体方法，业务逻辑已移动到PlayerState.StartTurn()
                    var operationResult = player.StartTurn(ctx.CurrentGameState);
                    await Task.CompletedTask;
                    return operationResult;
                };

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                turnStartAction,
                null,
                effectName);
        }
    }

}
