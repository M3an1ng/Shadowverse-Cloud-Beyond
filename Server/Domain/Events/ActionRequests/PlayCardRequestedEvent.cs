using System;
using System.Linq;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos.Enums;

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

        public PlayCardRequestedEvent(Guid playerId, PlayCardActionDto actionDto, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.PlayCardRequested, eventId, timestamp)
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

            if (cardToPlay.HasKeyword(CardKeyword.CannotUse))
            {
                result.Success = false;
                result.ErrorMessage = "该卡牌处于【无法使用】状态。";
                return result;
            }

            Ability? selectedAbility = null;
            if (ActionDto.AbilityRuntimeId.HasValue && ActionDto.AbilityRuntimeId.Value != Guid.Empty)
            {
                selectedAbility = cardToPlay.Abilities.FirstOrDefault(a => a.RuntimeId == ActionDto.AbilityRuntimeId.Value);
                if (selectedAbility == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Selected ability not found on the card.";
                    return result;
                }
            }
            var modeResolution = PlayModeResolver.Resolve(cardToPlay, activePlayerState, context);

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

            if (selectedAbility == null && selectedMode.Value == PlayMode.Enhance && modeResolution.SelectedAbilityRuntimeId.HasValue)
            {
                var selectedRuntimeId = modeResolution.SelectedAbilityRuntimeId.Value;
                selectedAbility = cardToPlay.Abilities.FirstOrDefault(a => a.RuntimeId == selectedRuntimeId)
                    ?? context.AbilityManager.GetAbilityByRuntimeId(selectedRuntimeId) as Ability;
            }

            var selectedAvailability = modeResolution.Modes.FirstOrDefault(m => m.Mode == selectedMode.Value);
            if (selectedAvailability == null || !selectedAvailability.IsAvailable)
            {
                result.Success = false;
                result.ErrorMessage = selectedAvailability?.Reason ?? "Selected play mode is not available.";
                return result;
            }

            var paidCost = ActionDto.PaidCost ?? selectedAvailability.EffectiveCost;
            var expectedCost = selectedAvailability.EffectiveCost;
            if (selectedMode.Value == PlayMode.Enhance)
            {
                if (selectedAbility == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Selected Enhance ability could not be resolved.";
                    return result;
                }

                var enhanceCost = cardToPlay.GetEnhanceCostForAbility(selectedAbility, context);
                if (enhanceCost <= 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "Selected ability does not provide a valid Enhance cost.";
                    return result;
                }

                var isEnhanceDisabled = false;
                if (cardToPlay.AbilityState.TryGetValue(PlayModeResolver.EnhanceDisabledKey, out var value) && value != null)
                {
                    isEnhanceDisabled = value switch
                    {
                        bool b => b,
                        int i => i != 0,
                        long l => l != 0,
                        string s => bool.TryParse(s, out var parsed) && parsed,
                        _ => false
                    };
                }
                if (isEnhanceDisabled)
                {
                    result.Success = false;
                    result.ErrorMessage = "Enhance disabled.";
                    return result;
                }

                if (activePlayerState.CurrentPlayPoints < enhanceCost)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Not enough PP for Enhance (need {enhanceCost}, have {activePlayerState.CurrentPlayPoints}).";
                    return result;
                }

                paidCost = enhanceCost;
                expectedCost = enhanceCost;
            }
            if (paidCost != expectedCost)
            {
                result.Success = false;
                result.ErrorMessage = $"Play cost mismatch for {selectedMode.Value}. Expected {expectedCost}, got {paidCost}.";
                return result;
            }

            // TODO: Add more sophisticated playability checks here (e.g., board space, target validity)

            // =================================================================
            // 2. Event Creation Logic from PlayCardEffectHandler.Apply
            // =================================================================
            var playResult = activePlayerState.PreparePlayCard(cardToPlay, selectedMode.Value, paidCost);
            if (playResult.Success && selectedMode.Value == PlayMode.Enhance && selectedAbility != null)
            {
                playResult.GeneratedEvents.Insert(0, new EnhanceEvent(activePlayerState, cardToPlay, paidCost, selectedAbility.RuntimeId));
            }
            return playResult;
        }
    }
}
