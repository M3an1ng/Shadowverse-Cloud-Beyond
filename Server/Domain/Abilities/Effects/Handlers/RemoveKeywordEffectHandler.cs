// File: Server/Domain/Abilities/Effects/Handlers/RemoveKeywordEffectHandler.cs
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For CardKeyword
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For IMapper
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq; // For Any()

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the "RemoveKeyword" effect, generating events to indicate keyword removals from target cards.
    /// </summary>
    public class RemoveKeywordEffectHandler : EffectHandlerBase<RemoveKeywordEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly ITargetProcessingService _targetProcessingService;

        public RemoveKeywordEffectHandler(ILogger logger, IMapper mapper, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, RemoveKeywordEffectParams parameters)
        {
            const string effectName = "RemoveKeywordEffect";
            Logger.LogDebug("{EffectName}: Starting with Keyword={Keyword}.", effectName, parameters.Keyword);

            // Define the action for removing keyword from a CardInstance
            Func<CardInstance, GameContext, Task<IndividualActionResult>> cardAction =
                async (card, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Processing card {InstanceId} to remove keyword '{Keyword}'.",
                                   effectName, card.InstanceId, parameters.Keyword);

                    // Check if the card actually has the keyword to remove
                    if (!card.Keywords.Contains(parameters.Keyword))
                    {
                        Logger.LogInformation("{EffectName}: Target card {InstanceId} does not have keyword '{Keyword}'. Skipping removal.",
                                             effectName, card.InstanceId, parameters.Keyword);

                        await Task.CompletedTask;
                        return new IndividualActionResult
                        {
                            TargetId = TargetId.Card(card.InstanceId),
                            Success = true,
                            Message = $"Card does not have keyword '{parameters.Keyword}'.",
                            GeneratedEvents = new List<IGameEvent>()
                        };
                    }

                    // Get the source player state from the context
                    var sourcePlayerState = ctx.RequestingPlayer;

                    // Generate KeywordRemovedEvent
                    var keywordEvent = new KeywordRemovedEvent(card, parameters.Keyword, sourcePlayerState, _mapper);
                    var individualEvents = new List<IGameEvent> { keywordEvent };

                    Logger.LogInformation("{EffectName}: Generated KeywordRemovedEvent for card {InstanceId} (Owner: {OwnerId}) to remove keyword '{Keyword}' by Player {SourcePlayerId}.",
                                         effectName, card.InstanceId, card.OwnerPlayerId, parameters.Keyword, sourcePlayerState.PlayerId);

                    await Task.CompletedTask; // Keyword removal is synchronous

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Card(card.InstanceId),
                        Success = true,
                        Message = $"Removed keyword '{parameters.Keyword}' from card.",
                        GeneratedEvents = individualEvents
                    };
                };

            // RemoveKeyword effect only targets CardInstances
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