using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    [GameEventMetadata(
        "换牌提交",
        "当玩家提交换牌选择时触发的事件。",
        EventType.MulliganSubmitted,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class MulliganSubmittedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("玩家ID", "提交换牌的玩家ID。", Scope = GameEventParameterScope.Input)]
        public Guid PlayerId { get; }

        [GameEventParameter("要替换的卡牌ID", "要替换的卡牌实例ID列表（向后兼容）。", Scope = GameEventParameterScope.Input)]
        public List<Guid> CardUidsToReplace { get; } // Made public setter private

        public MulliganSubmittedEvent(Guid playerId, List<CardInstance> cardsToReplace)
            : base(EventType.MulliganSubmitted)
        {
            PlayerId = playerId;
            var cardUids = new List<Guid>();
            foreach (var card in cardsToReplace ?? [])
            {
                Target.Add(TargetInfo.ForCard(card));
                cardUids.Add(card.InstanceId);
            }
            CardUidsToReplace = cardUids;
        }

        // 向后兼容的构造函数
        public MulliganSubmittedEvent(Guid playerId, List<Guid> cardUidsToReplace, GameContext context)
            : base(EventType.MulliganSubmitted)
        {
            PlayerId = playerId;
            CardUidsToReplace = cardUidsToReplace ?? new List<Guid>(); // Directly store the GUIDs

            // The logic to populate Target is for other systems that might inspect it,
            // but the primary data for Commit is now the raw GUID list.
            foreach (var cardId in CardUidsToReplace)
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
            var result = new DomainOperationResult();

            // =================================================================
            // 1. Validation Logic from MulliganAbility.CheckConditions
            // =================================================================

            if (context.CurrentGameState.Phase != GamePhase.Mulligan)
            {
                result.Success = false;
                result.ErrorMessage = $"MulliganSubmittedEvent received but game is not in Mulligan phase (Current: {context.CurrentGameState.Phase}).";
                return result;
            }

            PlayerState? playerState = context.CurrentGameState.GetPlayerState(PlayerId);
            if (playerState == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Could not find player state for PlayerId {PlayerId}.";
                return result;
            }

            if (playerState.HasCompletedMulligan)
            {
                result.Success = false;
                result.ErrorMessage = $"Player {PlayerId} has already completed mulligan.";
                return result;
            }

            // =================================================================
            // 2. Execution Logic from MulliganEffectHandler.Apply
            // =================================================================

            // BUG FIX: Use the directly stored CardUidsToReplace, not the computed property
            // that relies on the potentially empty Target list.
            return playerState.PerformMulligan(this.CardUidsToReplace, context.CurrentGameState);
        }
    }
}
