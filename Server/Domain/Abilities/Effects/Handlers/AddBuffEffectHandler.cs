using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 添加Buff效果处理器
    /// 为目标添加指定的Buff效果
    /// </summary>
    [EffectHandler(EffectType.AddBuff)]
    public class AddBuffEffectHandler : EffectHandlerBase<AddBuffEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly IBuffFactory _buffFactory;
        private readonly IBuffService _buffService;

        public AddBuffEffectHandler(ILogger<AddBuffEffectHandler> logger,
                                    ITargetProcessingService targetProcessingService,
                                    IBuffFactory buffFactory,
                                    IBuffService buffService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _buffFactory = buffFactory ?? throw new ArgumentNullException(nameof(buffFactory));
            _buffService = buffService ?? throw new ArgumentNullException(nameof(buffService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, AddBuffEffectParams parameters)
        {
            const string effectName = "AddBuffEffect";
            Logger.LogDebug("{EffectName}: Starting with BuffDefinitionId={BuffId}.", effectName, parameters.BuffDefinitionId);

            if (parameters.BuffDefinitionId == Guid.Empty)
            {
                Logger.LogError("{EffectName}: BuffDefinitionId cannot be empty.", effectName);
                return new GameStateActionResult { Success = false, ErrorMessage = "BuffDefinitionId cannot be empty." };
            }

            var sourceAbility = context.GetCurrentAbility();
            if (sourceAbility?.SourceCard == null)
            {
                Logger.LogError("{EffectName}: Source ability or its source card not found in context for AddBuff.", effectName);
                return new GameStateActionResult { Success = false, ErrorMessage = "Source ability or card is missing for AddBuff." };
            }

            var ownerPlayer = context.CurrentGameState.GetPlayerState(sourceAbility.SourceCard.OwnerPlayerId);
            if (ownerPlayer == null)
            {
                Logger.LogError("{EffectName}: Owner player not found for source card {CardId} of the ability.", effectName, sourceAbility.SourceCardId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Owner player of ability source card not found." };
            }
            var turnNumber = ownerPlayer.TurnNumber;

            Func<TargetInfo, GameContext, DomainOperationResult> createAddBuffEventAction =
                (targetInfo, ctx) =>
                {
                    var result = new DomainOperationResult();
                    if (targetInfo.Card == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "AddBuff target is not a valid card." };
                    }

                    var card = targetInfo.Card;
                    var buff = _buffFactory.CreateBuffFromDefinition(
                        parameters.BuffDefinitionId.ToString(),
                        card,
                        sourceAbility.SourceCard,
                        sourceAbility,
                        turnNumber
                    );

                    if (buff == null)
                    {
                        Logger.LogWarning("{EffectName}: BuffFactory returned a null buff for definition {BuffId}.", effectName, parameters.BuffDefinitionId);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Failed to create buff from definition." };
                    }

                    if (string.IsNullOrWhiteSpace(buff.EffectKey) && buff.SourceAbilityId != Guid.Empty)
                    {
                        buff.EffectKey = BuildEffectKey(effect, buff.SourceAbilityId);
                    }

                    return _buffService.ApplyBuffProcess(buff, ctx, parameters.CanStack);
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                createAddBuffEventAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for add buff effect, or no valid targets found.";
            }

            return serviceResult;
        }

        private static string BuildEffectKey(Effect effect, Guid sourceAbilityId)
        {
            return $"{sourceAbilityId:N}:{effect.EffectIndex}";
        }
    }
}
