using System;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Domain.Constants;

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
        [GameEventParameter("启动玩家", "发动启动能力的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? ActPlayer => Source?.Player;

        [GameEventParameter("启动卡牌", "发动启动能力的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? ActCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("支付费用", "本次启动实际消耗的费用。")]
        public int PaidCost { get; }

        [GameEventParameter("触发能力ID", "本次启动所对应的能力运行时ID。")]
        public Guid? TriggeredAbilityRuntimeId { get; }

        public ActInvokedEvent(PlayerState requestingPlayer, CardInstance sourceCard, int paidCost, Guid? triggeredAbilityRuntimeId = null, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Act, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(requestingPlayer);
            Target.Add(TargetInfo.ForCard(sourceCard));
            PaidCost = paidCost;
            TriggeredAbilityRuntimeId = triggeredAbilityRuntimeId;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
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

            var actCost = PaidCost;

            if (player.CurrentPlayPoints < actCost)
            {
                return new DomainOperationResult
                {
                    Success = false,
                    ErrorMessage = $"Not enough PP to use Act. Required: {actCost}, Available: {player.CurrentPlayPoints}"
                };
            }

            if (actCost > 0)
            {
                var ppResult = player.ModifyPlayPoints(-actCost);
                result.GeneratedEvents.AddRange(ppResult.GeneratedEvents);
            }

            card.MarkActUsed(player.TurnNumber);

            result.Success = true;
            result.GeneratedEvents.Add(new ActStateUpdatedEvent(card, player.TurnNumber));
            return result;
        }
    }
}
