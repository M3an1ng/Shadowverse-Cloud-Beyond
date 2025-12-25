using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 表示随从进行“超进化”的事件。超进化继承进化的触发，同时具备额外触发点。
    /// </summary>
    [GameEventMetadata("超进化", "当一张卡牌进行超进化时触发的事件。", EventType.SuperEvolve)]
    public class SuperEvolveEvent : GameEventBase, ICommittableEvent, IPlayerActionEvent
    {
        [GameEventParameter("超进化的卡牌", "被超进化的卡牌。")]
        public CardInstance? SuperEvolvedCard => Source?.Card;

        public SuperEvolveEvent(CardInstance card)
            : base(EventType.SuperEvolve)
        {
            Source = TargetInfo.ForCard(card);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var cardToEvolve = SuperEvolvedCard;

            if (cardToEvolve == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Card to super evolve is null in SuperEvolveEvent." };
            }

            cardToEvolve.ApplyEvolutionChange(isSuperEvolution: true);

            result.Success = true;
            return result;
        }
    }
}
