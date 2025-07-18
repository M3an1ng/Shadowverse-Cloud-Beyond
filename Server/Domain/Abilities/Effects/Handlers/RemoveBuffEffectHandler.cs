using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;

using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 移除Buff效果处理器
    /// 从目标身上移除指定的Buff效果
    /// </summary>
    using ShadowverseBeyond.Server.Domain.Buffs;

    public class RemoveBuffEffectHandler : EffectHandlerBase<RemoveBuffEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly IBuffManager _buffManager;

        public RemoveBuffEffectHandler(ILogger<RemoveBuffEffectHandler> logger,
                                       ITargetProcessingService targetProcessingService,
                                       IBuffManager buffManager)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _buffManager = buffManager ?? throw new ArgumentNullException(nameof(buffManager));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, RemoveBuffEffectParams parameters)
        {
            const string effectName = "RemoveBuffEffect";
            Logger.LogDebug("{EffectName}: Starting with BuffName={BuffName}, RemoveCount={Count}.",
                           effectName, parameters.BuffName ?? "All", parameters.RemoveCount);

            Func<CardInstance, GameContext, Task<DomainOperationResult>> cardRemoveBuffAction =
                (card, ctx) => card.RemoveBuffsAsync(parameters, _buffManager);

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                null,
                cardRemoveBuffAction,
                effectName);
        }
    }
}
