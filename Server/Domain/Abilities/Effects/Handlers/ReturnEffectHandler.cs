using Microsoft.Extensions.Logging;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;

using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 返回效果处理器
    /// 将目标卡牌返回到指定区域（通常是手牌或牌库）
    /// </summary>
    [EffectHandler(EffectType.Return)]
    public class ReturnEffectHandler : EffectHandlerBase<ReturnEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public ReturnEffectHandler(ILogger<ReturnEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ReturnEffectParams parameters)
        {
            const string effectName = "ReturnEffect";
            Logger.LogDebug("{EffectName}: Starting with TargetZone={Zone}.", effectName, parameters.TargetZone);

            Func<TargetInfo, GameContext, DomainOperationResult> returnAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Return effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;

                    // 根据框架约束：ReturnCardToHand 会直接引起状态变化，Handler 侧应只产出事件
                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(new ReturnEvent(card, "ReturnEffect"));
                    return result;
                };

            return _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                returnAction,
                effectName);
        }
    }
}
