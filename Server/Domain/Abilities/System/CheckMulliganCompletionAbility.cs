using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Abilities.System
{
    public class CheckMulliganCompletionAbility : IAbility
    {
        private static readonly Guid _runtimeId = Guid.NewGuid();
        private readonly ILogger<CheckMulliganCompletionAbility> _logger;

        public Guid RuntimeId => _runtimeId;
        public long DefinitionId => GameConstants.SystemAbilityIds.CheckMulliganCompletionAbilityId; // Need to add this ID
        public string Name => "Check Mulligan Completion";
        public string AbilityTag => "System";
        public AbilityType Type => AbilityType.Triggered;

        public TriggerConfig TriggerConfig => new TriggerConfig { EventTypes = new List<EventType>() }; // Not triggered by event
        public CardInstance? SourceCard { get; set; }

        public CheckMulliganCompletionAbility(ILogger<CheckMulliganCompletionAbility> logger)
        {
            _logger = logger;
        }


        public Task<bool> CanTrigger(IGameEvent gameEvent, GameContext context)
        {
            // This ability is triggered by chain, not by event.
            return Task.FromResult(false);
        }

        public Task<bool> CheckConditionsAsync(GameContext context)
        {
            bool allPlayersCompleted = context.CurrentGameState.Player1.HasCompletedMulligan &&
                                       context.CurrentGameState.Player2.HasCompletedMulligan;

            if (allPlayersCompleted)
            {
                _logger?.LogInformation("{AbilityName}: Conditions met. Both players have completed mulligan.", Name);
            }
            else
            {
                _logger?.LogInformation("{AbilityName}: Conditions not met. Waiting for other player to complete mulligan.", Name);
            }

            return Task.FromResult(allPlayersCompleted);
        }

        public Task<List<TargetInfo>> ResolveTargetsAsync(GameContext context)
        {
            // No specific targets needed, the effect will target the first player.
            return Task.FromResult(new List<TargetInfo>());
        }

        public Task<List<Effect>> CreateEffectsAsync(GameContext context, List<TargetInfo> resolvedTargets)
        {
            var effects = new List<Effect>();

            // The check for the Mulligan phase has been removed.
            // This ability's trigger condition (both players completing their mulligan)
            // is the definitive signal to transition to the next phase,
            // making a redundant phase check here both unnecessary and a source of bugs
            // if the phase is prematurely changed by another process.

            _logger?.LogInformation("{AbilityName}: Creating TurnStartEffect for the first player.", Name);

            var firstPlayerId = context.CurrentGameState.FirstPlayerId;
            if (!firstPlayerId.HasValue)
            {
                _logger?.LogError("{AbilityName}: FirstPlayerId is null, cannot create TurnStartEffect.", Name);
                return Task.FromResult(effects);
            }

            var firstPlayerState = context.CurrentGameState.GetPlayerState(firstPlayerId.Value);
            if (firstPlayerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not find state for first player {PlayerId}.", Name, firstPlayerId.Value);
                return Task.FromResult(effects);
            }

            var effectConfig = new EffectConfig
            {
                Type = "turnstart" // Corresponds to TurnStartEffectHandler
            };

            var effect = new Effect(
                effectConfig,
                firstPlayerId.Value,
                new List<TargetInfo> { TargetInfo.ForPlayer(firstPlayerState) },
                RuntimeId,
                context.TriggeringEvent?.EventId ?? Guid.Empty,
                null
            );

            effects.Add(effect);
            return Task.FromResult(effects);
        }

        public void RecordTrigger(GameContext context) { }
        public int GetTriggerCountThisTurn(GameContext context) => 0;
        public int GetTriggerCountThisGame(GameContext context) => 0;
        public void ResetTurnState(GameContext context) { }
    }
}