using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Interfaces; // Added for ICardRepository
using ShadowverseBeyond.Server.Exceptions; // Added for GameLogicException
using ShadowverseBeyond.Server.Domain.Abilities; // For AbilityManager and AbilityConfig
using ShadowverseBeyond.Server.Services; // For GameDataProvider
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of transforming a card instance into another card definition.
    /// </summary>
    [EffectHandler(EffectType.Transform)]
    public class TransformEffectHandler : EffectHandlerBase<TransformEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        public TransformEffectHandler(
            ILogger<TransformEffectHandler> logger,
            ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, TransformEffectParams parameters)
        {
            const string effectName = "TransformEffect";
            Logger.LogDebug("{EffectName}: Starting with TargetCardDefinitionId={TargetDefId}.",
                           effectName, parameters.TargetCardDefinitionId);

            var targetDefinition = GameDataProvider.Instance.GetCardDefinition(parameters.TargetCardDefinitionId);
            if (targetDefinition == null)
            {
                Logger.LogError("{EffectName}: Target CardDefinition with ID {TargetDefId} not found.",
                               effectName, parameters.TargetCardDefinitionId);
                return new GameStateActionResult { Success = false, ErrorMessage = $"Target CardDefinition ID {parameters.TargetCardDefinitionId} not found." };
            }

            Func<TargetInfo, GameContext, DomainOperationResult> transformAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Card || targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a card or card data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Transform effect can only be applied to cards." };
                    }

                    var card = targetInfo.Card;

                    // 通过唯一的 TransformEvent 表达意图，提交阶段执行实体变更
                    var evt = new TransformEvent(card, parameters.TargetCardDefinitionId);
                    var result = new DomainOperationResult { Success = true };
                    result.GeneratedEvents.Add(evt);
                    return result;
                };

            return _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                transformAction,
                effectName);
        }
    }
}