using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a card instance's stats (Attack, Health, MaxHealth) change.
    /// </summary>
    [GameEventMetadata("属性变化", "当一张卡牌的属性发生变化时触发的事件。", EventType.StatChanged)]
    public class StatChangedEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        private readonly record struct HealthChangePreview(
            bool IsHealthSet,
            bool PreserveCurrentHealth,
            int? ProjectedHealthAfter);

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
        public bool PreserveCurrentHealthOnMaxHealthChange { get; }

        public StatChangedEvent(
            CardInstance targetCard,
            int? attackModifier,
            int? healthModifier,
            int? costModifier,
            DurationType durationType = DurationType.Permanent,
            int? durationParameter = null,
            ModifierLayer modifierLayer = ModifierLayer.Additive,
            string? effectKey = null,
            bool canStack = true,
            bool preserveCurrentHealthOnMaxHealthChange = false,
            Guid? eventId = null,
            DateTime? timestamp = null)
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
            PreserveCurrentHealthOnMaxHealthChange = preserveCurrentHealthOnMaxHealthChange;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = TargetCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "StatChangedEvent.Commit: Target card is null." };
            }

            var modifications = BuildStatModifications();
            if (modifications.Count == 0)
            {
                return new DomainOperationResult { Success = true };
            }

            var healthPreview = PreviewHealthChange(card, context);
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

            ApplyPostBuffHealthHandling(card, result, healthPreview);
            AppendLethalOutcomeEvents(card, context, result, healthPreview);
            return result;
        }

        private List<StatModification> BuildStatModifications()
        {
            var modifications = new List<StatModification>(capacity: 3);

            if (ShouldApply(AttackModifier, ModifierLayer))
            {
                modifications.Add(new StatModification(
                    StatType.Attack,
                    NormalizeModifierValue(AttackModifier!.Value, ModifierLayer),
                    ModifierLayer));
            }

            if (ShouldApply(HealthModifier, ModifierLayer))
            {
                modifications.Add(new StatModification(
                    StatType.Health,
                    NormalizeModifierValue(HealthModifier!.Value, ModifierLayer),
                    ModifierLayer));
            }

            if (ShouldApply(CostModifier, ModifierLayer))
            {
                modifications.Add(new StatModification(
                    StatType.Cost,
                    NormalizeModifierValue(CostModifier!.Value, ModifierLayer),
                    ModifierLayer));
            }

            return modifications;
        }

        private HealthChangePreview PreviewHealthChange(CardInstance card, GameContext context)
        {
            var hasHealthChange = ShouldApply(HealthModifier, ModifierLayer);
            if (!hasHealthChange)
            {
                return new HealthChangePreview(
                    IsHealthSet: false,
                    PreserveCurrentHealth: false,
                    ProjectedHealthAfter: null);
            }

            var isHealthSet = ModifierLayer == ModifierLayer.Set;
            var preserveCurrentHealth = PreserveCurrentHealthOnMaxHealthChange;

            var currentMaxHealth = context.BuffService.GetEffectiveMaxHealth(card, context);
            var previousCurrentHealth = Math.Max(0, currentMaxHealth - card.Damage);

            var newMaxHealth = ApplyStatModification(
                currentMaxHealth,
                new StatModification(
                    StatType.Health,
                    NormalizeModifierValue(HealthModifier!.Value, ModifierLayer),
                    ModifierLayer));
            newMaxHealth = Math.Max(0, newMaxHealth);

            var projectedHealthAfter = preserveCurrentHealth
                ? Math.Min(previousCurrentHealth, newMaxHealth)
                : isHealthSet
                    ? newMaxHealth
                    : newMaxHealth - card.Damage;

            return new HealthChangePreview(
                IsHealthSet: isHealthSet,
                PreserveCurrentHealth: preserveCurrentHealth,
                ProjectedHealthAfter: projectedHealthAfter);
        }

        private static void ApplyPostBuffHealthHandling(
            CardInstance card,
            DomainOperationResult result,
            HealthChangePreview healthPreview)
        {
            if (healthPreview.PreserveCurrentHealth)
            {
                // Buff is not active until BuffAppliedEvent commits. Mark generated buff events so final
                // health capping runs after the new max-health has actually taken effect.
                foreach (var buffApplied in result.GeneratedEvents.OfType<BuffAppliedEvent>())
                {
                    buffApplied.AppliedBuff.PreserveCurrentHealthOnApply = true;
                }
                return;
            }

            if (healthPreview.IsHealthSet)
            {
                // Set layer on health means "set max health and fully restore to that max".
                card.Damage = 0;
            }
        }

        private static void AppendLethalOutcomeEvents(
            CardInstance card,
            GameContext context,
            DomainOperationResult result,
            HealthChangePreview healthPreview)
        {
            if (!healthPreview.ProjectedHealthAfter.HasValue || healthPreview.ProjectedHealthAfter.Value > 0)
            {
                return;
            }

            if (card.IsLeader)
            {
                var loserId = card.OwnerPlayerId;
                var winnerId = ResolveWinnerIdForLeaderDefeat(card, context);
                if (winnerId.HasValue && winnerId.Value != Guid.Empty)
                {
                    result.GeneratedEvents.Add(new GameEndEvent(winnerId.Value, loserId, "主战者生命值归零"));
                }
                return;
            }

            var sourceCard = context.SourceCard ?? card;
            var destroyReason = context.CurrentEffect != null
                ? EventReasonSource.Ability
                : EventReasonSource.System;
            result.GeneratedEvents.Add(new DestroyEvent(card, sourceCard, destroyReason));
        }

        private static Guid? ResolveWinnerIdForLeaderDefeat(CardInstance leaderCard, GameContext context)
        {
            var loserId = leaderCard.OwnerPlayerId;
            var winnerId = context.SourceCard?.OwnerPlayerId;

            if (!winnerId.HasValue || winnerId.Value == loserId)
            {
                var opponent = context.CurrentGameState.GetOpponentPlayerState(loserId);
                winnerId = opponent?.PlayerId;
            }

            return winnerId;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var target = TargetCard;
            if (target == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (ShouldApply(AttackModifier, ModifierLayer)) parts.Add(FormatByLayer("攻击力", AttackModifier!.Value, ModifierLayer));
            if (ShouldApply(HealthModifier, ModifierLayer)) parts.Add(FormatByLayer("生命值", HealthModifier!.Value, ModifierLayer));
            if (ShouldApply(CostModifier, ModifierLayer)) parts.Add(FormatByLayer("费用", CostModifier!.Value, ModifierLayer));
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

            if (modifierLayer == ModifierLayer.Set)
            {
                return true;
            }

            if (modifierLayer == ModifierLayer.Multiplicative)
            {
                // Multiplicative values are now percentage-based: 100 = 1.0x (no-op).
                return value.Value != 100;
            }

            return value.Value != 0;
        }

        private static string FormatByLayer(string label, int value, ModifierLayer layer)
        {
            return layer switch
            {
                ModifierLayer.Additive => FormatAdditive(label, value),
                ModifierLayer.Multiplicative => $"{label}调整为{AnnouncementTokens.Number(value)}%",
                ModifierLayer.Set => $"{label}设为{AnnouncementTokens.Number(value)}",
                ModifierLayer.Base => $"{label}基础值设为{AnnouncementTokens.Number(value)}",
                _ => FormatAdditive(label, value),
            };
        }

        private static string FormatAdditive(string label, int value)
        {
            if (value >= 0)
            {
                return $"{label}增加{AnnouncementTokens.Number(value)}";
            }

            return $"{label}减少{AnnouncementTokens.Number(Math.Abs(value))}";
        }

        private static int ApplyStatModification(int currentValue, StatModification modification)
        {
            return modification.Layer switch
            {
                ModifierLayer.Additive => currentValue + (int)modification.Value,
                ModifierLayer.Multiplicative => CeilToInt(currentValue * modification.Value),
                ModifierLayer.Set => CeilToInt(modification.Value),
                ModifierLayer.Base => CeilToInt(modification.Value),
                _ => currentValue,
            };
        }

        private static double NormalizeModifierValue(int rawValue, ModifierLayer layer)
        {
            if (layer == ModifierLayer.Multiplicative)
            {
                // Percentage input: 50 -> 0.5x, 150 -> 1.5x.
                return rawValue / 100d;
            }

            return rawValue;
        }

        private static int CeilToInt(double value) => (int)Math.Ceiling(value);
    }
}
