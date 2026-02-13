using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// High-level shuffle event: move cards from hand into their owner's deck and shuffle.
    /// </summary>
    [GameEventMetadata("洗回牌堆", "当手牌被洗回牌库并洗牌时触发的事件。", EventType.Shuffle)]
    public class ShuffleEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("洗回原因", "卡牌被洗回牌库的原因。")]
        public EventReasonSource Reason { get; }

        [GameEventParameter("洗回的卡牌", "被洗回牌库的卡牌列表。", DslDisplayTypeName = "事件目标")]
        public List<CardInstance> ShuffledCards => Target.Where(t => t.Card != null).Select(t => t.Card!).ToList();

        [GameEventParameter("目标玩家", "接收卡牌的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? TargetPlayer => Source?.Player;

        public ShuffleEvent(PlayerState player, List<CardInstance> cards, EventReasonSource reason = EventReasonSource.Ability, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Shuffle, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            Reason = reason;

            foreach (var card in cards ?? new List<CardInstance>())
            {
                Target.Add(TargetInfo.ForCard(card));
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var player = TargetPlayer;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "ShuffleEvent.Commit: target player is null." };
            }

            return player.ShuffleCardsIntoDeck(ShuffledCards);
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = TargetPlayer;
            var count = ShuffledCards.Count;
            if (player == null || count <= 0)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Player(player)}将 {AnnouncementTokens.Number(count)} 张牌洗回牌库。";
        }
    }
}
