using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    /// <summary>
    /// A top-level committable event representing a player's request to activate a card's Fusion ability.
    /// This event validates the request and emits a FusionEvent on success.
    /// </summary>
    [GameEventMetadata(
        "融合请求",
        "当玩家请求发动融合能力时触发的事件。",
        EventType.FusionRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public sealed class FusionRequestedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("玩家ID", "发起融合请求的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("融合动作", "融合动作的详细信息。")]
        public FusionActionDto ActionDto { get; }

        public FusionRequestedEvent(Guid playerId, FusionActionDto actionDto, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.FusionRequested, eventId, timestamp)
        {
            PlayerId = playerId;
            ActionDto = actionDto;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (context.CurrentGameState.CurrentPlayerId != PlayerId)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "It's not your turn to use Fusion." };
            }

            if (context.CurrentGameState.Phase != GamePhase.Main)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Fusion can only be used during Main phase." };
            }

            var card = context.CurrentGameState.FindCardInstance(ActionDto.CardUid);
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Card {ActionDto.CardUid} not found." };
            }

            if (card.OwnerPlayerId != PlayerId)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "You cannot use Fusion on an opponent's card." };
            }

            if (card.CurrentZone != CardZone.Hand)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Fusion can only be used while the card is in hand." };
            }

            var fusionAbilities = context.AbilityManager
                .GetEffectiveAbilitiesForCard(card.InstanceId, context)
                .Where(a => a.HasKeyword(AbilityKeyword.Fusion))
                .ToList();

            Ability? selectedAbility = null;
            if (ActionDto.AbilityRuntimeId.HasValue && ActionDto.AbilityRuntimeId.Value != Guid.Empty)
            {
                var ability = context.AbilityManager.GetAbilityByRuntimeId(ActionDto.AbilityRuntimeId.Value) as Ability;
                if (ability == null)
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "Selected Fusion ability not found." };
                }

                var sourceCardId = ability.SourceCardId ?? ability.SourceCard?.InstanceId;
                if (sourceCardId != card.InstanceId)
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "Selected Fusion ability does not belong to this card." };
                }

                if (!ability.HasKeyword(AbilityKeyword.Fusion))
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "Selected ability is not a Fusion ability." };
                }

                if (context.AbilityManager.IsAbilitySuppressed(ability, context))
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "Selected Fusion ability is suppressed." };
                }

                selectedAbility = ability;
            }

            if (!fusionAbilities.Any() && selectedAbility == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "This card has no Fusion ability." };
            }

            var abilityConfigs = selectedAbility != null
                ? new List<AbilityConfig> { selectedAbility.Config }
                : fusionAbilities
                    .OfType<Ability>()
                    .Select(a => a.Config)
                    .ToList();

            var evaluationContext = context with { SourceCard = card };
            var conditionEvaluator = context.ServiceProvider.GetRequiredService<IConditionEvaluator>();
            foreach (var config in abilityConfigs)
            {
                foreach (var condition in config.PreConditions)
                {
                    if (!conditionEvaluator.Evaluate(condition, evaluationContext))
                    {
                        return new DomainOperationResult { Success = false, ErrorMessage = $"Fusion precondition failed: {condition.Expression}" };
                    }
                }
            }

            var fusedCardUids = ActionDto.FusedCardUids ?? new List<Guid>();
            if (fusedCardUids.Count == 0)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Fusion requires at least one material card." };
            }

            var distinctFusedIds = fusedCardUids.Where(id => id != Guid.Empty).Distinct().ToList();
            if (distinctFusedIds.Count != fusedCardUids.Count)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Fusion materials contain duplicates." };
            }

            if (distinctFusedIds.Contains(card.InstanceId))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Fusion materials cannot include the fusion card itself." };
            }

            var fusedCards = new List<CardInstance>();
            foreach (var fusedId in distinctFusedIds)
            {
                var fusedCard = context.CurrentGameState.FindCardInstance(fusedId);
                if (fusedCard == null)
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = $"Fusion material {fusedId} not found." };
                }

                if (fusedCard.OwnerPlayerId != PlayerId)
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = $"Fusion material {fusedId} is not owned by requesting player." };
                }

                if (fusedCard.CurrentZone != CardZone.Hand)
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = $"Fusion material {fusedId} is not in hand." };
                }

                fusedCards.Add(fusedCard);
            }

            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.Add(new FusionEvent(card, fusedCards, selectedAbility?.RuntimeId));
            return result;
        }
    }
}
