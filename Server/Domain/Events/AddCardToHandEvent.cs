using System;
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
        [GameEventParameter("添加的卡牌", "被添加到手牌的卡牌列表。", DslDisplayTypeName = "事件目标")]
        public List<CardInstance> CardsToAdd => Target
            .Where(t => t.Card != null)
            .Select(t => t.Card!)
            .ToList();

        [GameEventParameter("目标玩家", "接收卡牌的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? TargetPlayer => Source?.Player;

        public AddCardToHandEvent(PlayerState targetPlayer, List<CardInstance> cardsToAdd, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.AddCardToHand, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(targetPlayer);
            foreach (var card in cardsToAdd ?? new List<CardInstance>())
            {
                if (card != null)
                {
                    Target.Add(TargetInfo.ForCard(card));
                }
            }
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
            var player = TargetPlayer;
            var count = CardsToAdd?.Count ?? 0;
            if (player == null || count <= 0)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Player(player)}获得 {AnnouncementTokens.Number(count)} 张手牌。";
        }
    }
}
