using System;
using System.Linq;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    [GameEventMetadata(
        "出牌请求",
        "当玩家请求打出卡牌时触发的事件。",
        EventType.PlayCardRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class PlayCardRequestedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("玩家ID", "发起出牌请求的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("出牌动作", "出牌动作的详细信息。")]
        public PlayCardActionDto ActionDto { get; }

        public PlayCardRequestedEvent(Guid playerId, PlayCardActionDto actionDto)
            : base(EventType.PlayCardRequested)
        {
            PlayerId = playerId;
            ActionDto = actionDto;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();

            // =================================================================
            // 1. Validation Logic from PlayCardAbility.CheckConditions
            // =================================================================

            if (PlayerId != context.CurrentGameState.CurrentPlayerId)
            {
                result.Success = false;
                result.ErrorMessage = "It's not your turn to play a card.";
                return result;
            }

            PlayerState? activePlayerState = context.CurrentGameState.GetPlayerState(PlayerId);
            if (activePlayerState == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Could not find player state for PlayerId {PlayerId}.";
                return result;
            }

            CardInstance? cardToPlay = activePlayerState.Hand.FirstOrDefault(c => c.InstanceId == ActionDto.CardUid);
            if (cardToPlay == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Card with UID {ActionDto.CardUid} not found in player {PlayerId}'s hand.";
                return result;
            }

            var abilityConfigs = cardToPlay.Abilities.Select(a => a.Config).ToList();
            var modeResolution = PlayModeResolver.Resolve(cardToPlay, activePlayerState, abilityConfigs);

            if (!modeResolution.HasAvailableMode)
            {
                result.Success = false;
                result.ErrorMessage = modeResolution.DefaultFailureReason ??
                    $"Player {PlayerId} does not have enough PP to play {cardToPlay.Name}.";
                return result;
            }

            var selectedMode = ActionDto.PlayMode ?? modeResolution.SelectedMode;
            if (!selectedMode.HasValue)
            {
                result.Success = false;
                result.ErrorMessage = modeResolution.DefaultFailureReason ?? "No available play mode could be selected.";
                return result;
            }

            var selectedAvailability = modeResolution.Modes.FirstOrDefault(m => m.Mode == selectedMode.Value);
            if (selectedAvailability == null || !selectedAvailability.IsAvailable)
            {
                result.Success = false;
                result.ErrorMessage = selectedAvailability?.Reason ?? "Selected play mode is not available.";
                return result;
            }

            var paidCost = ActionDto.PaidCost ?? selectedAvailability.EffectiveCost;
            if (paidCost != selectedAvailability.EffectiveCost)
            {
                result.Success = false;
                result.ErrorMessage = $"Play cost mismatch for {selectedMode.Value}. Expected {selectedAvailability.EffectiveCost}, got {paidCost}.";
                return result;
            }

            // TODO: Add more sophisticated playability checks here (e.g., board space, target validity)

            // =================================================================
            // 2. Event Creation Logic from PlayCardEffectHandler.Apply
            // =================================================================
            return activePlayerState.PreparePlayCard(cardToPlay, selectedMode.Value, paidCost);
        }
    }
}
