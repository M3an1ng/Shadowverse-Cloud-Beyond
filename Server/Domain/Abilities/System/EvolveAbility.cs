using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities;
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
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Effects; // For CardZone
// EvolveEffectParams is in ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters, but it's empty.
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;

namespace ShadowverseBeyond.Server.Domain.Abilities.System
{
    [Obsolete("System abilities are deprecated and will be removed. Use the event-driven flow with effect handlers instead.")]
    public class EvolveAbility : IAbility
    {
        private static readonly Guid _runtimeId = Guid.NewGuid();
        private readonly ILogger<EvolveAbility> _logger;

        public Guid RuntimeId => _runtimeId;
        public long DefinitionId => GameConstants.SystemAbilityIds.EvolveAbilityId;
        public string Name => "Evolve";
        private static readonly IReadOnlyList<AbilityKeywordConfig> SystemKeywords = Array.Empty<AbilityKeywordConfig>();
        public IReadOnlyList<AbilityKeywordConfig> AbilityKeywords => SystemKeywords;
        public AbilityKeyword PrimaryKeyword => AbilityKeyword.None;
        public string KeywordSummary => AbilityKeyword.None.ToString();
        public AbilityType Type => AbilityType.Triggered;
        public List<Target> Targets { get; } = new List<Target>();

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

        public Guid? SourceCardId { get; set; }

        public CardInstance? SourceCard { get; set; }

        public Guid? SourceBuffId => null; // System abilities are not granted by buffs.

        public int TriggerCountThisTurn => 0;

        public int TriggerCountThisGame => 0;

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

        public Task<TargetAvailabilityResult> CheckTargetAvailabilityAsync(GameContext context)
        {
            return Task.FromResult(new TargetAvailabilityResult(true));
        }

        public bool CheckConditions(GameContext context)
        {
            if (context.TriggeringEvent is not EvolveRequestedEvent requestEvent)
            {
                _logger?.LogWarning("{AbilityName}: Triggering event is not EvolveRequestedEvent. Event: {Event}", Name, context.TriggeringEvent?.GetType().Name);
                return false;
            }

            if (requestEvent.PlayerId != context.CurrentGameState.CurrentPlayerId)
            {
                _logger?.LogWarning("{AbilityName}: Player {RequestingPlayerId} attempted to evolve unit, but it's not their turn (Active: {ActivePlayerId}).",
                    Name, requestEvent.PlayerId, context.CurrentGameState.CurrentPlayerId);
                return false;
            }

            PlayerState? evolverPlayerState = context.CurrentGameState.GetPlayerState(requestEvent.PlayerId);
            if (evolverPlayerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not find player state for PlayerId {PlayerId}.", Name, requestEvent.PlayerId);
                return false;
            }

            if (!Guid.TryParse(requestEvent.ActionDto.UnitUid, out Guid unitGuidToEvolve))
            {
                _logger?.LogWarning("{AbilityName}: Invalid UnitUid format in EvolveActionDto: {UnitUid}", Name, requestEvent.ActionDto.UnitUid);
                return false;
            }

            CardInstance? cardToEvolve = evolverPlayerState.Field.FirstOrDefault(c => c.InstanceId == unitGuidToEvolve);
            if (cardToEvolve == null)
            {
                _logger?.LogWarning("{AbilityName}: Unit with UID {UnitUid} not found on player {PlayerId}'s field for evolution.",
                    Name, requestEvent.ActionDto.UnitUid, requestEvent.PlayerId);
                return false;
            }

            if (cardToEvolve.HasKeyword(CardKeyword.BanEvolve))
            {
                _logger?.LogWarning("{AbilityName}: Unit {UnitName} (UID: {UnitUid}) cannot evolve due to BanEvolve.",
                    Name, cardToEvolve.Name, cardToEvolve.InstanceId);
                return false;
            }

            // 1. Check intrinsic card properties (using the new pure method)
            if (!cardToEvolve.CanEvolve())
            {
                _logger?.LogWarning("{AbilityName}: Unit {UnitName} (UID: {UnitUid}) cannot evolve intrinsically (already evolved or not a follower).",
                    Name, cardToEvolve.Name, cardToEvolve.InstanceId);
                return false;
            }

            // 2. Check game rule conditions (EP, turn number, player's right to evolve)
            var hasFreeEvolve = cardToEvolve.HasKeyword(CardKeyword.FreeEvolve);
            if (!hasFreeEvolve && evolverPlayerState.CurrentEvolutionPoints < GameConstants.Evolution.EvolutionPointCost)
            {
                _logger?.LogWarning("{AbilityName}: Player {PlayerId} does not have enough EP to evolve. Has: {EP}, Needs: {Cost}",
                    Name, requestEvent.PlayerId, evolverPlayerState.CurrentEvolutionPoints, GameConstants.Evolution.EvolutionPointCost);
                return false;
            }

            if (!evolverPlayerState.CanEvolve)
            {
                _logger?.LogWarning("{AbilityName}: Player {PlayerId} has already used their evolution right this turn.",
                    Name, requestEvent.PlayerId);
                return false;
            }

            if (!evolverPlayerState.IsEvolutionUnlocked)
            {
                _logger?.LogWarning("{AbilityName}: Player {PlayerId} has not unlocked evolution yet.",
                    Name, requestEvent.PlayerId);
                return false;
            }

            _logger?.LogInformation("{AbilityName}: Conditions met for player {PlayerId} to evolve unit {UnitUid}.", Name, requestEvent.PlayerId, requestEvent.ActionDto.UnitUid);
            return true;
        }

        public Task<TargetResolutionResult> ResolveTargetsAsync(GameContext context, bool isCancellable = false)
        {
            // The unit to evolve is specified in the EvolveRequestedEvent.
            // The EvolveEffectHandler will use this information.
            _logger?.LogInformation("{AbilityName}: Target unit is specified in the EvolveRequestedEvent.", Name);
            return Task.FromResult(new TargetResolutionResult { Status = ResolutionStatus.Completed, ResolvedTargets = new Dictionary<string, List<TargetInfo>>() });
        }

        public Task<List<Effect>> CreateEffectsAsync(GameContext context, TargetResolutionResult targetResult)
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
                Type = EffectType.Evolve, // Corresponds to EvolveEffectHandler
                Parameters = new Dictionary<string, ValueConfig>(StringComparer.OrdinalIgnoreCase)
            };

            var requestingPlayerId = requestEvent.PlayerId;

            // The target is passed via SelectedTargets, not through parameters.
            // This line was causing a warning and is not the correct way to pass the target.
            // effectConfig.Parameters.Add("CardToEvolveInstanceId", new StaticValueConfig { Value = unitGuidToEvolve });

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

        public bool HasKeyword(AbilityKeyword keyword)
        {
            return false;
        }

        public int? GetKeywordValue(AbilityKeyword keyword)
        {
            return null;
        }

        public int GetTriggerCountThisTurn(GameContext context)
        {
            return TriggerCountThisTurn;
        }

        public int GetTriggerCountThisGame(GameContext context)
        {
            return TriggerCountThisGame;
        }

        public void ResetTurnState(GameContext context)
        {
            _logger?.LogDebug("{AbilityName} turn state reset.", Name);
        }
    }
}
