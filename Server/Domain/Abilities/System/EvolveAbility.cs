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
using ShadowverseBeyond.Server.Dtos.Enums; // For CardZone
// EvolveEffectParams is in ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters, but it's empty.

namespace ShadowverseBeyond.Server.Domain.Abilities.System
{
    public class EvolveAbility : IAbility
    {
        private static readonly Guid _runtimeId = Guid.NewGuid();
        private readonly ILogger<EvolveAbility> _logger;

        public Guid RuntimeId => _runtimeId;
        public long DefinitionId => GameConstants.SystemAbilityIds.EvolveAbilityId;
        public string Name => "Evolve";
        public string AbilityTag => "System";
        public AbilityType Type => AbilityType.Triggered;

        public TriggerConfig TriggerConfig
        {
            get
            {
                return new TriggerConfig
                {
                    EventTypes = new List<EventType> { EventType.EvolveRequested },
                };
            }
        }

        public CardInstance? SourceCard { get; set; }

        public EvolveAbility(ILogger<EvolveAbility> logger)
        {
            _logger = logger;
        }

        public EvolveAbility()
        {
            _logger = null!;
        }

        public Task<bool> CanTrigger(IGameEvent gameEvent, GameContext context)
        {
            // AbilityManager filters by EventType based on this.TriggerConfig.
            // So, if this method is reached, gameEvent.EventType is EvolveRequested.
            // No further event-specific checks needed here for this system ability.
            return Task.FromResult(true);
        }

        public Task<bool> CheckConditionsAsync(GameContext context)
        {
            if (context.TriggeringEvent is not EvolveRequestedEvent requestEvent)
            {
                _logger?.LogWarning("{AbilityName}: Triggering event is not EvolveRequestedEvent. Event: {Event}", Name, context.TriggeringEvent?.GetType().Name);
                return Task.FromResult(false);
            }

            if (requestEvent.PlayerId != context.CurrentGameState.CurrentPlayerId)
            {
                _logger?.LogWarning("{AbilityName}: Player {RequestingPlayerId} attempted to evolve unit, but it's not their turn (Active: {ActivePlayerId}).",
                    Name, requestEvent.PlayerId, context.CurrentGameState.CurrentPlayerId);
                return Task.FromResult(false);
            }

            PlayerState? evolverPlayerState = context.CurrentGameState.GetPlayerState(requestEvent.PlayerId);
            if (evolverPlayerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not find player state for PlayerId {PlayerId}.", Name, requestEvent.PlayerId);
                return Task.FromResult(false);
            }

            if (!Guid.TryParse(requestEvent.ActionDto.UnitUid, out Guid unitGuidToEvolve))
            {
                _logger?.LogWarning("{AbilityName}: Invalid UnitUid format in EvolveActionDto: {UnitUid}", Name, requestEvent.ActionDto.UnitUid);
                return Task.FromResult(false);
            }

            CardInstance? cardToEvolve = evolverPlayerState.Field.FirstOrDefault(c => c.InstanceId == unitGuidToEvolve);
            if (cardToEvolve == null)
            {
                _logger?.LogWarning("{AbilityName}: Unit with UID {UnitUid} not found on player {PlayerId}'s field for evolution.",
                    Name, requestEvent.ActionDto.UnitUid, requestEvent.PlayerId);
                return Task.FromResult(false);
            }

            // 1. Check intrinsic card properties (using the new pure method)
            if (!cardToEvolve.CanEvolve())
            {
                _logger?.LogWarning("{AbilityName}: Unit {UnitName} (UID: {UnitUid}) cannot evolve intrinsically (already evolved or no form).",
                    Name, cardToEvolve.Name, cardToEvolve.InstanceId);
                return Task.FromResult(false);
            }

            // 2. Check game rule conditions (EP, turn number, player's right to evolve)
            if (evolverPlayerState.CurrentEvolutionPoints < GameConstants.Evolution.EvolutionPointCost)
            {
                _logger?.LogWarning("{AbilityName}: Player {PlayerId} does not have enough EP to evolve. Has: {EP}, Needs: {Cost}",
                    Name, requestEvent.PlayerId, evolverPlayerState.CurrentEvolutionPoints, GameConstants.Evolution.EvolutionPointCost);
                return Task.FromResult(false);
            }

            if (!evolverPlayerState.CanEvolve)
            {
                _logger?.LogWarning("{AbilityName}: Player {PlayerId} has already used their evolution right this turn.",
                    Name, requestEvent.PlayerId);
                return Task.FromResult(false);
            }

            int evolutionStartTurn = evolverPlayerState.IsFirst ? GameConstants.Evolution.FirstPlayerEvolutionStartTurn : GameConstants.Evolution.SecondPlayerEvolutionStartTurn;
            if (evolverPlayerState.TurnNumber < evolutionStartTurn)
            {
                _logger?.LogWarning("{AbilityName}: Player {PlayerId} cannot evolve on turn {TurnNumber}. Evolution starts on turn {StartTurn}.",
                    Name, requestEvent.PlayerId, evolverPlayerState.TurnNumber, evolutionStartTurn);
                return Task.FromResult(false);
            }

            _logger?.LogInformation("{AbilityName}: Conditions met for player {PlayerId} to evolve unit {UnitUid}.", Name, requestEvent.PlayerId, requestEvent.ActionDto.UnitUid);
            return Task.FromResult(true);
        }

