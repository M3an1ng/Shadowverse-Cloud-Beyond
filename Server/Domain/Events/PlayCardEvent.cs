using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Player plays a card from hand. This is a player action event and a committable command.
    /// Commit() applies all state changes for playing the card.
    /// </summary>
    [GameEventMetadata("出牌", "当玩家从手牌中打出一张卡牌时触发的事件。", EventType.PlayCard)]
    public class PlayCardEvent : GameEventBase, ICommittableEvent, IPlayerActionEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("出牌玩家", "打出卡牌的玩家。")]
        public PlayerState? PlayingPlayer => Source?.Player;

        [GameEventParameter("被打出的卡牌", "被打出的卡牌。")]
        public CardInstance? PlayedCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("出牌模式", "本次出牌使用的模式。")]
        public PlayMode PlayMode { get; }

        [GameEventParameter("支付费用", "本次出牌实际消耗的费用。")]
        public int PaidCost { get; }

        public PlayCardEvent(PlayerState player, CardInstance card, PlayMode playMode = PlayMode.Base, int? paidCost = null)
            : base(EventType.PlayCard)
        {
            Source = TargetInfo.ForPlayer(player);
            Target.Add(TargetInfo.ForCard(card));
            PlayMode = playMode;
            PaidCost = paidCost ?? card.CurrentCost;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var player = PlayingPlayer;
            var card = PlayedCard;
            if (player == null || card == null)
            {
                result.Success = false;
                result.ErrorMessage = "PlayCardEvent.Commit: Missing player or card.";
                return result;
            }

            // Delegate to domain method that performs the play and generates factual events.
            // This method is expected to validate, apply PP cost, move card to appropriate zone,
            // and generate resulting events like HandUpdatedEvent, CardMovedEvent, Summon/Spell resolution events, etc.
            return player.ExecutePlayCard(card, PaidCost);
        }

        public string GetAnnouncement(GameState gameState)
        {
            var cardId = PlayedCard?.InstanceId;
            var playerId = PlayingPlayer?.PlayerId;
            if (!cardId.HasValue)
            {
                return "play-card";
            }

            var cardToken = gameState.InGameIds.Format(InGameIdKind.Card, cardId.Value);
            return playerId.HasValue
                ? $"play-card card={cardToken} player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)}"
                : $"play-card card={cardToken}";
        }
    }
}
