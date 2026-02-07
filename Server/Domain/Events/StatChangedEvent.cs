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
        public int? AttackModifier { get; }

        [GameEventParameter("生命值修正", "生命值的修正值。")]
        public int? HealthModifier { get; }

        [GameEventParameter("费用修正", "费用的修正值。")]
        public int? CostModifier { get; }

        [GameEventParameter("持续时间", "属性变化的持续时间类型。")]
        public DurationType DurationType { get; }

        [GameEventParameter("持续参数", "持续时间的附加参数。")]
        public int? DurationParameter { get; }

        [GameEventParameter("修正层级", "属性变化的修正层级。")]
        public ModifierLayer ModifierLayer { get; }

        [GameEventParameter("目标卡牌", "属性被修改的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? TargetCard => Target.FirstOrDefault()?.Card;

        public string? EffectKey { get; }
        public bool CanStack { get; }

        public StatChangedEvent(CardInstance targetCard, int? attackModifier, int? healthModifier, int? costModifier, DurationType durationType = DurationType.Permanent, int? durationParameter = null, ModifierLayer modifierLayer = ModifierLayer.Additive, string? effectKey = null, bool canStack = true, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.StatChanged, eventId, timestamp)
        {
            // The target of a stat change is the card being changed itself.
            Target.Add(TargetInfo.ForCard(targetCard));
            AttackModifier = attackModifier;
            HealthModifier = healthModifier;
            CostModifier = costModifier;
            DurationType = durationType;
            DurationParameter = durationParameter;
            ModifierLayer = modifierLayer;
            EffectKey = effectKey;
            CanStack = canStack;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = TargetCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "StatChangedEvent.Commit: Target card is null." };
            }

            var modifications = new List<StatModification>();
            if (ShouldApply(AttackModifier, ModifierLayer))
                modifications.Add(new StatModification(StatType.Attack, AttackModifier!.Value, ModifierLayer));
            if (ShouldApply(HealthModifier, ModifierLayer))
                modifications.Add(new StatModification(StatType.Health, HealthModifier!.Value, ModifierLayer));
            if (ShouldApply(CostModifier, ModifierLayer))
                modifications.Add(new StatModification(StatType.Cost, CostModifier!.Value, ModifierLayer));

            if (modifications.Count == 0)
                return new DomainOperationResult { Success = true }; // No change, no further events.

            int? projectedHealthAfter = null;
            if (ShouldApply(HealthModifier, ModifierLayer))
            {
                var currentMaxHealth = context.BuffService.GetEffectiveMaxHealth(card, context);
                var newMaxHealth = ApplyStatModification(currentMaxHealth, new StatModification(StatType.Health, HealthModifier!.Value, ModifierLayer));
                newMaxHealth = Math.Max(0, newMaxHealth);
                projectedHealthAfter = newMaxHealth - card.Damage;
            }

            // Unified buff application: BuffService will use ability context when present,
            // otherwise it will create a system/no-source stat buff.
            var result = context.BuffService.ApplyStatBuff(
                context: context,
                target: card,
                modifications: modifications,
                durationType: DurationType,
                durationParameter: DurationParameter,
                effectKey: EffectKey,
                canStack: CanStack);

            if (!result.Success)
            {
                return result;
            }

            if (projectedHealthAfter.HasValue && projectedHealthAfter.Value <= 0)
            {
                if (card.IsLeader)
                {
                    var loserId = card.OwnerPlayerId;
                    var winnerId = context.SourceCard?.OwnerPlayerId;
                    if (!winnerId.HasValue || winnerId.Value == loserId)
                    {
                        var opponent = context.CurrentGameState.GetOpponentPlayerState(loserId);
                        winnerId = opponent?.PlayerId;
                    }

                    if (winnerId.HasValue && winnerId.Value != Guid.Empty)
                    {
                        result.GeneratedEvents.Add(new GameEndEvent(winnerId.Value, loserId, "Leader health reached zero"));
                    }
                }
                else
                {
                    var sourceCard = context.SourceCard ?? card;
                    result.GeneratedEvents.Add(new DestroyEvent(card, sourceCard, "DestroyedByStatChange"));
                }
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

            var parts = new List<string>();
            if (ShouldApply(AttackModifier, ModifierLayer)) parts.Add(FormatSigned("攻", AttackModifier!.Value));
            if (ShouldApply(HealthModifier, ModifierLayer)) parts.Add(FormatSigned("体", HealthModifier!.Value));
            if (ShouldApply(CostModifier, ModifierLayer)) parts.Add(FormatSigned("费", CostModifier!.Value));
            if (parts.Count == 0)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Card(target)}属性变化（{string.Join(" / ", parts)}）。";
        }

        private static bool ShouldApply(int? value, ModifierLayer modifierLayer)
        {
            if (!value.HasValue)
            {
                return false;
            }

            return value.Value != 0 || modifierLayer == ModifierLayer.Set;
        }

        private static string FormatSigned(string label, int value)
        {
            var sign = value >= 0 ? "+" : "-";
            var amount = Math.Abs(value);
            return $"{label}{sign}{AnnouncementTokens.Number(amount)}";
        }

        private static int ApplyStatModification(int currentValue, StatModification modification)
        {
            return modification.Layer switch
            {
                ModifierLayer.Additive => currentValue + modification.Value,
                ModifierLayer.Multiplicative => (int)(currentValue * (modification.Value / 100.0)),
                ModifierLayer.Set => modification.Value,
                ModifierLayer.Base => modification.Value,
                _ => currentValue,
            };
        }
    }
}
