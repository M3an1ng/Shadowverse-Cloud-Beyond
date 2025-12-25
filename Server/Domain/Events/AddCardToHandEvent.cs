using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("添加卡牌到手牌", "当卡牌被添加到玩家手牌时触发的事件。", EventType.AddCardToHand)]
    public class AddCardToHandEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("添加的卡牌", "被添加到手牌的卡牌列表。")]
        public List<CardInstance> CardsToAdd { get; }

        [GameEventParameter("目标玩家", "接收卡牌的玩家。")]
        public PlayerState? TargetPlayer => Target.FirstOrDefault()?.Player;

        public AddCardToHandEvent(PlayerState targetPlayer, List<CardInstance> cardsToAdd)
            : base(EventType.AddCardToHand)
        {
            Target.Add(TargetInfo.ForPlayer(targetPlayer));
            CardsToAdd = cardsToAdd ?? new List<CardInstance>();
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = TargetPlayer;
            if (player == null)
                return new DomainOperationResult { Success = false, ErrorMessage = "Target player is null in AddCardToHandEvent." };

            // The Commit method now only performs the final state change.
            // The card instances are already created by the handler.
            return player.AddCardsToHand(CardsToAdd);
        }

        public string GetAnnouncement(GameState gameState)
        {
            var playerId = TargetPlayer?.PlayerId;
            var count = CardsToAdd?.Count ?? 0;
            return playerId.HasValue
                ? $"add-card player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)} count={count}"
                : "add-card";
        }
    }
}
