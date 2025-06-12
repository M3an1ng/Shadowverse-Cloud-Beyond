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
    /// 返回效果处理器
    /// 将目标卡牌返回到指定区域（通常是手牌或牌库）
    /// </summary>
    public class ReturnEffectHandler : EffectHandlerBase<ReturnEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly ITargetProcessingService _targetProcessingService;

        public ReturnEffectHandler(ILogger logger, IMapper mapper, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, ReturnEffectParams parameters)
        {
            const string effectName = "ReturnEffect";
            Logger.LogDebug("{EffectName}: Starting with TargetZone={Zone}, Reason={Reason}.",
                           effectName, parameters.TargetZone, parameters.ReturnReason);

            // Define the action for returning a CardInstance
            Func<CardInstance, GameContext, Task<IndividualActionResult>> cardReturnAction =
                async (card, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Attempting to return target {InstanceId} (DefId: {DefId}) to {Zone}. Reason: {Reason}",
                                   effectName, card.InstanceId, card.DefinitionId, parameters.TargetZone, parameters.ReturnReason);

                    // 1. Validate current zone (can only return cards from field typically)
                    if (card.CurrentZone != CardZone.Field)
                    {
                        Logger.LogWarning("{EffectName}: Target {InstanceId} is not on field (current zone: {CurrentZone}). Cannot return.",
                                         effectName, card.InstanceId, card.CurrentZone);

                        await Task.CompletedTask;
                        return new IndividualActionResult
                        {
                            TargetId = TargetId.Card(card.InstanceId),
                            Success = false,
                            Message = $"Target not on field (current zone: {card.CurrentZone}).",
                            GeneratedEvents = new List<IGameEvent>()
                        };
                    }

                    // 2. Validate target zone
                    if (parameters.TargetZone == CardZone.Field)
                    {
                        Logger.LogWarning("{EffectName}: Cannot return card to field. Invalid target zone.", effectName);

                        await Task.CompletedTask;
                        return new IndividualActionResult
                        {
                            TargetId = TargetId.Card(card.InstanceId),
                            Success = false,
                            Message = "Cannot return card to field.",
                            GeneratedEvents = new List<IGameEvent>()
                        };
                    }

                    // 3. Check space in target zone (for hand)
                    var ownerPlayerState = ctx.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                    if (ownerPlayerState == null)
                    {
                        Logger.LogError("{EffectName}: Could not find owner PlayerState (ID: {OwnerId}) for target {InstanceId}.",
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

                    if (parameters.TargetZone == CardZone.Hand && ownerPlayerState.Hand.Count >= GameConstants.Hand.MaxHandSize)
                    {
                        Logger.LogWarning("{EffectName}: Owner's hand is full ({HandSize}/{MaxHandSize}). Cannot return to hand.",
                                         effectName, ownerPlayerState.Hand.Count, GameConstants.Hand.MaxHandSize);

                        await Task.CompletedTask;
                        return new IndividualActionResult
                        {
                            TargetId = TargetId.Card(card.InstanceId),
                            Success = false,
                            Message = "Owner's hand is full.",
                            GeneratedEvents = new List<IGameEvent>()
                        };
                    }

                    // 4. Generate CardReturnedEvent
                    // TODO: Create CardReturnedEvent when event is defined
                    // For now, create a generic event or use existing events
                    var ownerPlayerDto = _mapper.Map<PlayerStateDto>(ownerPlayerState);

                    // TODO: Replace with proper CardReturnedEvent
                    // var returnEvent = new CardReturnedEvent(ownerPlayerDto, card, parameters.TargetZone, parameters.ReturnReason);
                    var individualEvents = new List<IGameEvent>(); // Placeholder until event is created

                    Logger.LogInformation("{EffectName}: Successfully generated return event for target {InstanceId} to {Zone}.",
                                         effectName, card.InstanceId, parameters.TargetZone);

                    await Task.CompletedTask; // Return event generation is synchronous

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Card(card.InstanceId),
                        Success = true,
                        Message = $"Generated return event to {parameters.TargetZone} (Reason: {parameters.ReturnReason}).",
                        GeneratedEvents = individualEvents
                    };
                };

            // Return effect only targets CardInstances
            Logger.LogDebug("{EffectName}: Processing targets assuming CardInstance type.", effectName);

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.CardInstance, // Expect only CardInstance targets
                null, // No PlayerState action needed
                cardReturnAction,
                effectName);
        }
    }
}
