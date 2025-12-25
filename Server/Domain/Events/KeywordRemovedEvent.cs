using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events; // EventType, GameContext
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Events.Metadata;

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

        [GameEventParameter("目标卡牌", "失去关键字的卡牌。")]
        public CardInstance? TargetCard => Source?.Card;

        public KeywordRemovedEvent(CardKeyword keyword, CardInstance source)
            : base(EventType.KeywordRemoved)
        {
            Keyword = keyword;
            Source = TargetInfo.ForCard(source);
            Target.Add(TargetInfo.ForCard(source));
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = Source?.Card;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "KeywordRemovedEvent.Commit: Source card is null." };
            }

            var buffService = context.ServiceProvider.GetRequiredService<IBuffService>();
            var buffResult = buffService.ApplyRemoveKeywordBuff(
                context: context,
                target: card,
                keywordsToRemove: new List<CardKeyword> { Keyword }
            );
            return buffResult;
        }
    }
}
