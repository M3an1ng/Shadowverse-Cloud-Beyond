using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using System;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when healing is applied to a target.
    /// </summary>
    [GameEventMetadata("回复", "当对目标进行治疗时触发的事件。", EventType.Heal)]
    public class HealEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("回复量", "回复的数值。")]
        public int HealAmount { get; }

        [GameEventParameter("回复来源", "提供回复的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? HealSource => Source?.Card;

        [GameEventParameter("回复目标", "接受回复的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? HealTarget => Target.FirstOrDefault()?.Card;

        public HealEvent(CardInstance target, int healAmount, CardInstance source, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Heal, eventId, timestamp)
        {
            Target.Add(TargetInfo.ForCard(target));
            HealAmount = healAmount;
            Source = TargetInfo.ForCard(source);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (HealTarget == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "HealEvent target card is null." };
            }

            var result = new DomainOperationResult { Success = true };
            var card = HealTarget;
            int oldHealth = card.CurrentHealth;
            int actualHealing = Math.Min(HealAmount, card.Damage);
            int newHealth = oldHealth + actualHealing;

            if (actualHealing > 0)
            {
                result.GeneratedEvents.Add(new HealthChangedEvent(card, oldHealth, newHealth, actualHealing));
            }

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var target = HealTarget;
            if (target == null || HealAmount <= 0)
            {
                return string.Empty;
            }

            var source = HealSource;
            return source != null
                ? $"{AnnouncementTokens.Card(source)}为{AnnouncementTokens.Card(target)}回复 {AnnouncementTokens.Number(HealAmount)} 点生命。"
                : $"{AnnouncementTokens.Card(target)}回复 {AnnouncementTokens.Number(HealAmount)} 点生命。";
        }
    }
}
