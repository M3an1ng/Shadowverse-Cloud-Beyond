using System;
using System.Collections.Generic;
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
    /// Event triggered when a player draws one or more cards.
    /// This event contains the specific instances of the cards that were drawn.
    /// </summary>
    [GameEventMetadata("抽牌", "当玩家抽取一张或多张卡牌时触发的事件。", EventType.Draw)]
    public class DrawEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("抽牌玩家", "执行抽牌的玩家。", Scope = GameEventParameterScope.Input)]
        public PlayerState? DrawingPlayer => Source?.Player;

        [GameEventParameter("抽取的卡牌", "被抽取的卡牌列表。", Scope = GameEventParameterScope.Output)]
        public List<CardInstance> DrawnCards => Target.Where(t => t.Card != null).Select(t => t.Card!).ToList();

        [GameEventParameter("抽取的卡牌ID", "被抽取的卡牌实例ID列表（向后兼容）。", Scope = GameEventParameterScope.Output)]
        public List<Guid> DrawnCardInstanceIds => DrawnCards.Select(c => c.InstanceId).ToList();

        public DrawEvent(PlayerState player, List<CardInstance> drawnCards)
            : base(EventType.Draw)
        {
            Source = TargetInfo.ForPlayer(player);
            foreach (var card in drawnCards ?? new List<CardInstance>())
            {
                Target.Add(TargetInfo.ForCard(card));
            }
        }

        // 向后兼容的构造函数
        public DrawEvent(PlayerState player, List<Guid> drawnCardInstanceIds, GameContext context)
            : base(EventType.Draw)
        {
            Source = TargetInfo.ForPlayer(player);
            foreach (var cardId in drawnCardInstanceIds ?? new List<Guid>())
            {
                var card = context.CurrentGameState.FindCardInstance(cardId);
                if (card != null)
                {
                    Target.Add(TargetInfo.ForCard(card));
                }
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (DrawingPlayer == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "DrawEvent source player is null." };
            }

            var player = DrawingPlayer;
            // DrawEvent 必须包含固定的卡牌实例ID；Commit 仅按指定ID执行抽牌移动
            return player.DrawCards(DrawnCardInstanceIds);
        }

        public string GetAnnouncement(GameState gameState)
        {
            var playerId = DrawingPlayer?.PlayerId;
            var count = DrawnCardInstanceIds.Count;
            return playerId.HasValue
                ? $"draw player={gameState.InGameIds.Format(InGameIdKind.Player, playerId.Value)} count={count}"
                : "draw";
        }
    }
}
