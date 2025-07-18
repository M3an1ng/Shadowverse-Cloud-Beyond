using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters; // For AttackEffectParams and AttackTargetType
using ShadowverseBeyond.Server.Dtos; // For CardZone (assuming it's directly under Dtos)

namespace ShadowverseBeyond.Server.Domain.Abilities.System
{
    public class AttackAbility : IAbility
    {
        private static readonly Guid _runtimeId = Guid.NewGuid();
        private readonly ILogger<AttackAbility> _logger;

        public Guid RuntimeId => _runtimeId;
        public long DefinitionId => GameConstants.SystemAbilityIds.AttackAbilityId;
        public string Name => "Attack";
        public string AbilityTag => "System";
        public AbilityType Type => AbilityType.Triggered;

        public TriggerConfig TriggerConfig
        {
            get
            {
                return new TriggerConfig
                {
                    EventTypes = new List<EventType> { EventType.AttackRequested },
                };
            }
        }

        public CardInstance? SourceCard { get; set; }

        public AttackAbility(ILogger<AttackAbility> logger)
        {
            _logger = logger;
        }

        public AttackAbility()
        {
            _logger = null!;
        }

        public Task<bool> CanTrigger(IGameEvent gameEvent, GameContext context)
        {
            // AbilityManager filters by EventType based on this.TriggerConfig.
            // So, if this method is reached, gameEvent.EventType is AttackRequested.
            // No further event-specific checks needed here for this system ability.
            return Task.FromResult(true);
        }

        public Task<bool> CheckConditionsAsync(GameContext context)
        {
            if (context.TriggeringEvent is not AttackRequestedEvent requestEvent)
            {
                _logger?.LogWarning("{AbilityName}: Triggering event is not AttackRequestedEvent. Event: {Event}", Name, context.TriggeringEvent?.GetType().Name);
                return Task.FromResult(false);
            }

            // Check if it's the requesting player's turn
            if (requestEvent.PlayerId != context.CurrentGameState.CurrentPlayerId)
            {
                _logger?.LogWarning("{AbilityName}: Player {RequestingPlayerId} attempted to attack, but it's not their turn (Active: {ActivePlayerId}).",
                    Name, requestEvent.PlayerId, context.CurrentGameState.CurrentPlayerId);
                return Task.FromResult(false);
            }

            PlayerState? attackerPlayerState = context.CurrentGameState.GetPlayerState(requestEvent.PlayerId);
            if (attackerPlayerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not find player state for Attacker PlayerId {PlayerId}.", Name, requestEvent.PlayerId);
                return Task.FromResult(false);
            }

            CardInstance? attacker = attackerPlayerState.Field.FirstOrDefault(c => c.InstanceId == requestEvent.ActionDto.AttackerUid);
            if (attacker == null)
            {
                // Could also be the leader attacking, but leader attacks are usually implicit or handled differently.
                // For now, assume attacker must be a field unit for this system ability.
                if (attackerPlayerState.Leader?.InstanceId == requestEvent.ActionDto.AttackerUid)
                {
                    attacker = attackerPlayerState.Leader;
                }
                else
                {
                    _logger?.LogWarning("{AbilityName}: Attacker card with UID {AttackerUid} not found on player {PlayerId}'s field or as leader.",
                        Name, requestEvent.ActionDto.AttackerUid, requestEvent.PlayerId);
                    return Task.FromResult(false);
                }
            }

            // Use the CardInstance's CanPerformAttack method for detailed checks
            if (!attacker.CanPerformAttack(context.CurrentGameState, attackerPlayerState))
            {
                _logger?.LogWarning("{AbilityName}: Attacker {AttackerName} (UID: {AttackerUid}) cannot perform attack based on its state.",
                   Name, attacker.Name, attacker.InstanceId);
                return Task.FromResult(false);
            }

            // Basic target existence check (target could be a unit or a player leader)
            var opponentPlayerState = context.CurrentGameState.GetOpponentPlayerState(requestEvent.PlayerId);
            if (opponentPlayerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not find opponent for player {PlayerId}.", Name, requestEvent.PlayerId);
                return Task.FromResult(false);
            }

            var targetUid = requestEvent.ActionDto.TargetUid;
            var targetIsOpponentLeader = opponentPlayerState.Leader?.InstanceId == targetUid;
            var targetIsOpponentUnitOnField = opponentPlayerState.Field.Any(c => c.InstanceId == targetUid);

            if (!targetIsOpponentLeader && !targetIsOpponentUnitOnField)
            {
                _logger?.LogWarning("{AbilityName}: Target UID {TargetUid} not found on opponent's field or as their leader.",
                    Name, targetUid);
                return Task.FromResult(false);
            }

            // More specific target validation (e.g., Ward, can't attack friendly unless specified)
            // should be handled by the AttackEffectHandler.

            _logger?.LogInformation("{AbilityName}: Conditions met for player {PlayerId} to attack with {AttackerUid} against {TargetUid}.",
                Name, requestEvent.PlayerId, requestEvent.ActionDto.AttackerUid, requestEvent.ActionDto.TargetUid);
            return Task.FromResult(true);
        }

