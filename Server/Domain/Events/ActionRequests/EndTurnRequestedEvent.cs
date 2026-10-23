using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.GameFlow;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    [GameEventMetadata(
        "结束回合请求",
        "当玩家请求结束回合时触发的事件。",
        EventType.EndTurnRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class EndTurnRequestedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("玩家ID", "发起结束回合请求的玩家ID。")]
        public Guid PlayerId { get; }

        public EndTurnRequestedEvent(Guid playerId, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.EndTurnRequested, eventId, timestamp)
        {
            PlayerId = playerId;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();

            // =================================================================
            // 1. Validation Logic from EndTurnAbility.CheckConditions
            // =================================================================

            if (PlayerId != context.CurrentGameState.CurrentPlayerId)
            {
                result.Success = false;
                result.ErrorMessage = $"Player {PlayerId} attempted to end turn, but active player is {context.CurrentGameState.CurrentPlayerId}.";
                return result;
            }

            if (context.CurrentGameState.IsGameOver)
            {
                result.Success = false;
                result.ErrorMessage = "Game is already over. Cannot end turn.";
                return result;
            }

            // =================================================================
            // 2. Event Creation Logic from EndTurnAbility.CreateEffectsAsync
            // =================================================================

            var currentPlayerState = context.CurrentGameState.GetPlayerState(PlayerId);
            if (currentPlayerState == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Could not find current player state for PlayerId {PlayerId}.";
                return result;
            }

            var nextPlayerState = context.CurrentGameState.GetOpponentPlayerState(PlayerId);
            if (nextPlayerState == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Could not determine the next player. CurrentPlayerId: {PlayerId}";
                return result;
            }

            // Generate the two events that represent the full turn transition
            var turnEndEvent = new TurnEndEvent(currentPlayerState);
            result.GeneratedEvents.Add(turnEndEvent);
            result.GeneratedEvents.Add(new TurnBoundaryCleanupEvent(
                currentPlayerState,
                TurnBoundaryCleanupTiming.TurnEnd));
            var turnStartTransitionResult = nextPlayerState.BuildTurnStartTransitionEvents();
            result.GeneratedEvents.AddRange(turnStartTransitionResult.GeneratedEvents);
            result.Success = true;

            return result;
        }
    }
}
