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

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 消失效果处理器
    /// 使目标消失，与摧毁效果不同的是不触发死亡相关效果（如LastWords）
    /// </summary>
    public class VanishEffectHandler : EffectHandlerBase<VanishEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly ITargetProcessingService _targetProcessingService;

        public VanishEffectHandler(ILogger logger, IMapper mapper, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, VanishEffectParams parameters)
        {
            const string effectName = "VanishEffect";
            Logger.LogDebug("{EffectName}: Starting with VanishReason={Reason}.", effectName, parameters.VanishReason);

            // Define the action for vanishing a CardInstance
            Func<CardInstance, GameContext, Task<IndividualActionResult>> cardVanishAction =
                async (card, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Attempting to vanish target {InstanceId} (DefId: {DefId}) owned by {OwnerId}. Reason: {Reason}",
                                   effectName, card.InstanceId, card.DefinitionId, card.OwnerPlayerId, parameters.VanishReason);

                    // 1. Check for Vanish Immunity/Resistance
                    // TODO: Implement check for VanishResist keyword and potential buff-based immunity
                    // For now, assume no resistance. Example check:
                    // if (card.HasKeyword(CardKeyword.VanishResist) && !parameters.IgnoreVanishResist)
                    // {
                    //     Logger.LogInformation("{EffectName}: Target {InstanceId} has VanishResist and effect does not ignore it. Skipping vanish.", effectName, card.InstanceId);
                    //     return new IndividualActionResult { TargetId = TargetId.Card(card.InstanceId), Success = true, Message = "Target has VanishResist.", GeneratedEvents = new List<IGameEvent>() };
                    // }

                    // 2. Check if already vanished or not on field
                    if (card.CurrentZone != CardZone.Field || card.CurrentHealth <= 0)
                    {
                        Logger.LogWarning("{EffectName}: Target {InstanceId} is not on field or already destroyed. Skipping.", effectName, card.InstanceId);

                        await Task.CompletedTask;
                        return new IndividualActionResult
                        {
                            TargetId = TargetId.Card(card.InstanceId),
                            Success = true,
                            Message = "Target not on field or already destroyed.",
                            GeneratedEvents = new List<IGameEvent>()
                        };
                    }

                    // 3. Generate CardVanishedEvent (different from CardDestroyedEvent)
                    var ownerPlayerState = ctx.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                    if (ownerPlayerState == null)
                    {
                        Logger.LogError("{EffectName}: Could not find owner PlayerState (ID: {OwnerId}) for target {InstanceId}. Cannot generate CardVanishedEvent.",
                                       effectName, card.OwnerPlayerId, card.InstanceId);

                        await Task.CompletedTask;
                        return new IndividualActionResult
                        {
                            TargetId = TargetId.Card(card.InstanceId),
                            Success = false,
                            Message = "Could not find owner PlayerState.",
                            GeneratedEvents = new List<IGameEvent>()
                        };
                    }

                    // TODO: Create CardVanishedEvent when event is defined
                    // For now, use CardDestroyedEvent but with a flag indicating it's a vanish
                    var ownerPlayerDto = _mapper.Map<PlayerStateDto>(ownerPlayerState);
                    var vanishEvent = new CardDestroyedEvent(ownerPlayerDto, card); // TODO: Replace with CardVanishedEvent
                    var individualEvents = new List<IGameEvent> { vanishEvent };

                    Logger.LogInformation("{EffectName}: Successfully generated vanish event for target {InstanceId}.", effectName, card.InstanceId);

                    await Task.CompletedTask; // Vanish event generation is synchronous

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Card(card.InstanceId),
                        Success = true,
                        Message = $"Generated vanish event (Reason: {parameters.VanishReason}).",
                        GeneratedEvents = individualEvents
                    };
                };

            // Vanish effect only targets CardInstances
            Logger.LogDebug("{EffectName}: Processing targets assuming CardInstance type.", effectName);

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.CardInstance, // Expect only CardInstance targets
                null, // No PlayerState action needed
                cardVanishAction,
                effectName);
        }
    }
}
