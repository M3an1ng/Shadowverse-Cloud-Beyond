using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Constants; // for CardKeyword
using ShadowverseBeyond.Server.Domain.Events; // EventType, GameContext
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a keyword is added to a card.
    /// </summary>
    [GameEventMetadata("关键字添加", "当一张卡牌获得关键字时触发的事件。", EventType.KeywordApplied)]
    public class KeywordAddedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("添加的关键字", "被添加的关键字类型。")]
        public CardKeyword KeywordAdded { get; }

        [GameEventParameter("目标卡牌", "获得关键字的卡牌。")]
        public CardInstance? TargetCard => Source?.Card;

        public KeywordAddedEvent(CardKeyword keywordAdded, CardInstance source)
            : base(EventType.KeywordApplied)
        {
            KeywordAdded = keywordAdded;
            Source = TargetInfo.ForCard(source);
            Target.Add(TargetInfo.ForCard(source));
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var card = TargetCard;
            if (card == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "KeywordAddedEvent.Commit: Source card is null." };
            }

            var buffService = context.ServiceProvider.GetRequiredService<IBuffService>();
            var buffResult = buffService.ApplyKeywordBuff(
                context: context,
                target: card,
                keywords: new List<CardKeyword> { KeywordAdded },
                durationType: DurationType.Permanent,
                durationParameter: null,
                isNonStackable: false);

            return buffResult;
        }
    }
}

