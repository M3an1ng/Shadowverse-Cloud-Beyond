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
    /// A committable event that represents a card evolving.
    /// Source points to the evolution source (card or player), Target contains the evolved card.
    /// Its Commit method applies the actual state changes to the card instance.
    /// </summary>
    [GameEventMetadata("进化", "当一张卡牌进化时触发的事件。", EventType.Evolve)]
    public class EvolveEvent : GameEventBase, ICommittableEvent, IPlayerActionEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("进化来源", "能力进化时为来源卡牌，否则为进化卡牌所属的玩家。", DslDisplayTypeName = "事件源")]
        public TargetInfo? EvolutionSource => Source;

        [GameEventParameter("进化的卡牌", "被进化的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? EvolvedCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("是否超进化", "标识该进化是否来自超进化。")]
        public bool IsSuperEvolution { get; }

        [GameEventParameter("是否手动进化", "进化是否由玩家通过进化点触发（否则为效果进化）。")]
        public bool IsManualEvolution { get; }

        public EvolveEvent(
            CardInstance evolvedCard,
            TargetInfo evolutionSource,
            bool isSuperEvolution = false,
            bool isManualEvolution = false,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.Evolve, eventId, timestamp)
        {
            if (evolutionSource == null)
            {
                throw new ArgumentNullException(nameof(evolutionSource));
            }

            Source = evolutionSource;
            Target.Add(TargetInfo.ForCard(evolvedCard));
            IsSuperEvolution = isSuperEvolution;
            IsManualEvolution = isManualEvolution;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var cardToEvolve = EvolvedCard;

            if (cardToEvolve == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Card to evolve is null in EvolveEvent." };
            }

            var evolveResult = cardToEvolve.ApplyEvolutionChange(context, IsSuperEvolution);
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
            var card = EvolvedCard;
            if (card == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Card(card)}进化了。";
        }
    }
}
