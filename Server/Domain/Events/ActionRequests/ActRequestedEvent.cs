using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
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

        public ActRequestedEvent(Guid playerId, ActActionDto actionDto)
            : base(EventType.ActRequested)
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

            var hasActAbility = context.AbilityManager
                .GetAbilitiesForCard(card.InstanceId)
                .Any(a => a.Type == AbilityType.Activated);

            if (!hasActAbility)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "This card has no Act ability." };
            }

            var owner = context.CurrentGameState.GetPlayerState(PlayerId);
            if (owner == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Player state not found for {PlayerId}." };
            }

            if (card.HasUsedActThisTurn(owner.TurnNumber))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "This card has already used Act this turn." };
            }

            // Trigger actual activated abilities (data-driven via TriggerConfig listening to EventType.Act).
            result.GeneratedEvents.Add(new ActInvokedEvent(owner, card));

            result.Success = true;
            return result;
        }
    }
}
