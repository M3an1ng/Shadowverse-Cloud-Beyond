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
        [GameEventParameter("添加的纹章", "被添加到纹章区的卡牌列表。")]
        public List<CardInstance> CardsToAdd { get; }

        [GameEventParameter("目标玩家", "接收纹章的玩家。")]
        public PlayerState? TargetPlayer => Target.FirstOrDefault()?.Player;

        public AddEmblemEvent(PlayerState targetPlayer, List<CardInstance> cardsToAdd)
            : base(EventType.AddEmblem)
        {
            Target.Add(TargetInfo.ForPlayer(targetPlayer));
            CardsToAdd = cardsToAdd ?? new List<CardInstance>();
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
            var playerId = TargetPlayer?.PlayerId;
            var count = CardsToAdd?.Count ?? 0;
            return playerId.HasValue
                ? $"add-emblem player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)} count={count}"
                : "add-emblem";
        }
    }
}
