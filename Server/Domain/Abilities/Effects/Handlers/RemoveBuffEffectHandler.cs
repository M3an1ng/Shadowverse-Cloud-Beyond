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
    using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
    using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
    using ShadowverseBeyond.Server.Domain.Effects;

    [EffectHandler(EffectType.RemoveBuff)]
    public class RemoveBuffEffectHandler : EffectHandlerBase<RemoveBuffEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly IBuffManager _buffManager;
        private readonly IBuffService _buffService;

        public RemoveBuffEffectHandler(ILogger<RemoveBuffEffectHandler> logger,
                                       ITargetProcessingService targetProcessingService,
                                       IBuffManager buffManager,
                                       IBuffService buffService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _buffManager = buffManager ?? throw new ArgumentNullException(nameof(buffManager));
            _buffService = buffService ?? throw new ArgumentNullException(nameof(buffService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, RemoveBuffEffectParams parameters)
        {
            const string effectName = "RemoveBuffEffect";
            Logger.LogDebug("{EffectName}: Starting with BuffName={BuffName}, RemoveCount={Count}.",
                           effectName, parameters.BuffName ?? "All", parameters.RemoveCount);

            Func<TargetInfo, GameContext, DomainOperationResult> removeBuffAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "RemoveBuff effect can only be applied to cards." };
                    }

                    var buffsMatchingPredicate = _buffManager.GetBuffsForCard(targetInfo.Card.InstanceId)
                                            .Where(b => string.IsNullOrEmpty(parameters.BuffName) || b.DefinitionId == parameters.BuffName)
                                            .ToList();

                    var buffsToActuallyRemove = parameters.RemoveCount > 0
                        ? buffsMatchingPredicate.Take(parameters.RemoveCount).ToList()
                        : buffsMatchingPredicate;

                    if (!buffsToActuallyRemove.Any())
                    {
                        return new DomainOperationResult { Success = true }; // No buffs to remove
                    }

                    var specificBuffIds = new HashSet<Guid>(buffsToActuallyRemove.Select(b => b.Id));
                    Func<Buff, bool> finalPredicate = b => specificBuffIds.Contains(b.Id);

                    return _buffService.RemoveAllBuffs(targetInfo.Card.InstanceId, ctx, finalPredicate);
                };

            return _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                removeBuffAction,
                effectName);
        }
    }
}
