using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of adding a specific card definition to a player's deck.
    /// </summary>
    [EffectHandler(EffectType.AddDeck)]
    public class AddDeckEffectHandler : EffectHandlerBase<AddDeckEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly ICardFactory _cardFactory;

        public AddDeckEffectHandler(
            ILogger<AddDeckEffectHandler> logger,
            ITargetProcessingService targetProcessingService,
            ICardFactory cardFactory)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _cardFactory = cardFactory ?? throw new ArgumentNullException(nameof(cardFactory));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, AddDeckEffectParams parameters)
        {
            const string effectName = "AddDeckEffect";
            var cardDefinitionIds = parameters.CardDefinitionId?.Where(id => id > 0).ToList() ?? new List<int>();
            var cardIdText = cardDefinitionIds.Count > 0 ? string.Join(",", cardDefinitionIds) : "[]";
            Logger.LogDebug("{EffectName}: Starting with CardDefinitionIds={CardDefIds}, Amount={Amount}, InsertMode={InsertMode}.",
                effectName, cardIdText, parameters.Amount, parameters.InsertMode);

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
                        ErrorMessage = "AddDeck instance mode requires card targets."
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
                            ErrorMessage = $"AddDeck instance mode owner not found for player {group.Key}."
                        };
                    }

                    instanceResult.GeneratedEvents.Add(new AddCardToDeckEvent(owner, group.ToList(), parameters.InsertMode));
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
                        ErrorMessage = "AddDeck copy mode requires card targets."
                    };
                }

                var copyPlayerTargets = PlayerScopeResolver.ResolvePlayers(
                    context,
                    parameters.PlayerScope);

                if (copyPlayerTargets.Count == 0)
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = "AddDeck copy mode could not resolve player scope."
                    };
                }

                var copyResult = new GameStateActionResult { Success = true };
                foreach (var player in copyPlayerTargets)
                {
                    var copies = new List<CardInstance>();
                    foreach (var card in targetCards)
                    {
                        for (int i = 0; i < parameters.Amount; i++)
                        {
                            var copy = new CardInstance(Guid.NewGuid(), card.CurrentDefinitionId, player.PlayerId)
                            {
                                CurrentZone = CardZone.Staging
                            };
                            copies.Add(copy);
                            copyResult.GeneratedEvents.Add(new CopyCardEvent(player, card, copy));
                        }
                    }

                    if (copies.Count > 0)
                    {
                        copyResult.GeneratedEvents.Add(new AddCardToDeckEvent(player, copies, parameters.InsertMode));
                    }
                }

                return copyResult;
            }

            if (cardDefinitionIds.Count == 0 || parameters.Amount <= 0)
            {
                Logger.LogWarning("{EffectName}: Invalid parameters CardDefinitionIds={CardDefIds} or Amount={Amount}.",
                    effectName, cardIdText, parameters.Amount);
                return new GameStateActionResult { Success = false, ErrorMessage = "Invalid CardDefinitionId list or Amount." };
            }

            Func<TargetInfo, GameContext, DomainOperationResult> createAddCardToDeckEventAction =
                (targetInfo, ctx) =>
                {
                    var result = new DomainOperationResult();
                    if (targetInfo.Player == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "AddDeck target is not a valid player." };
                    }

                    var player = targetInfo.Player;
                    var cardsToAdd = new List<CardInstance>();
                    foreach (var cardDefinitionId in cardDefinitionIds)
                    {
                        for (int i = 0; i < parameters.Amount; i++)
                        {
                            var newCard = _cardFactory.CreateAndRegisterNewCard(
                                ctx.CurrentGameState,
                                cardDefinitionId,
                                player.PlayerId,
                                CardZone.Staging
                            );
                            cardsToAdd.Add(newCard);
                        }
                    }

                    var addCardToDeckEvent = new AddCardToDeckEvent(player, cardsToAdd, parameters.InsertMode);
                    result.GeneratedEvents.Add(addCardToDeckEvent);
                    result.Success = true;

                    return result;
                };

            var playerTargets = PlayerScopeResolver.ResolvePlayerTargets(
                context,
                parameters.PlayerScope);

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                playerTargets,
                createAddCardToDeckEventAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for add deck effect, or no valid targets found.";
            }

            return serviceResult;
        }

    }
}
