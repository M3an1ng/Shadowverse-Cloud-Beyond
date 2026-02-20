using System;
using System.Collections.Generic;
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
        [GameEventParameter("施放玩家", "施放法术的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? CastingPlayer => Source?.Player;

        [GameEventParameter("触发法术", "触发魔力增幅的法术。")]
        public CardInstance? SpellCard { get; }

        [GameEventParameter("被增幅卡牌", "获得魔力增幅计数的卡牌列表。", DslDisplayTypeName = "事件目标")]
        public List<CardInstance> BoostedCards => Target.Where(t => t.Card != null).Select(t => t.Card!).ToList();

        public SpellBoostEvent(PlayerState player, CardInstance spellCard, IReadOnlyList<CardInstance> boostedCards, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.SpellBoost, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            SpellCard = spellCard;
            foreach (var card in boostedCards ?? Array.Empty<CardInstance>())
            {
                Target.Add(TargetInfo.ForCard(card));
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = CastingPlayer;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "SpellBoostEvent.Commit: Missing player." };
            }

            var result = new DomainOperationResult { Success = true };
            foreach (var boostedCard in BoostedCards)
            {
                if (boostedCard.OwnerPlayerId != player.PlayerId || boostedCard.CurrentZone != CardZone.Hand)
                {
                    continue;
                }

                if(boostedCard.AbilityKeywords.Contains(Constants.AbilityKeyword.SpellBoost)){
                    var oldValue = boostedCard.GetCounter(CardInstance.SpellboostCounterKey);
                    var newValue = boostedCard.AddCounter(CardInstance.SpellboostCounterKey, 1);
                    if (newValue != oldValue)
                    {
                        result.GeneratedEvents.Add(new CounterChangedEvent(boostedCard, CardInstance.SpellboostCounterKey, oldValue, newValue));
                    }
                }
            }

            return result;
        }
    }
}
