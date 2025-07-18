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
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters; // For PlayCardEffectParams

namespace ShadowverseBeyond.Server.Domain.Abilities.System
{
    public class PlayCardAbility : IAbility
    {
        private static readonly Guid _runtimeId = Guid.NewGuid();
        private readonly ILogger<PlayCardAbility> _logger;

        public Guid RuntimeId => _runtimeId;
        public long DefinitionId => GameConstants.SystemAbilityIds.PlayCardAbilityId;
        public string Name => "Play Card";
        public string AbilityTag => "System";
        public AbilityType Type => AbilityType.Triggered;

        public TriggerConfig TriggerConfig
        {
            get
            {
                return new TriggerConfig
                {
                    EventTypes = new List<EventType> { EventType.PlayCardRequested },
                };
            }
        }

        public CardInstance? SourceCard { get; set; } // System abilities usually don't have a source card

        public PlayCardAbility(ILogger<PlayCardAbility> logger)
        {
            _logger = logger;
        }

        public PlayCardAbility()
        {
            // Fallback for environments where ILogger might not be injected
            _logger = null!;
        }

        public Task<bool> CanTrigger(IGameEvent gameEvent, GameContext context)
        {
            // AbilityManager filters by EventType based on this.TriggerConfig.
            // So, if this method is reached, gameEvent.EventType is PlayCardRequested.
            // No further event-specific checks needed here for this system ability.
            return Task.FromResult(true);
        }

        public Task<bool> CheckConditionsAsync(GameContext context)
        {
            if (context.TriggeringEvent is not PlayCardRequestedEvent requestEvent)
            {
                _logger?.LogWarning("{AbilityName}: Triggering event is not PlayCardRequestedEvent. Event: {Event}", Name, context.TriggeringEvent?.GetType().Name);
                return Task.FromResult(false);
            }

            // Check if it's the requesting player's turn
            if (requestEvent.PlayerId != context.CurrentGameState.CurrentPlayerId)
            {
                _logger?.LogWarning("{AbilityName}: Player {RequestingPlayerId} attempted to play card, but it's not their turn (Active: {ActivePlayerId}).",
                    Name, requestEvent.PlayerId, context.CurrentGameState.CurrentPlayerId);
                return Task.FromResult(false);
            }

            PlayerState? activePlayerState = context.CurrentGameState.GetPlayerState(requestEvent.PlayerId);
            if (activePlayerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not find player state for PlayerId {PlayerId}.", Name, requestEvent.PlayerId);
                return Task.FromResult(false);
            }

            CardInstance? cardToPlay = activePlayerState.Hand.FirstOrDefault(c => c.InstanceId == requestEvent.ActionDto.CardUid);
            if (cardToPlay == null)
            {
                _logger?.LogWarning("{AbilityName}: Card with UID {CardUid} not found in player {PlayerId}'s hand.",
                    Name, requestEvent.ActionDto.CardUid, requestEvent.PlayerId);
                return Task.FromResult(false);
            }

            // Check PP cost
            if (activePlayerState.CurrentPlayPoints < cardToPlay.CurrentCost)
            {
                _logger?.LogWarning("{AbilityName}: Player {PlayerId} does not have enough PP to play {CardName} (Cost: {Cost}, Available: {PP}).",
                    Name, requestEvent.PlayerId, cardToPlay.Name, cardToPlay.CurrentCost, activePlayerState.CurrentPlayPoints);
                return Task.FromResult(false);
            }

            // Further playability checks (e.g., target validity, board space for followers)
            // are typically handled by a dedicated PlayabilityService or within the PlayCardEffectHandler.
            // This system ability focuses on the flow initiation.

            _logger?.LogInformation("{AbilityName}: Conditions met for player {PlayerId} to play card {CardUid}.", Name, requestEvent.PlayerId, requestEvent.ActionDto.CardUid);
            return Task.FromResult(true);
        }

