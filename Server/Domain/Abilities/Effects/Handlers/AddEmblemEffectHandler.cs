using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the effect of adding crest cards to a player's emblem zone.
    /// </summary>
    [EffectHandler(EffectType.AddEmblem)]
    public class AddEmblemEffectHandler : EffectHandlerBase<AddEmblemEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly ICardFactory _cardFactory;

        public AddEmblemEffectHandler(
            ILogger<AddEmblemEffectHandler> logger,
            ITargetProcessingService targetProcessingService,
            ICardFactory cardFactory)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _cardFactory = cardFactory ?? throw new ArgumentNullException(nameof(cardFactory));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, AddEmblemEffectParams parameters)
        {
            const string effectName = "AddEmblemEffect";
            var cardDefinitionIds = parameters.CardDefinitionId?.Where(id => id > 0).ToList() ?? new List<int>();
            var cardIdText = cardDefinitionIds.Count > 0 ? string.Join(",", cardDefinitionIds) : "[]";
            Logger.LogDebug("{EffectName}: Starting with CardDefinitionIds={CardDefIds}, Amount={Amount}.",
                effectName, cardIdText, parameters.Amount);

            if (parameters.CardSourceMode == CardSourceMode.Instance)
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
                        ErrorMessage = "AddEmblem instance mode requires card targets."
                    };
                }

                if (targetCards.Any(card => card.CardType != CardType.Crest))
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = "AddEmblem can only accept crest cards."
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
                            ErrorMessage = $"AddEmblem instance mode owner not found for player {group.Key}."
                        };
                    }

                    var cardsToAdd = FilterEmblemAdditions(owner, group, parameters.PreventDuplicate);

                    if (cardsToAdd.Count == 0)
                    {
                        continue;
                    }

                    instanceResult.GeneratedEvents.Add(new AddEmblemEvent(owner, cardsToAdd));
                }

                return instanceResult;
            }

            if (parameters.CardSourceMode == CardSourceMode.Copy)
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
                        ErrorMessage = "AddEmblem copy mode requires card targets."
                    };
                }

                if (targetCards.Any(card => card.CardType != CardType.Crest))
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = "AddEmblem can only accept crest cards."
                    };
                }

                var copyResult = new GameStateActionResult { Success = true };
                foreach (var group in targetCards.GroupBy(card => card.OwnerPlayerId))
                {
                    var owner = context.CurrentGameState.GetPlayerState(group.Key);
                    if (owner == null)
                    {
                        return new GameStateActionResult
                        {
                            Success = false,
                            ErrorMessage = $"AddEmblem copy mode owner not found for player {group.Key}."
                        };
                    }

                    var availableSlots = GetAvailableEmblemSlots(owner);
                    if (availableSlots <= 0)
                    {
                        continue;
                    }

                    HashSet<int>? existingIds = null;
                    if (parameters.PreventDuplicate)
                    {
                        existingIds = new HashSet<int>(owner.Emblem.Select(card => card.BaseDefinitionId));
                    }

                    var copies = new List<CardInstance>();
                    foreach (var card in group)
                    {
                        if (availableSlots <= 0)
                        {
                            break;
                        }

                        if (parameters.PreventDuplicate)
                        {
                            var definitionId = card.BaseDefinitionId;
                            if (existingIds!.Contains(definitionId))
                            {
                                continue;
                            }
                            existingIds.Add(definitionId);
                        }

                        var copiesToCreate = Math.Max(0, parameters.Amount);
                        if (parameters.PreventDuplicate)
                        {
                            copiesToCreate = Math.Min(copiesToCreate, 1);
                        }

                        for (int i = 0; i < copiesToCreate; i++)
                        {
                            if (availableSlots <= 0)
                            {
                                break;
                            }

                            var copy = new CardInstance(Guid.NewGuid(), card.CurrentDefinitionId, owner.PlayerId)
                            {
                                CurrentZone = CardZone.Staging
                            };
                            copyResult.GeneratedEvents.Add(new CopyCardEvent(owner, card, copy));
                            copies.Add(copy);
                            availableSlots -= 1;
                        }
                    }

                    if (copies.Count > 0)
                    {
                        copyResult.GeneratedEvents.Add(new AddEmblemEvent(owner, copies));
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

            foreach (var cardDefinitionId in cardDefinitionIds)
            {
                var definition = GameDataProvider.Instance?.GetCardDefinition(cardDefinitionId);
                if (definition == null)
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = $"CardDefinition {cardDefinitionId} not found for AddEmblem."
                    };
                }

                if (definition.Type != CardType.Crest)
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = "AddEmblem can only accept crest cards."
                    };
                }
            }

            Func<TargetInfo, GameContext, DomainOperationResult> createAddEmblemEventAction =
                (targetInfo, ctx) =>
                {
                    var result = new DomainOperationResult();
                    if (targetInfo.Player == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "AddEmblem target is not a valid player." };
                    }

                    var player = targetInfo.Player;
                    var availableSlots = GetAvailableEmblemSlots(player);
                    if (availableSlots <= 0)
                    {
                        result.Success = true;
                        return result;
                    }

                    var cardsToAdd = new List<CardInstance>();
                    foreach (var cardDefinitionId in cardDefinitionIds)
                    {
                        if (availableSlots <= 0)
                        {
                            break;
                        }

                        if (parameters.PreventDuplicate &&
                            player.Emblem.Any(card => card.BaseDefinitionId == cardDefinitionId))
                        {
                            continue;
                        }

                        var maxToAdd = Math.Min(parameters.Amount, availableSlots);
                        if (parameters.PreventDuplicate)
                        {
                            maxToAdd = Math.Min(maxToAdd, 1);
                        }

                        for (int i = 0; i < maxToAdd; i++)
                        {
                            var newCard = _cardFactory.CreateAndRegisterNewCard(
                                ctx.CurrentGameState,
                                cardDefinitionId,
                                player.PlayerId,
                                CardZone.Staging
                            );
                            cardsToAdd.Add(newCard);
                            availableSlots -= 1;
                            if (availableSlots <= 0)
                            {
                                break;
                            }
                        }
                    }

                    if (cardsToAdd.Count > 0)
                    {
                        var addEmblemEvent = new AddEmblemEvent(player, cardsToAdd);
                        result.GeneratedEvents.Add(addEmblemEvent);
                    }

                    result.Success = true;
                    return result;
                };

            var playerTargets = PlayerScopeResolver.ResolvePlayerTargets(
                context,
                parameters.PlayerScope);

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                playerTargets,
                createAddEmblemEventAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for add emblem effect, or no valid targets found.";
            }

            return serviceResult;
        }

        private static int GetAvailableEmblemSlots(PlayerState owner)
        {
            return Math.Max(0, GameConstants.Emblem.MaxEmblemSize - owner.Emblem.Count);
        }

        private static List<CardInstance> FilterEmblemAdditions(
            PlayerState owner,
            IEnumerable<CardInstance> candidates,
            bool preventDuplicate)
        {
            var availableSlots = GetAvailableEmblemSlots(owner);
            if (availableSlots <= 0)
            {
                return new List<CardInstance>();
            }

            HashSet<int>? existingIds = null;
            if (preventDuplicate)
            {
                existingIds = new HashSet<int>(owner.Emblem.Select(card => card.BaseDefinitionId));
            }

            var selected = new List<CardInstance>();
            foreach (var card in candidates)
            {
                if (availableSlots <= 0)
                {
                    break;
                }

                if (preventDuplicate)
                {
                    if (existingIds!.Contains(card.BaseDefinitionId))
                    {
                        continue;
                    }
                    existingIds.Add(card.BaseDefinitionId);
                }

                selected.Add(card);
                availableSlots -= 1;
            }

            return selected;
        }
    }
}
