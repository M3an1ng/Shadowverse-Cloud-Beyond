using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// A committable event that represents a card evolving.
    /// Its Commit method applies the actual state changes to the card instance.
    /// </summary>
    [GameEventMetadata("进化", "当一张卡牌进化时触发的事件。", EventType.Evolve)]
    public class EvolveEvent : GameEventBase, ICommittableEvent, IPlayerActionEvent
    {
        [GameEventParameter("进化的卡牌", "被进化的卡牌。")]
        public CardInstance? EvolvedCard => Source?.Card;

        [GameEventParameter("是否超进化", "标识该进化是否来自超进化。")]
        public bool IsSuperEvolution { get; }

        public EvolveEvent(CardInstance evolvedCard, bool isSuperEvolution = false)
            : base(EventType.Evolve)
        {
            Source = TargetInfo.ForCard(evolvedCard);
            IsSuperEvolution = isSuperEvolution;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var cardToEvolve = EvolvedCard;

            if (cardToEvolve == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Card to evolve is null in EvolveEvent." };
            }

            // Perform the actual state change via entity's internal Apply method
            cardToEvolve.ApplyEvolutionChange(IsSuperEvolution);

            result.Success = true;
            return result;
        }
    }
}
