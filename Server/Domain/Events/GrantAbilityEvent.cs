using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when an ability is granted to a card.
    /// </summary>
    [GameEventMetadata("授予能力", "当一张卡牌获得能力时触发的事件。", EventType.AbilityGranted,
        AbilityEditorCategories.TempStateId, AbilityEditorCategories.TempStateLabel)]
    public class GrantAbilityEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("授予的能力", "被授予的能力配置列表。")]
        public List<AbilityConfig> AbilityConfigs { get; }

        [GameEventParameter("授予来源", "授予能力的来源卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? GrantSource => Source?.Card;

        [GameEventParameter("目标卡牌", "获得能力的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? TargetCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("持续时间", "能力持续时间类型。")]
        public DurationType DurationType { get; }

        [GameEventParameter("持续参数", "持续时间参数。")]
        public int? DurationParameter { get; }

        public ConditionConfig? RemoveCondition { get; }

        public string? EffectKey { get; }
        public bool CanStack { get; }

        public GrantAbilityEvent(
            IEnumerable<AbilityConfig> abilityConfigs,
            CardInstance target,
            CardInstance? source = null,
            DurationType durationType = DurationType.Permanent,
            int? durationParameter = null,
            string? effectKey = null,
            bool canStack = true,
            ConditionConfig? removeCondition = null,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.AbilityGranted, eventId, timestamp)
        {
            AbilityConfigs = abilityConfigs?.ToList() ?? new List<AbilityConfig>();
            if (source != null)
            {
                Source = TargetInfo.ForCard(source);
            }
            Target.Add(TargetInfo.ForCard(target ?? throw new ArgumentNullException(nameof(target))));
            DurationType = durationType;
            DurationParameter = durationParameter;
            EffectKey = effectKey;
            CanStack = canStack;
            RemoveCondition = removeCondition;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (AbilityConfigs.Count == 0)
            {
                return new DomainOperationResult { Success = true };
            }

            var card = TargetCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "GrantAbilityEvent.Commit: Target card is null." };
            }

            var buffFactory = context.ServiceProvider.GetRequiredService<IBuffFactory>();
            var buffService = context.ServiceProvider.GetRequiredService<IBuffService>();

            var sourceAbility = context.GetCurrentAbility();
            var sourceCard = GrantSource ?? context.SourceCard;
            var ownerPlayer = context.CurrentGameState.GetPlayerState(sourceCard?.OwnerPlayerId ?? context.RequestingPlayerId);
            var turnNumber = ownerPlayer?.TurnNumber ?? 0;

            var effects = new List<IBuffEffect>
            {
                new AbilityBuffEffect(AbilityConfigs)
            };

            Buff buff;
            if (sourceAbility == null)
            {
                buff = new Buff(
                    sourceAbilityId: Guid.Empty,
                    targetCardInstanceId: card.InstanceId,
                    currentTurn: turnNumber,
                    effects: effects,
                    durationType: DurationType,
                    durationParameter: DurationParameter,
                    removalCondition: RemoveCondition);
            }
            else
            {
                buff = buffFactory.CreateCompositeBuff(
                    target: card,
                    sourceCard: sourceCard,
                    sourceAbility: sourceAbility,
                    effects: effects,
                    durationType: DurationType,
                    durationParameter: DurationParameter,
                    currentTurn: turnNumber,
                    removeCondition: RemoveCondition);
            }

            if (!string.IsNullOrWhiteSpace(EffectKey) && buff.SourceAbilityId != Guid.Empty)
            {
                buff.EffectKey = EffectKey;
            }

            return buffService.ApplyBuffProcess(buff, context, CanStack);
        }
    }
}
