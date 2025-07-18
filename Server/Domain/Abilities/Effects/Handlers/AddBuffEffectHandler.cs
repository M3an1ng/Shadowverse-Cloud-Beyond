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
    /// 添加Buff效果处理器
    /// 为目标添加指定的Buff效果
    /// </summary>
    using ShadowverseBeyond.Server.Domain.Buffs;

    public class AddBuffEffectHandler : EffectHandlerBase<AddBuffEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly IBuffFactory _buffFactory;
        private readonly IBuffManager _buffManager;

        public AddBuffEffectHandler(ILogger<AddBuffEffectHandler> logger,
                                    ITargetProcessingService targetProcessingService,
                                    IBuffFactory buffFactory,
                                    IBuffManager buffManager)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _buffFactory = buffFactory ?? throw new ArgumentNullException(nameof(buffFactory));
            _buffManager = buffManager ?? throw new ArgumentNullException(nameof(buffManager));
        }

        public override GameStateActionResult Apply(GameContext context, AddBuffEffectParams parameters)
        {
            const string effectName = "AddBuffEffect";
            Logger.LogDebug("{EffectName}: Starting with BuffDefinitionId={BuffId}.", effectName, parameters.BuffDefinitionId);

            if (parameters.BuffDefinitionId == Guid.Empty)
            {
                Logger.LogError("{EffectName}: BuffDefinitionId cannot be empty.", effectName);
                return new GameStateActionResult { Success = false, ErrorMessage = "BuffDefinitionId cannot be empty." };
            }

            var sourceAbility = context.CurrentAbility;
            if (sourceAbility?.SourceCard == null) // sourceAbility itself could be null if effect is not from an ability
            {
                Logger.LogError("{EffectName}: Source ability or its source card not found in context for AddBuff.", effectName);
                return new GameStateActionResult { Success = false, ErrorMessage = "Source ability or card is missing for AddBuff." };
            }

            // OwnerPlayer might not be strictly necessary here if turnNumber is passed differently or not needed by AddBuff
            // However, AddBuff currently takes turnNumber.
            var ownerPlayer = context.CurrentGameState.GetPlayerState(sourceAbility.SourceCard.OwnerPlayerId);
            if (ownerPlayer == null)
            {
                Logger.LogError("{EffectName}: Owner player not found for source card {CardId} of the ability.", effectName, sourceAbility.SourceCard.InstanceId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Owner player of ability source card not found." };
            }
            var turnNumber = ownerPlayer.TurnNumber;

            Func<CardInstance, GameContext, DomainOperationResult> cardAddBuffAction =
                (card, ctx) =>
                {
                    var buff = _buffFactory.CreateBuffFromDefinition(
                        parameters.BuffDefinitionId.ToString(),
                        card,
                        sourceAbility.SourceCard,
                        sourceAbility,
                        turnNumber
                    );

                    // Delegate the actual buff application to the domain entity.
                    // The CardInstance.AddBuff method will handle null buffs and return an appropriate DomainOperationResult.
                    if (buff == null)
                    {
                        Logger.LogWarning("{EffectName}: BuffFactory returned a null buff for definition {BuffId}.", effectName, parameters.BuffDefinitionId);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Failed to create buff from definition." };
                    }
                    return card.AddBuff(_buffManager, buff);
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                null,
                cardAddBuffAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for add buff effect, or no valid targets found.";
            }

            return serviceResult;
        }
    }
}
