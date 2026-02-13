using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// A trigger event that represents a successful Fusion activation request for a specific card instance.
    /// Abilities can listen to <see cref="EventType.Fusion"/> and drive material selection and follow-up effects.
    /// </summary>
    [GameEventMetadata(
        "发动融合",
        "当一张卡牌发动融合时触发的事件。",
        EventType.Fusion,
        AbilityEditorCategories.BaseActionId,
        AbilityEditorCategories.BaseActionLabel)]
    public sealed class FusionInvokedEvent : GameEventBase, ICommittableEvent, IPlayerActionEvent
    {
        [GameEventParameter("融合玩家", "发动融合能力的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? FusionPlayer => Source?.Player;

        [GameEventParameter("融合卡牌", "发动融合能力的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? FusionCard => Target.FirstOrDefault()?.Card;

        public FusionInvokedEvent(
            PlayerState requestingPlayer,
            CardInstance sourceCard,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.Fusion, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(requestingPlayer);
            Target.Add(TargetInfo.ForCard(sourceCard));
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = FusionCard;
            var player = FusionPlayer;
            if (card == null || player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "FusionInvokedEvent.Commit: player or card is null." };
            }

            if (card.OwnerPlayerId != player.PlayerId)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "FusionInvokedEvent.Commit: card owner mismatch." };
            }

            if (card.CurrentZone != CardZone.Hand)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "FusionInvokedEvent.Commit: fusion card is not in hand." };
            }

            return new DomainOperationResult { Success = true };
        }
    }
}
