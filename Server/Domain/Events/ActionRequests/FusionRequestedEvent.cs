using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    /// <summary>
    /// A top-level committable event representing a player's request to activate a card's Fusion ability.
    /// This event validates the request and emits a FusionInvokedEvent on success.
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
            var result = new DomainOperationResult();

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

            if (card.HasUsedFusionThisTurn())
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "This card has already used Fusion this turn." };
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

            var owner = context.CurrentGameState.GetPlayerState(PlayerId);
            if (owner == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Player state not found for {PlayerId}." };
            }

            // Fusion request only invokes the fusion action root.
            // Material selection and follow-up effects are handled by Fusion-triggered abilities.
            result.GeneratedEvents.Add(new FusionInvokedEvent(owner, card));
            result.Success = true;
            return result;
        }
    }
}
