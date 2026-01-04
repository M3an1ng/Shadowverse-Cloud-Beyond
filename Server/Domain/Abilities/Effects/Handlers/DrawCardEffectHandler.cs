using System;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 【最终重构】处理“DrawCard”效果。
    /// 遵循标准架构，使用 ITargetProcessingService 对每个目标玩家执行抽牌操作。
    /// </summary>
    [EffectHandler(EffectType.Draw)]
    public class DrawCardEffectHandler : EffectHandlerBase<DrawCardEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public DrawCardEffectHandler(
            ILogger<DrawCardEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, DrawCardEffectParams parameters)
        {
            const string effectName = "DrawCardEffect";
            Logger.LogDebug("{EffectName}: Starting with DrawAmount={Amount}.", effectName, parameters.DrawAmount);

            if (parameters.DrawAmount <= 0)
            {
                Logger.LogWarning("{EffectName}: DrawAmount is {DrawAmount}, no cards will be drawn.", effectName, parameters.DrawAmount);
                return new GameStateActionResult { Success = false, ErrorMessage = "DrawAmount must be positive." };
            }

            Func<TargetInfo, GameContext, DomainOperationResult> drawCardAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Player || targetInfo.Player == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "DrawCard effect can only be applied to players." };
                    }

                    var player = targetInfo.Player;
                    // 由实体方法生成 DrawEvent，固定本次抽牌的卡牌实例ID（遵循两方法模式）
                    var op = player.DrawCards(parameters.DrawAmount);
                    return op;
                };

            var playerTargets = PlayerScopeResolver.ResolvePlayerTargets(
                context.CurrentGameState,
                context.RequestingPlayerId,
                parameters.PlayerScope);

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                playerTargets,
                drawCardAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for draw card effect, or no valid targets found.";
            }

            return serviceResult;
        }

        // CreateEffectCompletedEvent method is removed as this responsibility has moved to EffectExecutor.
    }
}
