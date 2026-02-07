using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos.Enums;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("创建卡牌", "创建一张指定定义的卡牌实例。", EventType.CreateCard)]
    public sealed class CreateCardEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("目标玩家", "创建卡牌的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? TargetPlayer => Source?.Player;

        [GameEventParameter("卡牌定义ID", "要创建的卡牌定义ID。")]
        public int CardDefinitionId { get; }

        public CreateCardEvent(PlayerState player, int cardDefinitionId, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.CreateCard, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            CardDefinitionId = cardDefinitionId;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();

            var player = TargetPlayer;
            if (player == null)
            {
                result.Success = false;
                result.ErrorMessage = "create-card 玩家不存在";
                return result;
            }

            var cardFactory = context.ServiceProvider.GetRequiredService<ICardFactory>();
            var card = cardFactory.CreateAndRegisterNewCard(context.CurrentGameState, CardDefinitionId, player.PlayerId, CardZone.Staging);
            if (card == null)
            {
                result.Success = false;
                result.ErrorMessage = "create-card 卡牌创建失败";
                return result;
            }

            result.Success = true;
            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = TargetPlayer;
            var definitionName = GameDataProvider.Instance?.GetCardDefinition(CardDefinitionId)?.Name;
            var cardToken = AnnouncementTokens.CardName(definitionName);

            return player != null
                ? $"{AnnouncementTokens.Player(player)}创建了{cardToken}。"
                : $"{cardToken}被创建。";
        }
    }
}
