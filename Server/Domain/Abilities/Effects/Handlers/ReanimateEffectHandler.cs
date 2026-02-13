using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    [EffectHandler(EffectType.Reanimate)]
    public class ReanimateEffectHandler : EffectHandlerBase<ReanimateEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly ICardFactory _cardFactory;

        public static Random? RandomOverride { get; set; }

        public ReanimateEffectHandler(
            ILogger<ReanimateEffectHandler> logger,
            ITargetProcessingService targetProcessingService,
            ICardFactory cardFactory)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _cardFactory = cardFactory ?? throw new ArgumentNullException(nameof(cardFactory));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, ReanimateEffectParams parameters)
        {
            const string effectName = "ReanimateEffect";

            if (parameters.MaxCost < 0)
            {
                return new GameStateActionResult { Success = false, ErrorMessage = "Reanimate cost limit must be non-negative." };
            }

            Func<TargetInfo, GameContext, DomainOperationResult> playerAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != TargetType.Player || targetInfo.Player == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Reanimate effect can only be applied to players." };
                    }

                    var player = targetInfo.Player;
                    var eligibleCards = player.Graveyard
                        .Where(card => card.IsFollower
                            && card.BaseCost <= parameters.MaxCost
                            && card.LastMoveReason == CardMoveReason.Destroyed)
                        .ToList();

                    if (eligibleCards.Count == 0)
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = "No destroyed followers eligible for reanimate." };
                    }

                    var maxCost = eligibleCards.Max(card => card.BaseCost);
                    var maxCostCandidates = eligibleCards
                        .Where(card => card.BaseCost == maxCost)
                        .ToList();
                    var selectedDefinitionId = SelectWeightedDefinition(maxCostCandidates);

                    var summoned = _cardFactory.CreateAndRegisterNewCard(
                        ctx.CurrentGameState,
                        selectedDefinitionId,
                        player.PlayerId,
                        CardZone.Staging);

                    var result = new DomainOperationResult { Success = true };
                    var tribeBuffResult = ctx.BuffService.ApplyTribeBuff(
                        context: ctx,
                        target: summoned,
                        tribes: new List<string> { "亡者" },
                        durationType: DurationType.Permanent,
                        durationParameter: null,
                        effectKey: "reanimate_tribe",
                        canStack: true);
                    result.Merge(tribeBuffResult);
                    if (!tribeBuffResult.Success)
                    {
                        return tribeBuffResult;
                    }

                    var expectedPosition = player.Field.Count + 1;
                    result.GeneratedEvents.Add(new SummonEvent(player, summoned, expectedPosition));
                    return result;
                };

            var playerTargets = PlayerScopeResolver.ResolvePlayerTargets(
                context,
                parameters.PlayerScope);

            var serviceResult = _targetProcessingService.ProcessTargets(
                context,
                playerTargets,
                playerAction,
                effectName);

            if (!serviceResult.Success && string.IsNullOrEmpty(serviceResult.ErrorMessage))
            {
                serviceResult.ErrorMessage = "Failed to process targets for reanimate effect, or no valid targets found.";
            }

            return serviceResult;
        }

        private static int SelectWeightedDefinition(List<CardInstance> candidates)
        {
            if (candidates.Count == 1)
            {
                return candidates[0].BaseDefinitionId;
            }

            var grouped = candidates
                .GroupBy(card => card.BaseDefinitionId)
                .Select(group => new
                {
                    DefinitionId = group.Key,
                    Weight = Math.Max(1, group.Count())
                })
                .OrderBy(group => group.DefinitionId)
                .ToList();

            if (grouped.Count == 1)
            {
                return grouped[0].DefinitionId;
            }

            var weights = grouped.Select(g => g.Weight).ToList();
            var totalWeight = weights.Sum();
            if (totalWeight <= 0)
            {
                return grouped[0].DefinitionId;
            }

            var rng = RandomOverride ?? Random.Shared;
            var roll = rng.Next(1, totalWeight + 1);
            var cumulative = 0;

            for (var index = 0; index < grouped.Count; index++)
            {
                cumulative += weights[index];
                if (roll <= cumulative)
                {
                    return grouped[index].DefinitionId;
                }
            }

            return grouped[^1].DefinitionId;
        }
    }
}
