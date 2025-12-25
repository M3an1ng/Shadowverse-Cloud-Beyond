using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("卡牌公开", "当一张卡牌被公开其卡面信息时触发的事件。", EventType.CardRevealed)]
    public sealed class CardRevealedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("公开卡牌", "被公开的卡牌实例。")]
        public CardInstance? RevealedCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("卡牌定义ID", "被公开卡牌的定义ID（用于渲染卡面）。")]
        public int CardDefinitionId { get; }

        [GameEventParameter("卡牌名称", "被公开卡牌的名称（用于日志与显示）。")]
        public string CardName { get; }

        [GameEventParameter("原区域", "被公开卡牌所在的区域。")]
        public CardZone CardZone { get; }

        public CardRevealedEvent(PlayerState revealingPlayer, CardInstance revealedCard)
            : base(EventType.CardRevealed)
        {
            Source = TargetInfo.ForPlayer(revealingPlayer);
            Target.Add(TargetInfo.ForCard(revealedCard));

            CardDefinitionId = revealedCard.CurrentDefinitionId;
            CardName = revealedCard.Name;
            CardZone = revealedCard.CurrentZone;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            // Reveal is a pure visibility/broadcast event; it MUST NOT mutate game state.
            return new DomainOperationResult { Success = true };
        }

        public string GetAnnouncement(GameState gameState)
        {
            var card = RevealedCard;
            if (card == null) return "reveal";
            return $"reveal card={gameState.InGameIds.Format(InGameIdKind.Card, card.InstanceId)} def={CardDefinitionId} zone={CardZone}";
        }
    }
}
