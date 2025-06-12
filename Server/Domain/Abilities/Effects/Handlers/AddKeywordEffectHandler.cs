// File: Server/Domain/Abilities/Effects/Handlers/AddKeywordEffectHandler.cs
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // Add using for IMapper
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the "AddKeyword" effect, generating events to indicate keyword additions to target cards.
    /// </summary>
    public class AddKeywordEffectHandler : EffectHandlerBase<AddKeywordEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly ITargetProcessingService _targetProcessingService;

        public AddKeywordEffectHandler(ILogger logger, IMapper mapper, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, AddKeywordEffectParams parameters)
        {
            const string effectName = "AddKeywordEffect";
            Logger.LogDebug("{EffectName}: Starting with Keyword={Keyword}.", effectName, parameters.Keyword);

            // Define the action for adding keyword to a CardInstance
            Func<CardInstance, GameContext, Task<IndividualActionResult>> cardAction =
                async (card, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Processing card {InstanceId} to add keyword '{Keyword}'.",
                                   effectName, card.InstanceId, parameters.Keyword);

                    // Check if the card already has the keyword
                    if (card.Keywords.Contains(parameters.Keyword))
                    {
                        Logger.LogInformation("{EffectName}: Target card {InstanceId} already has keyword '{Keyword}'. Skipping.",
                                             effectName, card.InstanceId, parameters.Keyword);

                        await Task.CompletedTask;
                        return new IndividualActionResult
                        {
                            TargetId = TargetId.Card(card.InstanceId),
                            Success = true,
                            Message = $"Card already has keyword '{parameters.Keyword}'.",
                            GeneratedEvents = new List<IGameEvent>()
                        };
                    }

                    // Get the source player state from the context
                    var sourcePlayerState = ctx.RequestingPlayer;

                    // Generate KeywordAddedEvent
                    var keywordEvent = new KeywordAddedEvent(card, parameters.Keyword, sourcePlayerState, _mapper);
                    var individualEvents = new List<IGameEvent> { keywordEvent };

                    Logger.LogInformation("{EffectName}: Generated KeywordAddedEvent for card {InstanceId} (Owner: {OwnerId}) to add keyword '{Keyword}' by Player {SourcePlayerId}.",
                                         effectName, card.InstanceId, card.OwnerPlayerId, parameters.Keyword, sourcePlayerState.PlayerId);

                    await Task.CompletedTask; // Keyword addition is synchronous

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Card(card.InstanceId),
                        Success = true,
                        Message = $"Added keyword '{parameters.Keyword}' to card.",
                        GeneratedEvents = individualEvents
                    };
                };

            // AddKeyword effect only targets CardInstances
            Logger.LogDebug("{EffectName}: Processing targets assuming CardInstance type.", effectName);

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.CardInstance, // Expect only CardInstance targets
                null, // No PlayerState action needed
                cardAction,
                effectName);
        }
    }
}