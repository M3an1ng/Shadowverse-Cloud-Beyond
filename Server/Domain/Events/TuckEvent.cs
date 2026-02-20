using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// High-level tuck event: move a card from the field into its owner's deck.
    /// Commit delegates to AddCardToDeckEvent.
    /// </summary>
    [GameEventMetadata("置入牌库", "当一张卡牌从战场置入拥有者牌库时触发的事件。", EventType.Tuck)]
    public class TuckEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("置入原因", "卡牌被置入牌库的原因。")]
        public EventReasonSource Reason { get; }

        [GameEventParameter("置入的卡牌", "被置入牌库的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? TuckedCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("置入来源快照", "生成事件时记录的置入来源快照（若来源为卡牌）。", Scope = GameEventParameterScope.Input)]
        public CardInstanceSnapshot? TuckSourceSnapshot { get; }

        [GameEventParameter("置入目标快照", "生成事件时记录的置入目标快照。", Scope = GameEventParameterScope.Input)]
        public CardInstanceSnapshot? TuckedCardSnapshot { get; }

        public bool TuckApplied { get; private set; }

        public TuckEvent(
            CardInstance card,
            EventReasonSource reason = EventReasonSource.Ability,
            Guid? eventId = null,
            DateTime? timestamp = null,
            CardInstanceSnapshot? tuckedCardSnapshot = null,
            CardInstanceSnapshot? tuckSourceSnapshot = null)
            : base(EventType.Tuck, eventId, timestamp)
        {
            Target.Add(TargetInfo.ForCard(card));
            Reason = reason;
            TuckedCardSnapshot = tuckedCardSnapshot ?? TryCreateSnapshot(card);
            TuckSourceSnapshot = tuckSourceSnapshot;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            TuckApplied = false;
            var card = TuckedCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "TuckEvent.Commit: card is null." };
            }

            var owner = context.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
            if (owner == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"TuckEvent.Commit: owner {card.OwnerPlayerId} not found." };
            }

            var result = new DomainOperationResult { Success = true };
            TuckApplied = card.CurrentZone != CardZone.Deck;
            result.GeneratedEvents.Add(new AddCardToDeckEvent(owner, new List<CardInstance> { card }));
            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            if (!TuckApplied)
            {
                return string.Empty;
            }

            var card = TuckedCard;
            if (card == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Card(card)}被置入牌库。";
        }

        private static CardInstanceSnapshot? TryCreateSnapshot(CardInstance? card)
        {
            if (card == null)
            {
                return null;
            }

            try
            {
                return CardInstanceSnapshot.From(card);
            }
            catch
            {
                return null;
            }
        }
    }
}
