using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    /// <summary>
    /// A top-level committable event representing the player's request to use PP+1.
    /// </summary>
    [GameEventMetadata(
        "PP+1请求",
        "当玩家请求使用PP+1时触发的事件。",
        EventType.PpBoostRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class PpBoostRequestedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("玩家ID", "发起PP+1请求的玩家ID。")]
        public Guid PlayerId { get; }

        public PpBoostRequestedEvent(Guid playerId, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.PpBoostRequested, eventId, timestamp)
        {
            PlayerId = playerId;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var requestingPlayer = context.CurrentGameState.GetPlayerState(PlayerId);
            if (requestingPlayer == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Could not find player state for PlayerId {PlayerId}." };
            }

            if (requestingPlayer.IsFirst)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Only the second player can use PP+1." };
            }

            if (requestingPlayer.PlayerId != context.CurrentGameState.CurrentPlayerId)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "PP+1 can only be used on your own turn." };
            }

            if (context.CurrentGameState.Phase != GamePhase.TurnStart && context.CurrentGameState.Phase != GamePhase.Main)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "PP+1 can only be used during your turn." };
            }

            if (requestingPlayer.PpBoostUsesAvailable <= 0)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "No PP+1 uses available." };
            }

            var allowedMax = requestingPlayer.MaxPlayPoints + GameConstants.PlayPoints.PpBoostOverflowAmount;
            if (requestingPlayer.CurrentPlayPoints >= allowedMax)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "PP is already at the maximum allowed value." };
            }

            var targetCurrent = Math.Min(requestingPlayer.CurrentPlayPoints + GameConstants.PlayPoints.PpBoostAmount, allowedMax);
            result.GeneratedEvents.Add(new PpUpdateEvent(requestingPlayer, targetCurrent, allowOverflow: true));

            requestingPlayer.ApplyPpBoostUsesChange(requestingPlayer.PpBoostUsesAvailable - 1);
            result.GeneratedEvents.Add(new PpBoostStateUpdatedEvent(requestingPlayer, requestingPlayer.PpBoostUsesAvailable));

            result.Success = true;
            return result;
        }
    }
}
