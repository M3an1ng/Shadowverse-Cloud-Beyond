using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events; // EventType, GameContext
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a card instance's stats (Attack, Health, MaxHealth) change.
    /// </summary>
    [GameEventMetadata("属性变化", "当一张卡牌的属性发生变化时触发的事件。", EventType.StatChanged)]
    public class StatChangedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("攻击力修正", "攻击力的修正值。")]
        public int AttackModifier { get; }

        [GameEventParameter("生命值修正", "生命值的修正值。")]
        public int HealthModifier { get; }

        [GameEventParameter("费用修正", "费用的修正值。")]
        public int CostModifier { get; }

        [GameEventParameter("目标卡牌", "属性被修改的卡牌。")]
        public CardInstance? TargetCard => Target.FirstOrDefault()?.Card;

        public StatChangedEvent(CardInstance targetCard, int attackModifier, int healthModifier, int costModifier)
            : base(EventType.StatChanged)
        {
            // The target of a stat change is the card being changed itself.
            Target.Add(TargetInfo.ForCard(targetCard));
            AttackModifier = attackModifier;
            HealthModifier = healthModifier;
            CostModifier = costModifier;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = TargetCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "StatChangedEvent.Commit: Target card is null." };
            }

            var modifications = new List<StatModification>();
            if (AttackModifier != 0)
                modifications.Add(new StatModification(StatType.Attack, AttackModifier, ModifierLayer.Additive));
            if (HealthModifier != 0)
                modifications.Add(new StatModification(StatType.Defense, HealthModifier, ModifierLayer.Additive));
            if (CostModifier != 0)
                modifications.Add(new StatModification(StatType.Cost, CostModifier, ModifierLayer.Additive));

            if (modifications.Count == 0)
                return new DomainOperationResult { Success = true }; // No change, no further events.

            // Unified buff application: BuffService will use ability context when present,
            // otherwise it will create a system/no-source stat buff.
            return context.BuffService.ApplyStatBuff(
                context: context,
                target: card,
                modifications: modifications,
                durationType: DurationType.Permanent,
                durationParameter: null,
                isNonStackable: false);
        }

        public string GetAnnouncement(GameState gameState)
        {
            var targetId = TargetCard?.InstanceId;
            if (!targetId.HasValue)
            {
                return "stat";
            }

            string fmt(string key, int value) => value >= 0 ? $"{key}=+{value}" : $"{key}={value}";
            var parts = new List<string> { $"stat target={gameState.InGameIds.Format(InGameIdKind.Card, targetId.Value)}" };
            if (AttackModifier != 0) parts.Add(fmt("atk", AttackModifier));
            if (HealthModifier != 0) parts.Add(fmt("hp", HealthModifier));
            if (CostModifier != 0) parts.Add(fmt("cost", CostModifier));
            return string.Join(' ', parts);
        }
    }
}
