using System;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events; // EventType, GameContext
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a specific card is discarded from a player's hand.
    /// </summary>
    [GameEventMetadata("弃置", "当一张卡牌从玩家手牌中被弃置时触发的事件。", EventType.Discard)]
    public class CardDiscardedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("弃置原因", "卡牌被弃置的原因。")]
        public EventReasonSource Reason { get; }

        [GameEventParameter("弃置玩家", "弃置卡牌的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? DiscardingPlayer => Source?.Player;

        [GameEventParameter("被弃置的卡牌", "被弃置的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? DiscardedCard => Target.FirstOrDefault()?.Card;

        public CardDiscardedEvent(PlayerState player, CardInstance card, EventReasonSource reason, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Discard, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            Target.Add(TargetInfo.ForCard(card));
            Reason = reason;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = DiscardingPlayer;
            var card = DiscardedCard;
            if (player == null || card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "CardDiscardedEvent.Commit: player or card is null." };
            }

            // 丢弃：从手牌移动到墓地，使用封装方法触发 CardMovedEvent 原子事实
            var moveResult = player.MoveCardToGraveyard(card, CardMoveReason.Discarded);
            return moveResult;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = DiscardingPlayer;
            if (player == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Player(player)}舍弃了 {AnnouncementTokens.Number(1)} 张手牌。";
        }

    }
}
