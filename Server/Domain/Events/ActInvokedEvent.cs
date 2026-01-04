using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// A trigger event that represents a successful Act activation for a specific card instance.
    /// Abilities can listen to <see cref="EventType.Act"/> and use relation filters (e.g. SourceSelf) to scope to the card.
    /// </summary>
    [GameEventMetadata(
        "启动",
        "当一张卡牌成功发动启动能力时触发的事件。",
        EventType.Act,
        AbilityEditorCategories.BaseActionId,
        AbilityEditorCategories.BaseActionLabel)]
    public sealed class ActInvokedEvent : GameEventBase, ICommittableEvent, IPlayerActionEvent
    {
        [GameEventParameter("启动玩家", "发动启动能力的玩家。")]
        public PlayerState? ActPlayer { get; }

        [GameEventParameter("启动卡牌", "发动启动能力的卡牌。")]
        public CardInstance? ActCard => Source?.Card;

        public ActInvokedEvent(PlayerState requestingPlayer, CardInstance sourceCard)
            : base(EventType.Act)
        {
            Source = TargetInfo.ForCard(sourceCard);
            ActPlayer = requestingPlayer;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = ActCard;
            var player = ActPlayer;
            if (card == null || player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ActInvokedEvent.Commit: player or card is null." };
            }

            if (card.OwnerPlayerId != player.PlayerId)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ActInvokedEvent.Commit: card owner mismatch." };
            }

            if (card.HasUsedActThisTurn(player.TurnNumber))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ActInvokedEvent.Commit: card has already used Act this turn." };
            }

            card.MarkActUsed(player.TurnNumber);

            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.Add(new ActStateUpdatedEvent(card, player.TurnNumber));
            return result;
        }
    }
}
