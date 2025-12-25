using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a card enters the battlefield (summoned).
    /// This is the natural trigger point for Fanfare abilities.
    /// </summary>
    [GameEventMetadata("召唤", "当一张卡牌进入战场时触发的事件。", EventType.Summon)]
    public class SummonEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("召唤位置", "卡牌被召唤到的位置。")]
        public int? Position { get; }

        [GameEventParameter("召唤玩家", "执行召唤的玩家。")]
        public PlayerState? SummoningPlayer => Source?.Player;

        [GameEventParameter("被召唤的卡牌", "被召唤的卡牌。")]
        public CardInstance? SummonedCard => Target.FirstOrDefault()?.Card;

        public SummonEvent(PlayerState player, CardInstance card, int? position = null)
            : base(EventType.Summon)
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
            return player.SummonCardToField(cardToSummon, this.Position);
        }

        public string GetAnnouncement(GameState gameState)
        {
            var cardId = SummonedCard?.InstanceId;
            var playerId = SummoningPlayer?.PlayerId;
            if (!cardId.HasValue)
            {
                return "summon";
            }

            var cardToken = gameState.InGameIds.Format(InGameIdKind.Card, cardId.Value);
            return playerId.HasValue
                ? $"summon card={cardToken} player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)}"
                : $"summon card={cardToken}";
        }
    }
}