        public Task<List<TargetInfo>> ResolveTargetsAsync(GameContext context)
        {
            _logger?.LogInformation("{AbilityName}: Targets are specified in the AttackRequestedEvent.", Name);
            return Task.FromResult(new List<TargetInfo>());
        }

        public Task<List<Effect>> CreateEffectsAsync(GameContext context, List<TargetInfo> resolvedTargets)
        {
            var effects = new List<Effect>();

            if (context.TriggeringEvent is not AttackRequestedEvent requestEvent)
            {
                _logger?.LogError("{AbilityName}: Triggering event is not AttackRequestedEvent in CreateEffectsAsync.", Name);
                return Task.FromResult(effects);
            }

            // Determine TargetType
            AttackTargetType targetType;
            PlayerState? opponentPlayerState = context.CurrentGameState.GetOpponentPlayerState(requestEvent.PlayerId);

            if (opponentPlayerState?.Leader?.InstanceId == requestEvent.ActionDto.TargetUid)
            {
                targetType = AttackTargetType.Player;
            }
            else
            {
                targetType = AttackTargetType.Unit;
            }

            var attackParams = new AttackEffectParams
            {
                AttackerInstanceId = requestEvent.ActionDto.AttackerUid,
                TargetId = requestEvent.ActionDto.TargetUid,
                TargetType = targetType
            };

            var effectConfig = new EffectConfig
            {
                Type = "attack", // Corresponds to AttackEffectHandler
                Parameters = new Dictionary<string, ValueConfig>
                {
                    { nameof(AttackEffectParams.AttackerInstanceId), ValueConfig.Static(attackParams.AttackerInstanceId) },
                    { nameof(AttackEffectParams.TargetId), ValueConfig.Static(attackParams.TargetId) },
                    { nameof(AttackEffectParams.TargetType), ValueConfig.Static(attackParams.TargetType) }
                }
            };

            var requestingPlayerId = requestEvent.PlayerId;
            var selectedTargetsForEffect = new List<TargetInfo>();

            // We need to find the target card to add to the TargetInfo list for the effect.
            var targetCard = context.CurrentGameState.FindCardInstance(requestEvent.ActionDto.TargetUid);
            if (targetCard != null)
            {
                selectedTargetsForEffect.Add(TargetInfo.ForCard(targetCard));
            }

            effects.Add(new Effect(
                effectConfig,
                requestingPlayerId,
                selectedTargetsForEffect,
                this.RuntimeId, // parentAbilityId
                context.TriggeringEvent?.EventId ?? Guid.Empty, // triggeredEventId
                requestEvent.ActionDto.AttackerUid // sourceCardId
            ));
            _logger?.LogInformation("{AbilityName}: Added 'attack' effect for Attacker {AttackerUid} against Target {TargetUid} (Type: {TargetType}).",
                Name, requestEvent.ActionDto.AttackerUid, requestEvent.ActionDto.TargetUid, targetType);

            return Task.FromResult(effects);
        }

        public void RecordTrigger(GameContext context)
        {
            _logger?.LogDebug("{AbilityName} triggered.", Name);
        }

        public int GetTriggerCountThisTurn(GameContext context)
        {
            return 0;
        }

        public int GetTriggerCountThisGame(GameContext context)
        {
            return 0;
        }

        public void ResetTurnState(GameContext context)
        {
            _logger?.LogDebug("{AbilityName} turn state reset.", Name);
        }
    }
}