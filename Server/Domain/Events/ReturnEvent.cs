using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 高层返回事件：描述“将目标卡牌返回到拥有者手牌”的意图。
    /// Commit 不直接落地状态变化，而是生成 AddCardToHandEvent 交由后续处理。
    /// </summary>
    [GameEventMetadata("回手", "当一张卡牌被返回到拥有者手牌时触发的事件。", EventType.Return)]
    public class ReturnEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("返回原因", "卡牌被返回的原因。")]
        public string Reason { get; }

        [GameEventParameter("返回的卡牌", "被返回的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? ReturnedCard => Target.FirstOrDefault()?.Card;

        public ReturnEvent(PlayerState player, CardInstance card, string reason = "Effect", Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Return, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            Target.Add(TargetInfo.ForCard(card));
            Reason = reason;
        }

        public ReturnEvent(CardInstance card, string reason = "Effect", Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Return, eventId, timestamp)
        {
            Target.Add(TargetInfo.ForCard(card));
            Reason = reason;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = ReturnedCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ReturnEvent.Commit: card is null." };
            }

            var player = Source?.Player ?? context.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ReturnEvent.Commit: player is null." };
            }

            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.Add(new AddCardToHandEvent(player, [card]));
            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var card = ReturnedCard;
            if (card == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Card(card)}返回手牌。";
        }
    }
}
