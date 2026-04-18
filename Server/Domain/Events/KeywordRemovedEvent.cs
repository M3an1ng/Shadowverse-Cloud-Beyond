using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events; // EventType, GameContext
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a keyword is removed from a card.
    /// </summary>
    [GameEventMetadata("关键字移除", "当一张卡牌失去关键字时触发的事件。", EventType.KeywordRemoved)]
    public class KeywordRemovedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("移除的关键字", "被移除的关键字类型。")]
        public CardKeyword Keyword { get; }

        [GameEventParameter("关键字移除来源", "导致关键字移除的来源卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? KeywordRemovalSource => Source?.Card;

        [GameEventParameter("目标卡牌", "失去关键字的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? TargetCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("持续时间", "关键字移除持续时间类型。")]
        public DurationType DurationType { get; }

        [GameEventParameter("持续参数", "持续时间参数。")]
        public int? DurationParameter { get; }

        public ConditionConfig? RemoveCondition { get; }

        public string? EffectKey { get; }
        public bool CanStack { get; }

        public KeywordRemovedEvent(CardKeyword keyword, CardInstance target, CardInstance? source = null, DurationType durationType = DurationType.Permanent, int? durationParameter = null, string? effectKey = null, bool canStack = true, ConditionConfig? removeCondition = null, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.KeywordRemoved, eventId, timestamp)
        {
            Keyword = keyword;
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
            var card = TargetCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "KeywordRemovedEvent.Commit: target card is null." };
            }

            var buffService = context.ServiceProvider.GetRequiredService<IBuffService>();
            var buffResult = buffService.ApplyRemoveKeywordBuff(
                context: context,
                target: card,
                keywordsToRemove: new List<CardKeyword> { Keyword },
                durationType: DurationType,
                durationParameter: DurationParameter,
                effectKey: EffectKey,
                canStack: CanStack,
                removeCondition: RemoveCondition
            );
            return buffResult;
        }
    }
}