        public Task<List<TargetInfo>> ResolveTargetsAsync(GameContext context)
        {
            // The PlayCardRequestedEvent already contains the CardToPlay (via ActionDto.CardUid)
            // and potentially a SelectedTargetId (via ActionDto.TargetUid).
            // This system ability doesn't need to resolve new card targets.
            // The PlayCardEffectHandler will use the information from the triggering event.
            _logger?.LogInformation("{AbilityName}: Targets are expected to be in the triggering PlayCardRequestedEvent.", Name);
            return Task.FromResult(new List<TargetInfo>());
        }

        public Task<List<Effect>> CreateEffectsAsync(GameContext context, List<TargetInfo> resolvedTargets)
        {
            var effects = new List<Effect>();

            if (context.TriggeringEvent is not PlayCardRequestedEvent requestEvent)
            {
                _logger?.LogError("{AbilityName}: Triggering event is not PlayCardRequestedEvent in CreateEffectsAsync.", Name);
                return Task.FromResult(effects);
            }

            PlayerState? activePlayerState = context.CurrentGameState.GetPlayerState(requestEvent.PlayerId);
            if (activePlayerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not find player state for PlayerId {PlayerId} in CreateEffectsAsync.", Name, requestEvent.PlayerId);
                return Task.FromResult(effects);
            }

            CardInstance? cardToPlay = activePlayerState.Hand.FirstOrDefault(c => c.InstanceId == requestEvent.ActionDto.CardUid);
            if (cardToPlay == null)
            {
                _logger?.LogError("{AbilityName}: Card with UID {CardUid} not found in player {PlayerId}'s hand during CreateEffectsAsync.",
                    Name, requestEvent.ActionDto.CardUid, requestEvent.PlayerId);
                return Task.FromResult(effects);
            }

            var playCardParams = new PlayCardEffectParams
            {
                CardInstanceId = requestEvent.ActionDto.CardUid,
                TargetId = requestEvent.ActionDto.TargetUid, // This can be null if no target
                CardDefinitionId = cardToPlay.DefinitionId, // Get DefinitionId from the card instance
                PlayReason = "PlayerAction"
            };

            var effectConfig = new EffectConfig
            {
                Type = "playcard", // Corresponds to PlayCardEffectHandler
                Parameters = new Dictionary<string, ValueConfig>
                {
                    { nameof(PlayCardEffectParams.CardInstanceId), ValueConfig.Static(playCardParams.CardInstanceId) },
                    { nameof(PlayCardEffectParams.CardDefinitionId), ValueConfig.Static(playCardParams.CardDefinitionId) },
                    { nameof(PlayCardEffectParams.PlayReason), ValueConfig.Static(playCardParams.PlayReason) }
                }
            };

            if (playCardParams.TargetId.HasValue)
            {
                effectConfig.Parameters.Add(nameof(PlayCardEffectParams.TargetId), ValueConfig.Static(playCardParams.TargetId.Value));
            }

            // The requesting player is the one who initiated the PlayCardRequest
            var requestingPlayerId = requestEvent.PlayerId;

            // SelectedTargets for the Effect could include the card being played and its target,
            // but PlayCardEffectHandler will likely derive these from its parameters.
            // For simplicity, we can pass an empty list or a list containing the player.
            var selectedTargetsForEffect = new List<TargetInfo>();
            var playerTarget = context.CurrentGameState.GetPlayerState(requestingPlayerId);
            if (playerTarget != null)
            {
                selectedTargetsForEffect.Add(TargetInfo.ForPlayer(playerTarget));
            }

            if (playCardParams.TargetId.HasValue)
            {
                var cardTarget = context.CurrentGameState.FindCardInstance(playCardParams.TargetId.Value);
                if (cardTarget != null)
                {
                    selectedTargetsForEffect.Add(TargetInfo.ForCard(cardTarget));
                }
            }


            effects.Add(new Effect(
                effectConfig,
                requestingPlayerId,
                selectedTargetsForEffect,
                this.RuntimeId, // parentAbilityId
                context.TriggeringEvent?.EventId ?? Guid.Empty, // triggeredEventId
                this.SourceCard?.InstanceId // sourceCardId
            ));
            _logger?.LogInformation("{AbilityName}: Added 'playcard' effect for CardUID {CardUid} by Player {PlayerId}.", Name, requestEvent.ActionDto.CardUid, requestingPlayerId);

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