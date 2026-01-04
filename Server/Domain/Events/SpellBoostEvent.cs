using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("魔力增幅", "当玩家使用法术后触发的增幅事件。", EventType.SpellBoost)]
    public class SpellBoostEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("施放玩家", "施放法术的玩家。")]
        public PlayerState? CastingPlayer => Source?.Player;

        [GameEventParameter("触发法术", "触发魔力增幅的法术。")]
        public CardInstance? SpellCard { get; }

        [GameEventParameter("被增幅卡牌", "获得魔力增幅计数的卡牌。")]
        public CardInstance? BoostedCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("旧计数", "增幅前的计数值。")]
        public int OldValue { get; }

        [GameEventParameter("新计数", "增幅后的计数值。")]
        public int NewValue { get; private set; }

        public SpellBoostEvent(PlayerState player, CardInstance spellCard, CardInstance boostedCard)
            : base(EventType.SpellBoost)
        {
            Source = TargetInfo.ForPlayer(player);
            Target.Add(TargetInfo.ForCard(boostedCard));
            SpellCard = spellCard;
            OldValue = boostedCard.GetCounter(CardInstance.SpellboostCounterKey);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = CastingPlayer;
            var boostedCard = BoostedCard;
            if (player == null || boostedCard == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "SpellBoostEvent.Commit: Missing player or boosted card." };
            }

            if (boostedCard.OwnerPlayerId != player.PlayerId || boostedCard.CurrentZone != CardZone.Hand)
            {
                return new DomainOperationResult { Success = true };
            }

            NewValue = boostedCard.AddCounter(CardInstance.SpellboostCounterKey, 1);
            return new DomainOperationResult { Success = true };
        }
    }
}
