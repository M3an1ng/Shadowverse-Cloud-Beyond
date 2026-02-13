using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a card enters the battlefield (summoned).
    /// This is the natural trigger point for Fanfare abilities.
    /// </summary>
    [GameEventMetadata("召唤", "当一张卡牌进入战场时触发的事件。", EventType.Summon)]
    public class SummonEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("召唤位置", "卡牌被召唤到的位置。", Scope = GameEventParameterScope.Both)]
        public int? Position { get; private set; }

        [GameEventParameter("召唤玩家", "执行召唤的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? SummoningPlayer => Source?.Player;

        [GameEventParameter("被召唤的卡牌", "被召唤的卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? SummonedCard => Target.FirstOrDefault()?.Card;

        public SummonEvent(PlayerState player, CardInstance card, int? position = null, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.Summon, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            Target.Add(TargetInfo.ForCard(card));
            Position = position;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (Source?.Player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "SummonEvent source player is null." };
            }
            var cardToSummon = Target.FirstOrDefault()?.Card;
            if (cardToSummon == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "SummonEvent target card is null." };
            }

            var player = Source.Player;

            // This method now returns the result of the domain logic,
            // which should include the CardMovedEvent.
            var result = player.SummonCardToField(cardToSummon, null);
            if (result.Success)
            {
                Position = ResolveSummonPosition(player, cardToSummon);
                if (cardToSummon.CardType == CardType.Follower && cardToSummon.CurrentHealth <= 0)
                    result.GeneratedEvents.Add(new DestroyEvent(cardToSummon, cardToSummon, EventReasonSource.System));
                else if ((cardToSummon.CardType == CardType.Amulet || cardToSummon.CardType == CardType.Crest) && cardToSummon.Keywords.Contains(Constants.CardKeyword.Countdown) && cardToSummon.Countdown <= 0)
                    result.GeneratedEvents.Add(new DestroyEvent(cardToSummon, cardToSummon, EventReasonSource.System));
            }

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var card = SummonedCard;
            var player = SummoningPlayer;
            if (card == null)
            {
                return string.Empty;
            }

            return player != null
                ? $"{AnnouncementTokens.Player(player)}召唤{AnnouncementTokens.Card(card)}。"
                : $"{AnnouncementTokens.Card(card)}被召唤。";
        }

        private static int? ResolveSummonPosition(PlayerState player, CardInstance card)
        {
            if (card.CurrentZone == CardZone.Field && card.ZoneIndex.HasValue)
            {
                return card.ZoneIndex.Value + 1;
            }

            if (card.CurrentZone == CardZone.Field)
            {
                var index = player.Field.IndexOf(card);
                if (index >= 0)
                {
                    return index + 1;
                }
            }

            return null;
        }
    }
}
