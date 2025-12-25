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
    [GameEventMetadata("治疗", "当对目标进行治疗时触发的事件。", EventType.Heal)]
    public class HealEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("治疗量", "治疗的数值。")]
        public int HealAmount { get; }

        [GameEventParameter("治疗来源", "提供治疗的卡牌。")]
        public CardInstance? HealSource => Source?.Card;

        [GameEventParameter("治疗目标", "接受治疗的卡牌。")]
        public CardInstance? HealTarget => Target.FirstOrDefault()?.Card;

        public HealEvent(CardInstance target, int healAmount, CardInstance source)
            : base(EventType.Heal)
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
            int newHealth = Math.Max(0, oldHealth + actualHealing);

            if (actualHealing > 0)
            {
                result.GeneratedEvents.Add(new HealthChangedEvent(card, oldHealth, newHealth, actualHealing));
            }

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var targetId = HealTarget?.InstanceId;
            var sourceId = HealSource?.InstanceId;
            if (!targetId.HasValue || HealAmount <= 0)
            {
                return "heal";
            }

            var targetToken = gameState.InGameIds.Format(InGameIdKind.Card, targetId.Value);
            return sourceId.HasValue
                ? $"heal target={targetToken} amount={HealAmount} source={gameState.InGameIds.Format(InGameIdKind.Card, sourceId.Value)}"
                : $"heal target={targetToken} amount={HealAmount}";
        }
    }
}
