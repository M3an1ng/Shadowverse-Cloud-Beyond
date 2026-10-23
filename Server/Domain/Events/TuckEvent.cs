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

        [GameEventParameter("置入来源快照", "提交置入事件时记录的置入来源快照（若来源为卡牌）。", Scope = GameEventParameterScope.Output)]
        public CardInstanceSnapshot? TuckSourceSnapshot { get; private set; }

        [GameEventParameter("置入目标快照", "提交置入事件时记录的置入目标快照。", Scope = GameEventParameterScope.Output)]
        public CardInstanceSnapshot? TuckedCardSnapshot { get; private set; }

        [GameEventParameter("牌库插入方式", "卡牌置入牌库时使用的插入方式。")]
        public DeckInsertMode InsertMode { get; }

        public TuckEvent(
            CardInstance card,
            EventReasonSource reason = EventReasonSource.Ability,
            Guid? eventId = null,
            DateTime? timestamp = null,
            CardInstanceSnapshot? tuckedCardSnapshot = null,
            CardInstanceSnapshot? tuckSourceSnapshot = null,
            DeckInsertMode insertMode = DeckInsertMode.Random)
            : base(EventType.Tuck, eventId, timestamp)
        {
            Target.Add(TargetInfo.ForCard(card));
            Reason = reason;
            InsertMode = insertMode;
            TuckedCardSnapshot = tuckedCardSnapshot;
            TuckSourceSnapshot = tuckSourceSnapshot;
        }

        public DomainOperationResult Commit(GameContext context)
        {
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

            TuckedCardSnapshot ??= CardSnapshotCapture.TryCapture(card);
            TuckSourceSnapshot ??= CardSnapshotCapture.TryCapture(Source?.Card);

            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.Add(new AddCardToDeckEvent(owner, new List<CardInstance> { card }, InsertMode));
            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {

            var card = TuckedCard;
            if (card == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Card(card)}被置入牌库。";
        }

    }
}
