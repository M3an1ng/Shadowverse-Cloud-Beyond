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
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Effects; // For PlayCardEffectParams
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;

namespace ShadowverseBeyond.Server.Domain.Abilities.System
{
    [Obsolete("System abilities are deprecated and will be removed. Use the event-driven flow with effect handlers instead.")]
    public class PlayCardAbility : IAbility
    {
        private static readonly Guid _runtimeId = Guid.NewGuid();
        private readonly ILogger<PlayCardAbility> _logger;

        public Guid RuntimeId => _runtimeId;
        public long DefinitionId => GameConstants.SystemAbilityIds.PlayCardAbilityId;
        public string Name => "Play Card";
        private static readonly IReadOnlyList<AbilityKeywordConfig> SystemKeywords = Array.Empty<AbilityKeywordConfig>();
        public IReadOnlyList<AbilityKeywordConfig> AbilityKeywords => SystemKeywords;
        public IReadOnlyList<AbilityKeyword> Keywords => Array.Empty<AbilityKeyword>();
        public string KeywordSummary => AbilityKeyword.None.ToString();
        public AlternatePlayConfig? AlternatePlay => null;
        public AbilityType Type => AbilityType.Triggered;
        public List<Target> Targets { get; } = new List<Target>();

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
        public Guid? SourceCardId { get; set; }

        public CardInstance? SourceCard { get; set; } // System abilities usually don't have a source card

        public Guid? SourceBuffId => null; // System abilities are not granted by buffs.

        public int TriggerCountThisTurn => 0;

        public int TriggerCountThisGame => 0;

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

        public Task<TargetAvailabilityResult> CheckTargetAvailabilityAsync(GameContext context)
        {
            return Task.FromResult(new TargetAvailabilityResult(true));
        }

        public bool CheckConditions(GameContext context)
        {
            if (context.TriggeringEvent is not PlayCardRequestedEvent requestEvent)
            {
                _logger?.LogWarning("{AbilityName}: Triggering event is not PlayCardRequestedEvent. Event: {Event}", Name, context.TriggeringEvent?.GetType().Name);
                return false;
            }

            // Check if it's the requesting player's turn
            if (requestEvent.PlayerId != context.CurrentGameState.CurrentPlayerId)
            {
                _logger?.LogWarning("{AbilityName}: Player {RequestingPlayerId} attempted to play card, but it's not their turn (Active: {ActivePlayerId}).",
                    Name, requestEvent.PlayerId, context.CurrentGameState.CurrentPlayerId);
                return false;
            }

            PlayerState? activePlayerState = context.CurrentGameState.GetPlayerState(requestEvent.PlayerId);
            if (activePlayerState == null)
            {
                _logger?.LogError("{AbilityName}: Could not find player state for PlayerId {PlayerId}.", Name, requestEvent.PlayerId);
                return false;
            }

            CardInstance? cardToPlay = activePlayerState.Hand.FirstOrDefault(c => c.InstanceId == requestEvent.ActionDto.CardUid);
            if (cardToPlay == null)
            {
                _logger?.LogWarning("{AbilityName}: Card with UID {CardUid} not found in player {PlayerId}'s hand.",
                    Name, requestEvent.ActionDto.CardUid, requestEvent.PlayerId);
                return false;
            }

            // Check PP cost
            var spendablePp = activePlayerState.GetSpendablePlayPoints();
            if (cardToPlay.CurrentCost > spendablePp)
            {
                _logger?.LogWarning("{AbilityName}: Player {PlayerId} does not have enough PP to play {CardName} (Cost: {Cost}, Available: {PP}).",
                    Name, requestEvent.PlayerId, cardToPlay.Name, cardToPlay.CurrentCost, spendablePp);
                return false;
            }

            // Further playability checks (e.g., target validity, board space for followers)
            // are typically handled by a dedicated PlayabilityService or within the PlayCardEffectHandler.
            // This system ability focuses on the flow initiation.

            _logger?.LogInformation("{AbilityName}: Conditions met for player {PlayerId} to play card {CardUid}.", Name, requestEvent.PlayerId, requestEvent.ActionDto.CardUid);
            return true;
        }

        public Task<TargetResolutionResult> ResolveTargetsAsync(GameContext context, bool isCancellable = false)
        {
            // The PlayCardRequestedEvent already contains the CardToPlay (via ActionDto.CardUid)
            // and potentially a SelectedTargetId (via ActionDto.TargetUid).
            // This system ability doesn't need to resolve new card targets.
            // The PlayCardEffectHandler will use the information from the triggering event.
            _logger?.LogInformation("{AbilityName}: Targets are expected to be in the triggering PlayCardRequestedEvent.", Name);
            return Task.FromResult(new TargetResolutionResult { Status = ResolutionStatus.Completed, ResolvedTargets = new Dictionary<string, List<TargetInfo>>() });
        }

        public Task<List<Effect>> CreateEffectsAsync(GameContext context, TargetResolutionResult targetResult)
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
                TargetIds = requestEvent.ActionDto.TargetUids, // This can be null if no target
                CardDefinitionId = cardToPlay.BaseDefinitionId, // Get DefinitionId from the card instance
                PlayReason = "PlayerAction"
            };

            var effectConfig = new EffectConfig
            {
                Type = EffectType.Play, // Corresponds to PlayCardEffectHandler
                Parameters = new Dictionary<string, ValueConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    { nameof(PlayCardEffectParams.CardInstanceId), new StaticValueConfig { Value = playCardParams.CardInstanceId } },
                    { nameof(PlayCardEffectParams.CardDefinitionId), new StaticValueConfig { Value = playCardParams.CardDefinitionId } },
                    { nameof(PlayCardEffectParams.PlayReason), new StaticValueConfig { Value = playCardParams.PlayReason } }
                }
            };

            if (playCardParams.TargetIds != null && playCardParams.TargetIds.Any())
            {
                effectConfig.Parameters.Add(nameof(PlayCardEffectParams.TargetIds), new StaticValueConfig { Value = playCardParams.TargetIds });
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

            // 【BUG修复】移除此代码块。PlayCardEffect的目标只应该是玩家。
            // 玩家选择的卡牌目标（TargetId）已经作为参数传递给了effectConfig，
            // 不应该被添加到这个初始效果的目标列表中，否则会污染后续效果的目标选择。
            /*
            if (playCardParams.TargetId.HasValue)
            {
                var cardTarget = context.CurrentGameState.FindCardInstance(playCardParams.TargetId.Value);
                if (cardTarget != null)
                {
                    selectedTargetsForEffect.Add(TargetInfo.ForCard(cardTarget));
                }
            }
            */

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
