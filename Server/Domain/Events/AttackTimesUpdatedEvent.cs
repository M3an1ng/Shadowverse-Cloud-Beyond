using System;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using System.Diagnostics.Tracing;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a unit's maximum attacks per turn changes.
    /// </summary>
    [GameEventMetadata("攻击次数更新", "当卡牌每回合可攻击次数发生变化时触发。", EventType.AttackTimesUpdated, visibility: EventMetadataVisibility.Hidden)]
    public class AttackTimesUpdatedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("最大攻击次数", "更新后的最大攻击次数。")]
        public int MaxAttacksPerTurn { get; }

        [GameEventParameter("目标卡牌", "攻击次数被修改的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? TargetCard => Source?.Card;

        public AttackTimesUpdatedEvent(CardInstance targetCard, int maxAttacksPerTurn, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.AttackTimesUpdated, eventId, timestamp)
        {
            Source = TargetInfo.ForCard(targetCard);
            MaxAttacksPerTurn = maxAttacksPerTurn;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = TargetCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "AttackTimesUpdatedEvent.Commit: Target card is null." };
            }

            var oldCanAttack = card.CanAttack;
            card.MaxAttacksPerTurn = Math.Max(0, MaxAttacksPerTurn);

            var result = new DomainOperationResult { Success = true };
            if (oldCanAttack != card.CanAttack)
            {
                result.GeneratedEvents.Add(new AttackStateUpdatedEvent(card, card.CanAttack));
            }

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var target = TargetCard;
            if (target == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Card(target)}每回合可攻击 {AnnouncementTokens.Number(MaxAttacksPerTurn)} 次。";
        }
    }
}
