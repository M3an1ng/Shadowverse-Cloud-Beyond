// File: Server/Domain/Abilities/Effects/Handlers/DestroyUnitEffectHandler.cs
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For CardKeyword
using ShadowverseBeyond.Server.Dtos; // For PlayerStateDto (needed for CardDestroyedEvent)
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue; // For mapping PlayerState to PlayerStateDto
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the "DestroyUnit" effect, moving targeted units to the graveyard.
    /// </summary>
    [EffectHandler(EffectType.Destroy)]
    public class DestroyEffectHandler : EffectHandlerBase<DestroyEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly ITargetProcessingService _targetProcessingService;

        public DestroyEffectHandler(ILogger<DestroyEffectHandler> logger, IMapper mapper, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, DestroyEffectParams parameters)
        {
            const string effectName = "DestroyEffect";
            var sourceCard = context.SourceCard;

            if (sourceCard == null)
            {
                Logger.LogError("{EffectName}: SourceCard is null, cannot determine the cause of destruction.", effectName);
                return new GameStateActionResult { Success = false, ErrorMessage = "Cannot destroy card without a source." };
            }

            Func<TargetInfo, GameContext, DomainOperationResult> createDestroyEventAction =
                (targetInfo, ctx) =>
                {
                    var result = new DomainOperationResult();
                    if (targetInfo.Card == null)
                    {
                        Logger.LogWarning("{EffectName}: Target card data is missing for one of the targets.", effectName);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Destroy target is not a valid card." };
                    }

                    var targetCard = targetInfo.Card;

                    // Precondition check: Ensure the target has an owner.
                    var ownerPlayer = ctx.CurrentGameState.GetPlayerState(targetCard.OwnerPlayerId);
                    if (ownerPlayer == null)
                    {
                        Logger.LogError("{EffectName}: Could not find owner PlayerState for card {CardId}.", effectName, targetCard.InstanceId);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Owner player not found for target card." };
                    }

                    // Create the DestroyEvent command.
                    var destroyEvent = new DestroyEvent(targetCard, sourceCard, EventReasonSource.Ability);
                    result.GeneratedEvents.Add(destroyEvent);
                    result.Success = true;

                    return result;
                };

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                createDestroyEventAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for destroy effect, or no valid targets found.";
            }

            return serviceResult;
        }

        // CreateEffectCompletedEvent method is removed as this responsibility has moved to EffectExecutor.
    }
}
