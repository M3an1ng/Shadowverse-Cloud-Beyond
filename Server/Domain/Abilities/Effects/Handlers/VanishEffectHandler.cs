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
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 消失效果处理器
    /// 使目标消失，与摧毁效果不同的是不触发死亡相关效果（如LastWords）
    /// </summary>
    [EffectHandler(EffectType.Vanish)]
    public class VanishEffectHandler : EffectHandlerBase<VanishEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public VanishEffectHandler(ILogger<VanishEffectHandler> logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, VanishEffectParams parameters)
        {
            const string effectName = "VanishEffect";
            Logger.LogDebug("{EffectName}: Starting with VanishReason={Reason}.", effectName, "Effect");

            Func<TargetInfo, GameContext, DomainOperationResult> vanishAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Vanish effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;
                    var ownerPlayer = ctx.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                    if (ownerPlayer == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "Owner player not found." };
                    }

                    var keywords = ctx.BuffService.GetEffectiveKeywords(card, ctx);
                    if (keywords.Contains(CardKeyword.VanishResist))
                    {
                        Logger.LogDebug("{EffectName}: Target {TargetId} has VanishResist; skipping vanish.", effectName, card.InstanceId);
                        return new DomainOperationResult { Success = true };
                    }

                    // 通过事件表达意图，由 CardVanishedEvent / CardMovedEvent.Commit 执行状态变更
                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(new CardVanishedEvent(card, card.CurrentZone, "Effect"));
                    return result;
                };

            return _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                vanishAction,
                effectName);
        }
    }
}
