using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a card instance's health changes.
    /// Atomic commit: apply the actual health change by mutating Damage via CardInstance.ApplyHealthChange
    /// </summary>
    [GameEventMetadata("生命值变化", "当卡牌的生命值发生变化时触发的事件。", EventType.HealthChanged)]
    public class HealthChangedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("旧生命值", "变化前的生命值。")]
        public int OldHealth { get; }

        [GameEventParameter("新生命值", "变化后的生命值。")]
        public int NewHealth { get; }

        [GameEventParameter("变化量", "生命值的变化量（正数为治疗，负数为伤害）。")]
        public int ChangeAmount { get; }

        [GameEventParameter("目标卡牌", "生命值发生变化的卡牌。")]
        public CardInstance? TargetCard => Source?.Card;

        public HealthChangedEvent(CardInstance card, int oldHealth, int newHealth, int changeAmount)
            : base(EventType.HealthChanged)
        {
            Source = TargetInfo.ForCard(card);
            OldHealth = oldHealth;
            NewHealth = newHealth;
            ChangeAmount = changeAmount;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var card = TargetCard;
            if (card == null)
            {
                result.Success = false;
                result.ErrorMessage = "HealthChangedEvent.Commit: Source card is null.";
                return result;
            }

            // Positive ChangeAmount = heal, negative = damage
            card.ApplyHealthChange(ChangeAmount);

            result.Success = true;
            return result;
        }
    }
}

