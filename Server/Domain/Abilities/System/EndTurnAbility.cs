using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.ActionRequests;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue; // Corrected: For Effect
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // Added for EffectConfig
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Effects; // For TargetId
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;

// Removed Placeholder Event Definitions as we will use existing ones from CoreGameEvents.cs

namespace ShadowverseBeyond.Server.Domain.Abilities.System
{
    [Obsolete("System abilities are deprecated and will be removed. Use the event-driven flow with effect handlers instead.")]
    public class EndTurnAbility : IAbility
    {
        private static readonly Guid _runtimeId = Guid.NewGuid();
        private readonly ILogger<EndTurnAbility> _logger;

        public Guid RuntimeId => _runtimeId;
        public long DefinitionId => GameConstants.SystemAbilityIds.EndTurnAbilityId;
        public string Name => "End Turn";
        private static readonly IReadOnlyList<AbilityKeywordConfig> SystemKeywords = Array.Empty<AbilityKeywordConfig>();
        public IReadOnlyList<AbilityKeywordConfig> AbilityKeywords => SystemKeywords;
        public IReadOnlyList<AbilityKeyword> Keywords => Array.Empty<AbilityKeyword>();
        public string KeywordSummary => AbilityKeyword.None.ToString();
        public AbilityType Type => AbilityType.Triggered;
        public List<Target> Targets { get; } = new List<Target>();

        public TriggerConfig TriggerConfig
        {
            get
            {
                return new TriggerConfig
                {
                    EventTypes = new List<EventType> { EventType.EndTurnRequested },
                };
            }
        }

        public Guid? SourceCardId { get; set; }

        public CardInstance? SourceCard { get; set; }

        public Guid? SourceBuffId => null; // System abilities are not granted by buffs.

        public int TriggerCountThisTurn => 0;

        public int TriggerCountThisGame => 0;

        public EndTurnAbility(ILogger<EndTurnAbility> logger)
        {
            _logger = logger;
        }

        // Default constructor might be problematic if IMapper is strictly required.
        // Consider removing or ensuring IMapper can be resolved if this is used (e.g. service locator anti-pattern, or make it nullable and handle)
        public EndTurnAbility()
        {
            // This will likely fail if IMapper is not available through other means.
            // For robust design, prefer constructor injection.
            _logger = null!; // Or some default logger
        }

        public Task<bool> CanTrigger(IGameEvent gameEvent, GameContext context)
        {
            // AbilityManager filters by EventType based on this.TriggerConfig.
            // So, if this method is reached, gameEvent.EventType is EndTurnRequested.
            // No further event-specific checks needed here for this system ability.
            return Task.FromResult(true);
        }

        public Task<TargetAvailabilityResult> CheckTargetAvailabilityAsync(GameContext context)
        {
            return Task.FromResult(new TargetAvailabilityResult(true));
        }

        public bool CheckConditions(GameContext context)
        {
            if (context.TriggeringEvent is not EndTurnRequestedEvent requestEvent)
            {
                _logger?.LogWarning("{AbilityName}: Triggering event is not EndTurnRequestedEvent. Event: {Event}", Name, context.TriggeringEvent?.GetType().Name);
                return false;
            }

            if (requestEvent.PlayerId != context.CurrentGameState.CurrentPlayerId)
            {
                _logger?.LogWarning("{AbilityName}: Player {RequestingPlayerId} attempted to end turn, but active player is {ActivePlayerId}.",
                    Name, requestEvent.PlayerId, context.CurrentGameState.CurrentPlayerId);
                return false;
            }

            if (context.CurrentGameState.IsGameOver)
            {
                _logger?.LogInformation("{AbilityName}: Game is already over. Cannot end turn.", Name);
                return false;
            }

            _logger?.LogInformation("{AbilityName}: Conditions met for player {PlayerId} to end turn.", Name, requestEvent.PlayerId);
            return true;
        }

        public Task<TargetResolutionResult> ResolveTargetsAsync(GameContext context, bool isCancellable = false)
        {
            _logger?.LogInformation("{AbilityName}: System ability, typically no card targets.", Name);
            return Task.FromResult(new TargetResolutionResult { Status = ResolutionStatus.Completed, ResolvedTargets = new Dictionary<string, List<TargetInfo>>() });
        }

        public Task<List<Effect>> CreateEffectsAsync(GameContext context, TargetResolutionResult targetResult)
        {
            var effects = new List<Effect>();
            var currentPlayerGuid = context.CurrentGameState.CurrentPlayerId;

            if (!currentPlayerGuid.HasValue)
            {
                _logger?.LogError("{AbilityName}: CurrentPlayerId is null, cannot create effects.", Name);
                return Task.FromResult(effects);
            }

            var currentPlayerState = context.CurrentGameState.GetPlayerState(currentPlayerGuid.Value);
            if (currentPlayerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not find current player state for PlayerId {PlayerId}.", Name, currentPlayerGuid.Value);
                return Task.FromResult(effects);
            }

            PlayerState? nextPlayerState = context.CurrentGameState.GetOpponentPlayerState(currentPlayerGuid.Value);

            if (nextPlayerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not determine the next player. CurrentPlayerId: {PlayerId}", Name, currentPlayerGuid.Value);
                return Task.FromResult(effects);
            }
            var nextPlayerId = nextPlayerState.PlayerId;

            _logger?.LogInformation("{AbilityName}: Creating effects for end of turn. Current: {CurrentPlayerId}, Next: {NextPlayerId}", Name, currentPlayerGuid.Value, nextPlayerId);

            // Effect 1: End current player's turn
            var effectConfig1 = new EffectConfig
            {
                Type = EffectType.TurnEnd, // Corrected EffectConfig.Type
                Parameters = new Dictionary<string, ValueConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    { "EndingPlayerId", new StaticValueConfig { Value = currentPlayerGuid.Value } },
                    { "EndTurnReason", new StaticValueConfig { Value = "PlayerAction" } }
                }
            };
            effects.Add(new Effect(
                effectConfig1,
                currentPlayerGuid.Value,
                new List<TargetInfo> { TargetInfo.ForPlayer(currentPlayerState) },
                this.RuntimeId,
                context.TriggeringEvent?.EventId ?? Guid.Empty,
                this.SourceCard?.InstanceId
            ));
            _logger?.LogInformation("{AbilityName}: Added 'turnend' effect for player {PlayerId}.", Name, currentPlayerGuid.Value);

            // Effect 2: Start next player's turn
            var effectConfig2 = new EffectConfig
            {
                Type = EffectType.TurnStart, // Corrected EffectConfig.Type
                Parameters = new Dictionary<string, ValueConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    { "StartingPlayerId", new StaticValueConfig { Value = nextPlayerId } },
                    { "StartTurnReason", new StaticValueConfig { Value = "TurnTransition" } }
                }
            };
            effects.Add(new Effect(
                effectConfig2,
                currentPlayerGuid.Value,
                new List<TargetInfo> { TargetInfo.ForPlayer(nextPlayerState) },
                this.RuntimeId,
                context.TriggeringEvent?.EventId ?? Guid.Empty,
                this.SourceCard?.InstanceId
            ));
            _logger?.LogInformation("{AbilityName}: Added 'turnstart' effect for player {PlayerId}.", Name, nextPlayerId);

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
