using System;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events; // EventType, GameContext
using ShadowverseBeyond.Server.Dtos.Enums; // CardZone
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a card is vanished from the game.
    /// </summary>
    [GameEventMetadata("消失", "当一张卡牌从游戏中消失时触发的事件。", EventType.Vanish)]
    public class CardVanishedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("原始区域", "卡牌消失前所在的区域。")]
        public CardZone OriginalZone { get; }

        [GameEventParameter("消失原因", "卡牌消失的原因。")]
        public EventReasonSource VanishReason { get; }

        [GameEventParameter("消失来源", "导致消失的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? VanishSource => Source?.Card;

        [GameEventParameter("消失的卡牌", "被消失的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? VanishedCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("消失来源快照", "提交消失事件时记录的消失来源快照。", Scope = GameEventParameterScope.Output)]
        public CardInstanceSnapshot? VanishSourceSnapshot { get; private set; }

        [GameEventParameter("消失目标快照", "提交消失事件时记录的消失目标快照。", Scope = GameEventParameterScope.Output)]
        public CardInstanceSnapshot? VanishedCardSnapshot { get; private set; }

        public CardVanishedEvent(
            CardInstance card,
            CardZone originalZone,
            EventReasonSource vanishReason = EventReasonSource.Ability,
            CardInstance? sourceCard = null,
            Guid? eventId = null,
            DateTime? timestamp = null,
            CardInstanceSnapshot? vanishedCardSnapshot = null,
            CardInstanceSnapshot? vanishSourceSnapshot = null)
            : base(EventType.Vanish, eventId, timestamp)
        {
            if (sourceCard != null)
            {
                Source = TargetInfo.ForCard(sourceCard);
            }
            Target.Add(TargetInfo.ForCard(card));
            VanishedCardSnapshot = vanishedCardSnapshot;
            VanishSourceSnapshot = vanishSourceSnapshot;
            OriginalZone = originalZone;
            VanishReason = vanishReason;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = VanishedCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "CardVanishedEvent.Commit: card is null." };
            }
            var owner = context.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
            if (owner == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"CardVanishedEvent.Commit: owner {card.OwnerPlayerId} not found." };
            }

            VanishedCardSnapshot ??= CardSnapshotCapture.TryCapture(card);
            VanishSourceSnapshot ??= CardSnapshotCapture.TryCapture(VanishSource);

            // Vanish is represented as moving the card into the removed-from-game zone.
            // Important: Commit MUST NOT call a method that enqueues another CardVanishedEvent to avoid recursion.
            var result = new DomainOperationResult { Success = true };
            result.GeneratedEvents.Add(new CardMovedEvent(card, card.CurrentZone, CardZone.Removed, CardMoveReason.Vanished));
            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var card = VanishedCard;
            if (card == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Card(card)}消失了。";
        }

    }
}
