using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Domain.Metadata;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("添加纹章", "当纹章被添加到玩家纹章区时触发的事件。", EventType.AddEmblem, AbilityEditorCategories.PositionChangeId,
        AbilityEditorCategories.PositionChangeLabel)]
    public class AddEmblemEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("添加的纹章", "被添加到纹章区的卡牌列表。", DslDisplayTypeName = "事件目标")]
        public List<CardInstance> CardsToAdd => Target
            .Where(t => t.Card != null)
            .Select(t => t.Card!)
            .ToList();

        [GameEventParameter("目标玩家", "接收纹章的玩家。", DslDisplayTypeName = "事件源")]
        public PlayerState? TargetPlayer => Source?.Player;

        public AddEmblemEvent(PlayerState targetPlayer, List<CardInstance> cardsToAdd, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.AddEmblem, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(targetPlayer ?? throw new ArgumentNullException(nameof(targetPlayer)));
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
                return new DomainOperationResult { Success = false, ErrorMessage = "Target player is null in AddEmblemEvent." };
            }

            return player.AddCardsToEmblem(CardsToAdd);
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = TargetPlayer;
            var count = CardsToAdd?.Count ?? 0;
            if (player == null || count <= 0)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Player(player)}获得 {AnnouncementTokens.Number(count)} 个纹章。";
        }
    }
}
