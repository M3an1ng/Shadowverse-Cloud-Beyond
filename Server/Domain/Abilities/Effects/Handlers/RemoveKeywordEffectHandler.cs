// File: Server/Domain/Abilities/Effects/Handlers/RemoveKeywordEffectHandler.cs
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For CardKeyword
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For IMapper
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq; // For Any()

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    public class RemoveKeywordEffectHandler : EffectHandlerBase<RemoveKeywordEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public RemoveKeywordEffectHandler(ILogger<RemoveKeywordEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, RemoveKeywordEffectParams parameters)
        {
            const string effectName = "RemoveKeywordEffect";
            Logger.LogDebug("{EffectName}: Starting with Keyword={Keyword}.", effectName, parameters.Keyword);

            Func<CardInstance, GameContext, Task<DomainOperationResult>> cardAction =
                async (card, ctx) =>
                {
                    // Delegate the core logic to the domain entity
                    var operationResult = card.RemoveKeyword(parameters.Keyword);
                    await Task.CompletedTask;
                    return operationResult;
                };

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                null,
                cardAction,
                effectName);
        }
    }
}