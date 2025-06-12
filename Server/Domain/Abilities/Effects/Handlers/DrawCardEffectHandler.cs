// File: Server/Domain/Abilities/Effects/Handlers/DrawCardEffectHandler.cs
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the "DrawCard" effect, generating events to indicate card draws for target players.
    /// </summary>
    public class DrawCardEffectHandler : EffectHandlerBase<DrawCardEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;

        public DrawCardEffectHandler(ILogger logger, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, DrawCardEffectParams parameters)
        {
            const string effectName = "DrawCardEffect";
            Logger.LogDebug("{EffectName}: Starting with DrawAmount={Amount}.", effectName, parameters.DrawAmount);

            // Validate draw amount
            if (parameters.DrawAmount <= 0)
            {
                Logger.LogWarning("{EffectName}: DrawAmount is {DrawAmount}, no cards will be drawn.", effectName, parameters.DrawAmount);
                return new GameStateActionResult { Success = false, ErrorMessage = "DrawAmount must be positive." };
            }

            // Define the action for drawing cards for a PlayerState
            Func<PlayerState, GameContext, Task<IndividualActionResult>> playerDrawAction =
                async (player, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Drawing {DrawAmount} cards for Player {PlayerId} using domain method.",
                                   effectName, parameters.DrawAmount, player.PlayerId);

                    var individualEvents = new List<IGameEvent>();

                    // 使用PlayerState领域方法直接执行抽卡
                    var drawnCards = player.DrawCards(parameters.DrawAmount);

                    // 生成抽卡完成事件（如果需要的话）
                    if (drawnCards.Count > 0)
                    {
                        // TODO: 可以在这里生成具体的抽卡事件，比如CardDrawnEvent
                        // 目前暂时跳过事件生成，因为领域方法已经完成了状态修改
                        Logger.LogDebug("{EffectName}: Successfully drew {ActualDrawCount} cards for Player {PlayerId}.",
                                       effectName, drawnCards.Count, player.PlayerId);
                    }

                    Logger.LogInformation("{EffectName}: Drew {ActualDrawCount} cards for Player {PlayerId} using domain method.",
                                         effectName, drawnCards.Count, player.PlayerId);

                    await Task.CompletedTask; // Draw logic is synchronous

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Player(player.PlayerId),
                        Success = true,
                        Message = $"Drew {drawnCards.Count} card(s) using domain method.",
                        GeneratedEvents = individualEvents
                    };
                };

            // DrawCard effect can target Players or CardInstances (targeting card owner)
            // For now, assume it targets Players directly
            Logger.LogDebug("{EffectName}: Processing targets assuming Player type.", effectName);

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.Player, // Expect Player targets
                playerDrawAction,
                null, // No CardInstance action needed
                effectName);
        }
    }
}