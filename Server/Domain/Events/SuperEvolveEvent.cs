using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 表示随从进行“超进化”的事件。超进化继承进化的触发，同时具备额外触发点。
    /// Source 为进化来源（卡牌或玩家），Target 为被超进化的卡牌。
    /// </summary>
    [GameEventMetadata("超进化", "当一张卡牌进行超进化时触发的事件。", EventType.SuperEvolve)]
    public class SuperEvolveEvent : GameEventBase, ICommittableEvent, IPlayerActionEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("进化来源", "能力进化时为来源卡牌，否则为进化卡牌所属的玩家。", DslDisplayTypeName = "事件源")]
        public TargetInfo? EvolutionSource => Source;

        [GameEventParameter("超进化的卡牌", "被超进化的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? SuperEvolvedCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("是否手动进化", "true 表示通过进化点手动超进化；false 表示通过效果超进化。")]
        public bool IsManualEvolution { get; }

        public SuperEvolveEvent(
            CardInstance card,
            TargetInfo evolutionSource,
            bool isManualEvolution = false,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.SuperEvolve, eventId, timestamp)
        {
            if (evolutionSource == null)
            {
                throw new ArgumentNullException(nameof(evolutionSource));
            }

            Source = evolutionSource;
            Target.Add(TargetInfo.ForCard(card));
            IsManualEvolution = isManualEvolution;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var cardToEvolve = SuperEvolvedCard;

            if (cardToEvolve == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Card to super evolve is null in SuperEvolveEvent." };
            }

            var evolveResult = cardToEvolve.ApplySuperEvolutionChange(context);
            if (!evolveResult.Success)
            {
                return evolveResult;
            }

            result.GeneratedEvents.AddRange(evolveResult.GeneratedEvents);

            result.Success = true;
            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var card = SuperEvolvedCard;
            if (card == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Card(card)}超进化了。";
        }
    }
}
