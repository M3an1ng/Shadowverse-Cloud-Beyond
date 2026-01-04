using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("公开", "当一张卡牌被公开时触发的事件。", EventType.CardRevealed)]
    public sealed class CardRevealedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("公开卡牌", "被公开的卡牌实例。")]
        public CardInstance? RevealedCard => Target.FirstOrDefault()?.Card;

        public CardRevealedEvent(PlayerState revealingPlayer, CardInstance revealedCard)
            : base(EventType.CardRevealed)
        {
            Source = TargetInfo.ForPlayer(revealingPlayer);
            Target.Add(TargetInfo.ForCard(revealedCard));
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
            return $"reveal card={gameState.InGameIds.Format(InGameIdKind.Card, card.InstanceId)}";
        }
    }
}
