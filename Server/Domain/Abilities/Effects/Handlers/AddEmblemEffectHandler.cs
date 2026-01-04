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
            Logger.LogDebug("{EffectName}: Starting with CardDefinitionId={CardDefId}, Amount={Amount}.",
                effectName, parameters.CardDefinitionId, parameters.Amount);

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

                    var cardsToAdd = group.ToList();
                    if (parameters.PreventDuplicate)
                    {
                        var existingIds = new HashSet<int>(owner.Emblem.Select(card => card.BaseDefinitionId));
                        cardsToAdd = cardsToAdd
                            .Where(card => !existingIds.Contains(card.BaseDefinitionId))
                            .ToList();
                    }

                    if (cardsToAdd.Count == 0)
                    {
                        continue;
                    }

                    instanceResult.GeneratedEvents.Add(new AddEmblemEvent(owner, cardsToAdd));
                }

                return instanceResult;
            }

            if (parameters.CardDefinitionId <= 0 || parameters.Amount <= 0)
            {
                Logger.LogWarning("{EffectName}: Invalid parameters CardDefinitionId={CardDefId} or Amount={Amount}.",
                    effectName, parameters.CardDefinitionId, parameters.Amount);
                return new GameStateActionResult { Success = false, ErrorMessage = "Invalid CardDefinitionId or Amount." };
            }

            var definition = GameDataProvider.Instance?.GetCardDefinition(parameters.CardDefinitionId);
            if (definition == null)
            {
                return new GameStateActionResult
                {
                    Success = false,
                    ErrorMessage = $"CardDefinition {parameters.CardDefinitionId} not found for AddEmblem."
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

            Func<TargetInfo, GameContext, DomainOperationResult> createAddEmblemEventAction =
                (targetInfo, ctx) =>
                {
                    var result = new DomainOperationResult();
                    if (targetInfo.Player == null)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "AddEmblem target is not a valid player." };
                    }

                    var player = targetInfo.Player;
                    if (parameters.PreventDuplicate &&
                        player.Emblem.Any(card => card.BaseDefinitionId == parameters.CardDefinitionId))
                    {
                        result.Success = true;
                        return result;
                    }

                    var cardsToAdd = new List<CardInstance>();
                    for (int i = 0; i < parameters.Amount; i++)
                    {
                        var newCard = _cardFactory.CreateAndRegisterNewCard(
                            ctx.CurrentGameState,
                            parameters.CardDefinitionId,
                            player.PlayerId,
                            CardZone.Playing
                        );
                        cardsToAdd.Add(newCard);
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
                context.CurrentGameState,
                context.RequestingPlayerId,
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
    }
}
