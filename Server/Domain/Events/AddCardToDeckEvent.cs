using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("添加卡牌到牌库", "当卡牌被添加到玩家牌库时触发的事件。", EventType.AddDeck)]
    public class AddCardToDeckEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("添加的卡牌", "被添加到牌库的卡牌列表。")]
        public List<CardInstance> CardsToAdd { get; }

        [GameEventParameter("目标玩家", "接收卡牌的玩家。")]
        public PlayerState? TargetPlayer => Target.FirstOrDefault()?.Player;

        public AddCardToDeckEvent(PlayerState targetPlayer, List<CardInstance> cardsToAdd)
            : base(EventType.AddDeck)
        {
            Target.Add(TargetInfo.ForPlayer(targetPlayer));
            CardsToAdd = cardsToAdd ?? new List<CardInstance>();
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = TargetPlayer;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Target player is null in AddCardToDeckEvent." };
            }

            return player.AddCardsToDeck(CardsToAdd);
        }

        public string GetAnnouncement(GameState gameState)
        {
            var playerId = TargetPlayer?.PlayerId;
            var count = CardsToAdd?.Count ?? 0;
            return playerId.HasValue
                ? $"add-deck player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)} count={count}"
                : "add-deck";
        }
    }
}
