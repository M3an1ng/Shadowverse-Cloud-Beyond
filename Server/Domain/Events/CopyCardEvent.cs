using System;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("复制卡牌", "复制一张卡牌实例并放置到打出区。", EventType.CopyCard, visibility: EventMetadataVisibility.System)]
    public sealed class CopyCardEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("目标玩家", "复制卡牌的目标玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? TargetPlayer => Source?.Player;

        [GameEventParameter("复制源卡牌", "作为复制源的卡牌实例。", DslDisplayTypeName = "事件源")]
        public CardInstance? SourceCard { get; }

        [GameEventParameter("复制结果", "复制后的卡牌实例。", DslDisplayTypeName = "事件目标")]
        public CardInstance? CopiedCard => Target.FirstOrDefault()?.Card;

        public CopyCardEvent(PlayerState player, CardInstance sourceCard, CardInstance copiedCard, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.CopyCard, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            SourceCard = sourceCard;
            if (copiedCard != null)
            {
                Target.Add(TargetInfo.ForCard(copiedCard));
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var player = TargetPlayer;
            if (player == null)
            {
                result.Success = false;
                result.ErrorMessage = "copy-card 目标玩家不存在";
                return result;
            }

            var sourceCard = SourceCard;
            if (sourceCard == null)
            {
                result.Success = false;
                result.ErrorMessage = "copy-card 源卡牌不存在";
                return result;
            }

            var copiedCard = CopiedCard;
            if (copiedCard == null)
            {
                result.Success = false;
                result.ErrorMessage = "copy-card 复制目标不存在";
                return result;
            }

            var cardFactory = context.ServiceProvider.GetRequiredService<ICardFactory>();
            var copy = cardFactory.CreateAndRegisterCardCopy(
                context.CurrentGameState,
                sourceCard,
                player.PlayerId,
                CardZone.Staging,
                exactCopy: true,
                copyInstance: copiedCard);
            if (copy == null)
            {
                result.Success = false;
                result.ErrorMessage = "copy-card 复制失败";
                return result;
            }

            result.Success = true;
            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = TargetPlayer;
            var source = SourceCard;
            if (player == null || source == null)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Player(player)}复制了{AnnouncementTokens.Card(source)}。";
        }
    }
}
