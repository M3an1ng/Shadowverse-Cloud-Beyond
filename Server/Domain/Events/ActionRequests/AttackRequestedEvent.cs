using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Domain.Entities;
using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Constants;
using System.Collections.Generic;
using AutoMapper;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    /// <summary>
    /// A top-level committable event representing a player's request to perform an attack.
    /// This event encapsulates the entire attack process, from validation to damage calculation.
    /// </summary>
    [GameEventMetadata(
        "攻击请求",
        "当玩家请求进行攻击时触发的事件。",
        EventType.AttackRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.Hidden)]
    public class AttackRequestedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("玩家ID", "发起攻击请求的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("攻击动作", "攻击动作的详细信息。")]
        public AttackActionDto ActionDto { get; }

        public AttackRequestedEvent(Guid playerId, AttackActionDto actionDto, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.AttackRequested, eventId, timestamp) // Use Attack event type for triggering purposes
        {
            PlayerId = playerId;
            ActionDto = actionDto;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();
            var playerId = this.PlayerId;

            // 1. Resolve entities
            var attackerPlayer = context.CurrentGameState.GetPlayerState(playerId);
            var opponentPlayer = context.CurrentGameState.GetOpponentPlayerState(playerId);

            if (attackerPlayer == null || opponentPlayer == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Could not find player or opponent state." };
            }

            var attacker = context.CurrentGameState.FindCardInstance(this.ActionDto.AttackerUid);
            var target = context.CurrentGameState.FindCardInstance(this.ActionDto.TargetUid);

            // 2. Pre-condition Checks (migrated from AttackAbility)
            if (attacker == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Attacker card with UID {this.ActionDto.AttackerUid} not found." };
            }
            if (target == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Target card with UID {this.ActionDto.TargetUid} not found." };
            }

            // Check if it's the requesting player's turn
            if (playerId != context.CurrentGameState.CurrentPlayerId)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "It's not your turn to attack." };
            }

            // Use the CardInstance's CanPerformAttack method for detailed checks
            if (!attacker.CanPerformAttack(context.CurrentGameState, attackerPlayer, target))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Attacker {attacker.Name} (UID: {attacker.InstanceId}) cannot perform attack based on its state." };
            }

            // Target validation
            if (target.OwnerPlayerId != opponentPlayer.PlayerId)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Target UID {target.InstanceId} not found on opponent's side." };
            }

            // This now returns a result containing the generated events.
            var declaredResult = attacker.PerformAttackDeclared(target);
            if (!declaredResult.Success)
            {
                // Propagate the failure result.
                return declaredResult;
            }

            // Add the generated events to the current transaction's result.
            result.GeneratedEvents.AddRange(declaredResult.GeneratedEvents);
            result.Success = true;

            return result;
        }

        public GameEventDto? ToDto(IMapper mapper)
        {
            return null; // This is a command, not for broadcast.
        }
    }
}
