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
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For mapping PlayerState to PlayerStateDto

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the "DestroyUnit" effect, moving targeted units to the graveyard.
    /// </summary>
    public class DestroyEffectHandler : EffectHandlerBase<DestroyUnitEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly ITargetProcessingService _targetProcessingService;

        public DestroyEffectHandler(ILogger logger, IMapper mapper, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, DestroyUnitEffectParams parameters)
        {
            const string effectName = "DestroyUnitEffect";
            Logger.LogDebug("{EffectName}: Starting with DestroyReason={Reason}.", effectName, parameters.DestroyReason);

            // Define the action for destroying a CardInstance
            Func<CardInstance, GameContext, Task<IndividualActionResult>> cardDestroyAction =
                async (card, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Attempting to destroy target {InstanceId} (DefId: {DefId}) owned by {OwnerId}. Reason: {Reason}",
                                   effectName, card.InstanceId, card.DefinitionId, card.OwnerPlayerId, parameters.DestroyReason);

                    // 1. Check for Destruction Immunity/Resistance
                    // TODO: Implement check for DestroyResist keyword and potential buff-based immunity
                    // For now, assume no resistance. Example check:
                    // if (card.HasKeyword(CardKeyword.DestroyResist) && !parameters.IgnoreDestroyResist)
                    // {
                    //     Logger.LogInformation("{EffectName}: Target {InstanceId} has DestroyResist and effect does not ignore it. Skipping destruction.", effectName, card.InstanceId);
                    //     return new IndividualActionResult { TargetId = TargetId.Card(card.InstanceId), Success = true, Message = "Target has DestroyResist.", GeneratedEvents = new List<IGameEvent>() };
                    // }

                    // 2. Check if already destroyed or not on field (shouldn't happen with proper targeting, but good safeguard)
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

                    // 3. Generate CardDestroyedEvent
                    var ownerPlayerState = ctx.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                    if (ownerPlayerState == null)
                    {
                        Logger.LogError("{EffectName}: Could not find owner PlayerState (ID: {OwnerId}) for target {InstanceId}. Cannot generate CardDestroyedEvent.",
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

                    var ownerPlayerDto = _mapper.Map<PlayerStateDto>(ownerPlayerState);
                    var destroyedEvent = new CardDestroyedEvent(ownerPlayerDto, card);
                    var individualEvents = new List<IGameEvent> { destroyedEvent };

                    Logger.LogInformation("{EffectName}: Successfully generated CardDestroyedEvent for target {InstanceId}.", effectName, card.InstanceId);

                    await Task.CompletedTask; // Destruction event generation is synchronous

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Card(card.InstanceId),
                        Success = true,
                        Message = $"Generated destruction event (Reason: {parameters.DestroyReason}).",
                        GeneratedEvents = individualEvents
                    };
                };

            // DestroyUnit effect only targets CardInstances
            Logger.LogDebug("{EffectName}: Processing targets assuming CardInstance type.", effectName);

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.CardInstance, // Expect only CardInstance targets
                null, // No PlayerState action needed
                cardDestroyAction,
                effectName);
        }
    }
}