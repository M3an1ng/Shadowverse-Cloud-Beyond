using System;
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
        [GameEventParameter("添加的卡牌", "被添加到牌库的卡牌列表。", DslDisplayTypeName = "事件目标")]
        public List<CardInstance> CardsToAdd => Target
            .Where(t => t.Card != null)
            .Select(t => t.Card!)
            .ToList();

        [GameEventParameter("目标玩家", "接收卡牌的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? TargetPlayer => Source?.Player;

        [GameEventParameter("插入方式", "卡牌插入的位置")]
        public DeckInsertMode InsertMode { get; }

        public AddCardToDeckEvent(
            PlayerState targetPlayer,
            List<CardInstance> cardsToAdd,
            DeckInsertMode insertMode = DeckInsertMode.Random,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.AddDeck, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(targetPlayer);
            InsertMode = insertMode;
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
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Target player is null in AddCardToDeckEvent." };
            }

            return player.AddCardsToDeck(CardsToAdd, InsertMode);
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = TargetPlayer;
            var count = CardsToAdd?.Count ?? 0;
            if (player == null || count <= 0)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Player(player)}将 {AnnouncementTokens.Number(count)} 张牌加入牌库。";
        }
    }
}
