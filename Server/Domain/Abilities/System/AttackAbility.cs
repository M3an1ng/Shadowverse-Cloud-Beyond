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
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;

namespace ShadowverseBeyond.Server.Domain.Abilities.System
{
    public class AttackAbility : IAbility
    {
        private static readonly Guid _runtimeId = Guid.NewGuid();
        private readonly ILogger<AttackAbility> _logger;

        public Guid RuntimeId => _runtimeId;
        public long DefinitionId => GameConstants.SystemAbilityIds.AttackAbilityId;
        public string Name => "Attack";
        public EffectKeyword AbilityTag => EffectKeyword.System;
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

        public Guid? SourceCardId { get; set; }

        public CardInstance? SourceCard { get; set; }

        public Guid? SourceBuffId => null; // System abilities are not granted by buffs.

        public List<Runtime.Target> Targets => new List<Target>();

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

        public bool CheckConditions(GameContext context)
        {
            if (context.TriggeringEvent is not AttackRequestedEvent requestEvent)
            {
                _logger?.LogWarning("{AbilityName}: Triggering event is not AttackRequestedEvent. Event: {Event}", Name, context.TriggeringEvent?.GetType().Name);
                return false;
            }

            // Check if it's the requesting player's turn
            if (requestEvent.PlayerId != context.CurrentGameState.CurrentPlayerId)
            {
                _logger?.LogWarning("{AbilityName}: Player {RequestingPlayerId} attempted to attack, but it's not their turn (Active: {ActivePlayerId}).",
                    Name, requestEvent.PlayerId, context.CurrentGameState.CurrentPlayerId);
                return false;
            }

            PlayerState? attackerPlayerState = context.CurrentGameState.GetPlayerState(requestEvent.PlayerId);
            if (attackerPlayerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not find player state for Attacker PlayerId {PlayerId}.", Name, requestEvent.PlayerId);
                return false;
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
                    return false;
                }
            }

            var target = context.CurrentGameState.FindCardInstance(requestEvent.ActionDto.TargetUid);

            if (target == null)
            {
                _logger?.LogWarning("{AbilityName}: Target card with UID {TargetUid} not found.", Name, requestEvent.ActionDto.TargetUid);
                return false;
            }

            // Use the CardInstance's CanPerformAttack method for detailed checks
            if (!attacker.CanPerformAttack(context.CurrentGameState, attackerPlayerState, target))
            {
                _logger?.LogWarning("{AbilityName}: Attacker {AttackerName} (UID: {AttackerUid}) cannot perform attack based on its state.",
                   Name, attacker.Name, attacker.InstanceId);
                return false;
            }

            // Basic target existence check (target could be a unit or a player leader)
            var opponentPlayerState = context.CurrentGameState.GetOpponentPlayerState(requestEvent.PlayerId);
            if (opponentPlayerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not find opponent for player {PlayerId}.", Name, requestEvent.PlayerId);
                return false;
            }

            var targetUid = requestEvent.ActionDto.TargetUid;
            var targetIsOpponentLeader = opponentPlayerState.Leader?.InstanceId == targetUid;
            var targetIsOpponentUnitOnField = opponentPlayerState.Field.Any(c => c.InstanceId == targetUid);

            if (!targetIsOpponentLeader && !targetIsOpponentUnitOnField)
            {
                _logger?.LogWarning("{AbilityName}: Target UID {TargetUid} not found on opponent's field or as their leader.",
                    Name, targetUid);
                return false;
            }

            // More specific target validation (e.g., Ward, can't attack friendly unless specified)
            // should be handled by the AttackEffectHandler.

            _logger?.LogInformation("{AbilityName}: Conditions met for player {PlayerId} to attack with {AttackerUid} against {TargetUid}.",
                Name, requestEvent.PlayerId, requestEvent.ActionDto.AttackerUid, requestEvent.ActionDto.TargetUid);
            return true;
        }

        public Task<TargetResolutionResult> ResolveTargetsAsync(GameContext context, bool isCancellable = false)
        {
            _logger?.LogInformation("{AbilityName}: Targets are specified in the AttackRequestedEvent and resolved directly in CreateEffectsAsync.", Name);
            var result = new TargetResolutionResult { Status = ResolutionStatus.Completed };
            return Task.FromResult(result);
        }

        public Task<List<Effect>> CreateEffectsAsync(GameContext context, TargetResolutionResult targetResult)
        {
            var effects = new List<Effect>();

            if (context.TriggeringEvent is not AttackRequestedEvent requestEvent)
            {
                _logger?.LogError("{AbilityName}: Triggering event is not AttackRequestedEvent in CreateEffectsAsync.", Name);
                return Task.FromResult(effects);
            }

            var requestingPlayerId = requestEvent.PlayerId;
            var selectedTargetsForEffect = new List<TargetInfo>();
            var targetUid = requestEvent.ActionDto.TargetUid;

            // Find the target entity (card or player leader) and create the appropriate TargetInfo.
            var targetCard = context.CurrentGameState.FindCardInstance(targetUid);
            if (targetCard != null)
            {
                selectedTargetsForEffect.Add(TargetInfo.ForCard(targetCard));
            }
            else
            {
                var opponentPlayerState = context.CurrentGameState.GetOpponentPlayerState(requestingPlayerId);
                if (opponentPlayerState?.Leader?.InstanceId == targetUid)
                {
                    selectedTargetsForEffect.Add(TargetInfo.ForPlayer(opponentPlayerState));
                }
                else
                {
                    _logger?.LogError("{AbilityName}: Could not find target with UID {TargetUid} for attack effect.", Name, targetUid);
                    return Task.FromResult(effects); // Return empty list if target is not found
                }
            }

            // The new AttackEffectHandler gets all required info from the context and SelectedTargets.
            // The parameters in EffectConfig are now for behavioral modifiers, which can be empty for a standard attack.
            var effectConfig = new EffectConfig
            {
                Type = EffectType.Attack, // Corresponds to AttackEffectHandler
                Parameters = new Dictionary<string, ValueConfig>() // Empty, as we use default behaviors.
            };

            effects.Add(new Effect(
                effectConfig,
                requestingPlayerId,
                selectedTargetsForEffect,
                this.RuntimeId, // parentAbilityId
                context.TriggeringEvent?.EventId ?? Guid.Empty, // triggeredEventId
                requestEvent.ActionDto.AttackerUid // sourceCardId is the attacker
            ));

            _logger?.LogInformation("{AbilityName}: Created 'attack' effect for Attacker {AttackerUid} against Target {TargetUid}.",
                Name, requestEvent.ActionDto.AttackerUid, targetUid);

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
