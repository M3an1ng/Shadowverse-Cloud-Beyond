using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    /// <summary>
    /// A top-level committable event representing a player's request to activate a card's Act ability.
    /// </summary>
    [GameEventMetadata(
        "启动请求",
        "当玩家请求发动启动能力时触发的事件。",
        EventType.ActRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public sealed class ActRequestedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("玩家ID", "发起启动请求的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("启动动作", "启动动作的详细信息。")]
        public ActActionDto ActionDto { get; }

        public ActRequestedEvent(Guid playerId, ActActionDto actionDto, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.ActRequested, eventId, timestamp)
        {
            PlayerId = playerId;
            ActionDto = actionDto;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();

            if (context.CurrentGameState.CurrentPlayerId != PlayerId)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "It's not your turn to use Act." };
            }

            if (context.CurrentGameState.Phase != GamePhase.Main)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Act can only be used during Main phase." };
            }

            var card = context.CurrentGameState.FindCardInstance(ActionDto.CardUid);
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Card {ActionDto.CardUid} not found." };
            }

            if (card.OwnerPlayerId != PlayerId)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "You cannot use Act on an opponent's card." };
            }

            if (card.CurrentZone != CardZone.Field)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Act can only be used while the card is on the field." };
            }

            var abilities = context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId, context)
                ?? new List<IAbility>();
            var hasActKeywordAbility = abilities
                .Any(a => a.AbilityKeywords?.Any(k => k.Keyword == AbilityKeyword.Act) == true);

            Ability? selectedAbility = null;
            if (ActionDto.AbilityRuntimeId.HasValue && ActionDto.AbilityRuntimeId.Value != Guid.Empty)
            {
                var ability = context.AbilityManager.GetAbilityByRuntimeId(ActionDto.AbilityRuntimeId.Value) as Ability;
                if (ability == null)
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "Selected Act ability not found." };
                }

                var sourceCardId = ability.SourceCardId ?? ability.SourceCard?.InstanceId;
                if (sourceCardId != card.InstanceId)
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "Selected Act ability does not belong to this card." };
                }

                if (!ability.HasKeyword(AbilityKeyword.Act))
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "Selected ability is not an Act ability." };
                }

                if (context.AbilityManager.IsAbilitySuppressed(ability, context))
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = "Selected Act ability is suppressed." };
                }

                selectedAbility = ability;
            }

            if (!hasActKeywordAbility && selectedAbility == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "This card has no Act ability." };
            }

            var owner = context.CurrentGameState.GetPlayerState(PlayerId);
            if (owner == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Player state not found for {PlayerId}." };
            }

            if (card.HasUsedActThisTurn())
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "This card has already used Act this turn." };
            }

            var paidCost = selectedAbility != null
                ? card.GetActCostForAbility(selectedAbility, context)
                : card.GetActCost(context);

            // Trigger actual activated abilities (data-driven via TriggerConfig listening to EventType.Act).
            result.GeneratedEvents.Add(new ActInvokedEvent(owner, card, paidCost, selectedAbility?.RuntimeId));

            result.Success = true;
            return result;
        }

    }
}
