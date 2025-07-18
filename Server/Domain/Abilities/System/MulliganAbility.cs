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
using ShadowverseBeyond.Server.Dtos.Enums; // For GamePhase
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters; // For MulliganEffectParams

namespace ShadowverseBeyond.Server.Domain.Abilities.System
{
    public class MulliganAbility : IAbility
    {
        private static readonly Guid _runtimeId = Guid.NewGuid();
        private readonly ILogger<MulliganAbility> _logger;
        private readonly CheckMulliganCompletionAbility _checkMulliganCompletionAbility;

        public Guid RuntimeId => _runtimeId;
        public long DefinitionId => GameConstants.SystemAbilityIds.MulliganAbilityId;
        public string Name => "Mulligan";
        public string AbilityTag => "System";
        public AbilityType Type => AbilityType.Triggered;

        public TriggerConfig TriggerConfig
        {
            get
            {
                return new TriggerConfig
                {
                    EventTypes = new List<EventType> { EventType.MulliganSubmitted },
                };
            }
        }

        public CardInstance? SourceCard { get; set; }

        public MulliganAbility(ILogger<MulliganAbility> logger, CheckMulliganCompletionAbility checkMulliganCompletionAbility)
        {
            _logger = logger;
            _checkMulliganCompletionAbility = checkMulliganCompletionAbility;
        }


        public Task<bool> CanTrigger(IGameEvent gameEvent, GameContext context)
        {
            // AbilityManager filters by EventType based on this.TriggerConfig.
            // So, if this method is reached, gameEvent.EventType is MulliganSubmitted.
            // No further event-specific checks needed here for this system ability.
            return Task.FromResult(true);
        }

        public Task<bool> CheckConditionsAsync(GameContext context)
        {
            if (context.TriggeringEvent is not MulliganSubmittedEvent requestEvent)
            {
                _logger?.LogWarning("{AbilityName}: Triggering event is not MulliganSubmittedEvent. Event: {Event}", Name, context.TriggeringEvent?.GetType().Name);
                return Task.FromResult(false);
            }

            // Check if the game is in the Mulligan phase
            if (context.CurrentGameState.Phase != GamePhase.Mulligan)
            {
                _logger?.LogWarning("{AbilityName}: MulliganSubmittedEvent received but game is not in Mulligan phase (Current: {GamePhase}).",
                    Name, context.CurrentGameState.Phase);
                return Task.FromResult(false);
            }

            PlayerState? playerState = context.CurrentGameState.GetPlayerState(requestEvent.PlayerId);
            if (playerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not find player state for PlayerId {PlayerId}.", Name, requestEvent.PlayerId);
                return Task.FromResult(false);
            }

            // Check if the player has already completed mulligan
            if (playerState.HasCompletedMulligan)
            {
                _logger?.LogWarning("{AbilityName}: Player {PlayerId} has already completed mulligan.", Name, requestEvent.PlayerId);
                return Task.FromResult(false);
            }

            _logger?.LogInformation("{AbilityName}: Conditions met for player {PlayerId} to submit mulligan.", Name, requestEvent.PlayerId);
            return Task.FromResult(true);
        }

        public Task<List<TargetInfo>> ResolveTargetsAsync(GameContext context)
        {
            // Mulligan targets the player themselves and their hand/deck.
            // The specific cards to replace are in the MulliganSubmittedEvent.
            _logger?.LogInformation("{AbilityName}: Mulligan targets are implicit from the event.", Name);
            return Task.FromResult(new List<TargetInfo>());
        }

        public Task<List<Effect>> CreateEffectsAsync(GameContext context, List<TargetInfo> resolvedTargets)
        {
            var effects = new List<Effect>();

            if (context.TriggeringEvent is not MulliganSubmittedEvent requestEvent)
            {
                _logger?.LogError("{AbilityName}: Triggering event is not MulliganSubmittedEvent in CreateEffectsAsync.", Name);
                return Task.FromResult(effects);
            }

            var mulliganParams = new MulliganEffectParams
            {
                CardUidsToReplace = requestEvent.CardUidsToReplace,
                MulliganReason = "PlayerChoice"
            };

            var effectConfig = new EffectConfig
            {
                Type = "mulligan", // Corresponds to MulliganEffectHandler
                Parameters = new Dictionary<string, ValueConfig>
                {
                    { nameof(MulliganEffectParams.CardUidsToReplace), ValueConfig.Static(mulliganParams.CardUidsToReplace) },
                    { nameof(MulliganEffectParams.MulliganReason), ValueConfig.Static(mulliganParams.MulliganReason) }
                }
            };

            var requestingPlayerId = requestEvent.PlayerId;
            var selectedTargetsForEffect = new List<TargetInfo>();
            var playerTarget = context.CurrentGameState.GetPlayerState(requestingPlayerId);
            if (playerTarget != null)
            {
                selectedTargetsForEffect.Add(TargetInfo.ForPlayer(playerTarget));
            }

            var mulliganEffect = new Effect(
                effectConfig,
                requestingPlayerId,
                selectedTargetsForEffect,
                this.RuntimeId,
                context.TriggeringEvent?.EventId ?? Guid.Empty,
                this.SourceCard?.InstanceId
            );

            // Directly chain the hard-coded system ability instance
            mulliganEffect.ChainedAbilities.Add(_checkMulliganCompletionAbility);

            effects.Add(mulliganEffect);
            _logger?.LogInformation("{AbilityName}: Added 'mulligan' effect for Player {PlayerId}, replacing {Count} cards.",
                Name, requestingPlayerId, requestEvent.CardUidsToReplace.Count);

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