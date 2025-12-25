using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos.Enums;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("创建卡牌", "创建一张指定定义的卡牌实例并放置到指定区域。", EventType.CreateCard)]
    public sealed class CreateCardEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("目标玩家ID", "接收卡牌的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("卡牌定义ID", "要创建的卡牌定义ID。")]
        public int CardDefinitionId { get; }

        [GameEventParameter("放置区域", "创建后放置到的区域（Hand/Field/Deck）。")]
        public CardZone Location { get; }

        public CreateCardEvent(Guid playerId, int cardDefinitionId, CardZone location)
            : base(EventType.CreateCard)
        {
            PlayerId = playerId;
            CardDefinitionId = cardDefinitionId;
            Location = location;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();

            var gameState = context.CurrentGameState;
            var player = gameState.GetPlayerState(PlayerId);
            if (player == null)
            {
                result.Success = false;
                result.ErrorMessage = "create-card 玩家不存在";
                return result;
            }

            Source = TargetInfo.ForPlayer(player);

            var cardFactory = context.ServiceProvider.GetRequiredService<ICardFactory>();
            var card = cardFactory.CreateAndRegisterNewCard(gameState, CardDefinitionId, player.PlayerId, CardZone.Deck);
            if (card == null)
            {
                result.Success = false;
                result.ErrorMessage = "create-card 卡牌创建失败";
                return result;
            }

            result.Success = true;
            result.GeneratedEvents.AddRange(Location switch
            {
                CardZone.Hand => new IGameEvent[] { new AddCardToHandEvent(player, new List<CardInstance> { card }) },
                CardZone.Field => new IGameEvent[] { new SummonEvent(player, card) },
                CardZone.Deck => new IGameEvent[] { new DeckCountChangedEvent(player, player.Deck.Count) },
                _ => new IGameEvent[] { new AddCardToHandEvent(player, new List<CardInstance> { card }) }
            });

            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            return $"create-card player={gameState.InGameIds.Format(InGameIdKind.Player, PlayerId)} card={CardDefinitionId} location={Location}";
        }
    }
}
