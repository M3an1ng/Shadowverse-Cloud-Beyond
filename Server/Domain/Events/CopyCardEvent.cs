using System.Linq;
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
        [GameEventParameter("目标玩家", "复制卡牌的目标玩家。")]
        public PlayerState? TargetPlayer => Source?.Player;

        [GameEventParameter("复制源卡牌", "作为复制源的卡牌实例。")]
        public CardInstance? SourceCard => Target.FirstOrDefault()?.Card;

        [GameEventParameter("复制结果", "复制后的卡牌实例。")]
        public CardInstance? CopiedCard { get; }

        public CopyCardEvent(PlayerState player, CardInstance sourceCard, CardInstance copiedCard)
            : base(EventType.CopyCard)
        {
            Source = TargetInfo.ForPlayer(player);
            Target.Add(TargetInfo.ForCard(sourceCard));
            CopiedCard = copiedCard;
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
                CardZone.Playing,
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
            var playerId = TargetPlayer?.PlayerId;
            var sourceId = SourceCard?.InstanceId;
            return playerId.HasValue && sourceId.HasValue
                ? $"copy-card player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)} source={gameState.InGameIds.Format(InGameIdKind.Card, sourceId.Value)}"
                : "copy-card";
        }
    }
}
