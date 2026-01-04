using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event committed when a player finalizes mulligan.
    /// Implements ICommittableEvent to apply mulligan results and transition readiness.
    /// </summary>
    [GameEventMetadata("调度完成", "当玩家完成调度操作时触发的事件。", EventType.MulliganCompleted)]
    public class MulliganCompletedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("换牌玩家", "执行换牌操作的玩家。")]
        public PlayerState? MulliganPlayer => Source?.Player;

        [GameEventParameter("替换的卡牌", "被替换的卡牌列表。")]
        public List<CardInstance> ReplacedCards => Target.Where(t => t.Card != null).Select(t => t.Card!).ToList();

        public MulliganCompletedEvent(PlayerState player, List<CardInstance> replacedCards)
            : base(EventType.MulliganCompleted)
        {
            Source = TargetInfo.ForPlayer(player);
            foreach (var card in replacedCards ?? [])
            {
                Target.Add(TargetInfo.ForCard(card));
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var player = MulliganPlayer;
            var gameState = context.CurrentGameState;
            if (player == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "MulliganCompletedEvent.Commit: Source player is null." };
            }

            // Finalize mulligan via internal apply method
            player.ApplyMulliganCompleted();

            if (gameState.AreBothMulligansCompleted())
            {
                // Transition to Main phase and set current player to the first player.
                gameState.TransitionFromMulliganToMain();

                // Start the first player's turn by enqueuing a TurnStartEvent for the FirstPlayer
                var firstPlayerId = gameState.FirstPlayerId;
                if (firstPlayerId.HasValue)
                {
                    var firstPlayer = gameState.GetPlayerState(firstPlayerId.Value);
                    if (firstPlayer != null)
                    {
                        result.GeneratedEvents.Add(new TurnStartEvent(firstPlayer));
                    }
                }
            }

            result.Success = true;
            return result;
        }
    }
}