        public Task<List<TargetInfo>> ResolveTargetsAsync(GameContext context)
        {
            // The unit to evolve is specified in the EvolveRequestedEvent.
            // The EvolveEffectHandler will use this information.
            _logger?.LogInformation("{AbilityName}: Target unit is specified in the EvolveRequestedEvent.", Name);
            return Task.FromResult(new List<TargetInfo>());
        }

        public Task<List<Effect>> CreateEffectsAsync(GameContext context, List<TargetInfo> resolvedTargets)
        {
            var effects = new List<Effect>();

            if (context.TriggeringEvent is not EvolveRequestedEvent requestEvent)
            {
                _logger?.LogError("{AbilityName}: Triggering event is not EvolveRequestedEvent in CreateEffectsAsync.", Name);
                return Task.FromResult(effects);
            }

            if (!Guid.TryParse(requestEvent.ActionDto.UnitUid, out Guid unitGuidToEvolve))
            {
                _logger?.LogError("{AbilityName}: Invalid UnitUid format in EvolveActionDto during CreateEffectsAsync: {UnitUid}", Name, requestEvent.ActionDto.UnitUid);
                return Task.FromResult(effects);
            }

            var effectConfig = new EffectConfig
            {
                Type = "evolve", // Corresponds to EvolveEffectHandler
                Parameters = new Dictionary<string, ValueConfig>()
            };

            var requestingPlayerId = requestEvent.PlayerId;

            effectConfig.Parameters.Add("CardToEvolveInstanceId", ValueConfig.Static(unitGuidToEvolve));

            var selectedTargetsForEffect = new List<TargetInfo>();
            var cardToEvolve = context.CurrentGameState.FindCardInstance(unitGuidToEvolve);
            if (cardToEvolve != null)
            {
                selectedTargetsForEffect.Add(TargetInfo.ForCard(cardToEvolve));
            }

            effects.Add(new Effect(
                effectConfig,
                requestingPlayerId,
                selectedTargetsForEffect,
                this.RuntimeId, // parentAbilityId
                context.TriggeringEvent?.EventId ?? Guid.Empty, // triggeredEventId
                this.SourceCard?.InstanceId // sourceCardId
            ));
            _logger?.LogInformation("{AbilityName}: Added 'evolve' effect for UnitUID {UnitUid} by Player {PlayerId}.", Name, unitGuidToEvolve, requestingPlayerId);

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