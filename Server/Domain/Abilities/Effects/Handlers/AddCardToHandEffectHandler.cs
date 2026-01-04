using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Dtos.Enums; // Added for CardZone
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
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Factories; // Keep this for GameConstants
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of adding a specific card definition to a player's hand.
    /// </summary>
    [EffectHandler(EffectType.GetToken)]
    public class AddCardToHandEffectHandler : EffectHandlerBase<AddCardToHandEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly ICardFactory _cardFactory;

        public AddCardToHandEffectHandler(ILogger<AddCardToHandEffectHandler> logger,
                                          ITargetProcessingService targetProcessingService,
                                          ICardFactory cardFactory)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _cardFactory = cardFactory ?? throw new ArgumentNullException(nameof(cardFactory));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, AddCardToHandEffectParams parameters)
        {
            const string effectName = "AddCardToHandEffect";
            Logger.LogDebug("{EffectName}: Starting with CardDefinitionId={CardDefId}, Amount={Amount}.",
                           effectName, parameters.CardDefinitionId, parameters.Amount);

            if (parameters.CardSourceMode == Common.CardSourceMode.Instance)
            {
                var targetCards = effect.SelectedTargets
                    .Where(target => target.Card != null)
                    .Select(target => target.Card!)
                    .ToList();

                if (targetCards.Count == 0)
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = "AddCardToHand instance mode requires card targets."
                    };
                }

                var instanceResult = new GameStateActionResult { Success = true };
                foreach (var group in targetCards.GroupBy(card => card.OwnerPlayerId))
                {
                    var owner = context.CurrentGameState.GetPlayerState(group.Key);
                    if (owner == null)
                    {
                        return new GameStateActionResult
                        {
                            Success = false,
                            ErrorMessage = $"AddCardToHand instance mode owner not found for player {group.Key}."
                        };
                    }

                    instanceResult.GeneratedEvents.Add(new AddCardToHandEvent(owner, group.ToList()));
                }

                return instanceResult;
            }

            if (parameters.CardSourceMode == Common.CardSourceMode.Copy)
            {
                var targetCards = effect.SelectedTargets
                    .Where(target => target.Card != null)
                    .Select(target => target.Card!)
                    .ToList();

                if (targetCards.Count == 0)
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = "AddCardToHand copy mode requires card targets."
                    };
                }

                var copyPlayerTargets = PlayerScopeResolver.ResolvePlayers(
                    context.CurrentGameState,
                    context.RequestingPlayerId,
                    parameters.PlayerScope);

                if (copyPlayerTargets.Count == 0)
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = "AddCardToHand copy mode could not resolve player scope."
                    };
                }

                var copyResult = new GameStateActionResult { Success = true };
                foreach (var player in copyPlayerTargets)
                {
                    var copies = new List<CardInstance>();
                    foreach (var card in targetCards)
                    {
                        var copy = new CardInstance(Guid.NewGuid(), card.CurrentDefinitionId, player.PlayerId)
                        {
                            CurrentZone = CardZone.Playing
                        };
                        copies.Add(copy);
                        copyResult.GeneratedEvents.Add(new CopyCardEvent(player, card, copy));
                    }

                    if (copies.Count > 0)
                    {
                        copyResult.GeneratedEvents.Add(new AddCardToHandEvent(player, copies));
                    }
                }

                return copyResult;
            }

            if (parameters.CardDefinitionId <= 0 || parameters.Amount <= 0)
            {
                Logger.LogWarning("{EffectName}: Invalid parameters CardDefinitionId={CardDefId} or Amount={Amount}.",
                                effectName, parameters.CardDefinitionId, parameters.Amount);
                return new GameStateActionResult { Success = false, ErrorMessage = "Invalid CardDefinitionId or Amount." };
            }

            Func<TargetInfo, GameContext, DomainOperationResult> createAddCardToHandEventAction =
                (targetInfo, ctx) =>
                {
                    var result = new DomainOperationResult();
                    if (targetInfo.Player == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "AddCardToHand target is not a valid player." };
                    }

                    var player = targetInfo.Player;
                    var cardsToAdd = new List<CardInstance>();
                    for (int i = 0; i < parameters.Amount; i++)
                    {
                        var newCard = _cardFactory.CreateAndRegisterNewCard(
                            ctx.CurrentGameState,
                            parameters.CardDefinitionId,
                            player.PlayerId,
                            CardZone.Playing // Initial zone before being placed in hand
                        );
                        cardsToAdd.Add(newCard);
                    }

                    // The handler now creates the event, which will be committed by the transaction service.
                    var addCardToHandEvent = new AddCardToHandEvent(player, cardsToAdd);
                    result.GeneratedEvents.Add(addCardToHandEvent);
                    result.Success = true;

                    return result;
                };

            var playerTargets = PlayerScopeResolver.ResolvePlayerTargets(
                context.CurrentGameState,
                context.RequestingPlayerId,
                parameters.PlayerScope);

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                playerTargets,
                createAddCardToHandEventAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for add card to hand effect, or no valid targets found.";
            }

            return serviceResult;
        }

        // CreateEffectCompletedEvent method is removed as this responsibility has moved to EffectExecutor.

    }
}
