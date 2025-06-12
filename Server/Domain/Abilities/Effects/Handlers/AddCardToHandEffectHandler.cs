using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases; // Corrected: Using the base class namespace
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Exceptions;
using ShadowverseBeyond.Server.Domain.Constants; // Keep this for GameConstants

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of adding a specific card definition to a player's hand.
    /// </summary>
    public class AddCardToHandEffectHandler : EffectHandlerBase<AddCardToHandEffectParams> // Correct: Inherits from base
    {
        private readonly ICardRepository _cardRepository;
        private readonly ITargetProcessingService _targetProcessingService;

        public AddCardToHandEffectHandler(ILogger logger, ICardRepository cardRepository, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _cardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, AddCardToHandEffectParams parameters)
        {
            const string effectName = "AddCardToHandEffect";
            Logger.LogDebug("{EffectName}: Starting with CardDefinitionId={CardDefId}, Amount={Amount}.",
                           effectName, parameters.CardDefinitionId, parameters.Amount);

            // Validate card definition first
            var cardDefinition = await _cardRepository.GetCardDefinitionByIdAsync(parameters.CardDefinitionId);
            if (cardDefinition == null)
            {
                Logger.LogError("{EffectName}: CardDefinition with ID {CardDefId} not found.", effectName, parameters.CardDefinitionId);
                return new GameStateActionResult { Success = false, ErrorMessage = $"CardDefinition ID {parameters.CardDefinitionId} not found." };
            }

            // Define the action for adding cards to a player's hand
            Func<PlayerState, GameContext, Task<IndividualActionResult>> playerAction =
                async (player, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Adding {Amount} card(s) (DefId: {CardDefId}) to Player {PlayerId}'s hand.",
                                   effectName, parameters.Amount, parameters.CardDefinitionId, player.PlayerId);

                    int cardsAdded = 0;
                    bool handFull = false;
                    var individualEvents = new List<IGameEvent>();

                    for (int i = 0; i < parameters.Amount; i++)
                    {
                        if (player.Hand.Count >= GameConstants.Hand.MaxHandSize)
                        {
                            Logger.LogWarning("{EffectName}: Player {PlayerId} hand is full ({HandSize}/{MaxHandSize}). Cannot add more cards.",
                                              effectName, player.PlayerId, player.Hand.Count, GameConstants.Hand.MaxHandSize);
                            handFull = true;
                            break;
                        }

                        var newCardInstance = new CardInstance(cardDefinition.Id, player.PlayerId);
                        player.Hand.Add(newCardInstance);
                        cardsAdded++;

                        // TODO: Generate CardAddedToHandEvent when event is defined
                        // var addedEvent = new CardAddedToHandEvent(player.PlayerId, newCardInstance.InstanceId, newCardInstance.DefinitionId);
                        // individualEvents.Add(addedEvent);

                        Logger.LogTrace("{EffectName}: Added card instance {InstanceId} (DefId: {DefinitionId}) to Player {PlayerId}'s hand. Hand size: {HandCount}",
                                       effectName, newCardInstance.InstanceId, newCardInstance.DefinitionId, player.PlayerId, player.Hand.Count);
                    }

                    await Task.CompletedTask; // Adding cards is synchronous

                    string message = $"Added {cardsAdded} card(s) to hand.";
                    if (handFull && cardsAdded < parameters.Amount)
                    {
                        message += " Hand became full.";
                    }

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Player(player.PlayerId),
                        Success = true,
                        Message = message,
                        GeneratedEvents = individualEvents
                    };
                };

            // AddCardToHand effect only targets Players
            Logger.LogDebug("{EffectName}: Processing targets assuming Player type.", effectName);

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.Player, // Expect only Player targets
                playerAction,
                null, // No CardInstance action needed
                effectName);
        }
    }
}