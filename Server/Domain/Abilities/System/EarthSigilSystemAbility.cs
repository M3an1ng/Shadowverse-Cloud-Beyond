using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.System
{
    /// <summary>
    /// 系统能力：土之印召唤完成后，将土之印堆叠结算作为普通效果排队。
    /// </summary>
    public class EarthSigilSystemAbility : IAbility
    {
        private static readonly Guid RuntimeAbilityId = Guid.NewGuid();
        private static readonly IReadOnlyList<AbilityKeywordConfig> EmptyAbilityKeywords = Array.Empty<AbilityKeywordConfig>();
        private static readonly IReadOnlyList<AbilityKeyword> EmptyKeywords = Array.Empty<AbilityKeyword>();

        public Guid RuntimeId => RuntimeAbilityId;
        public long DefinitionId => GameConstants.SystemAbilityIds.EarthSigilSystemAbilityId;
        public string Name => "Earth Sigil Settlement";
        public IReadOnlyList<AbilityKeywordConfig> AbilityKeywords => EmptyAbilityKeywords;
        public IReadOnlyList<AbilityKeyword> Keywords => EmptyKeywords;
        public string KeywordSummary => AbilityKeyword.None.ToString();
        public AlternatePlayConfig? AlternatePlay => null;
        public AbilityType Type => AbilityType.Triggered;
        public TriggerConfig TriggerConfig { get; } = new()
        {
            EventTypes = new List<EventType> { EventType.Summon },
            Timing = TriggerTiming.After
        };
        public Guid? SourceCardId => null;
        public CardInstance? SourceCard => null;
        public Guid? SourceBuffId => null;
        public int TriggerCountThisTurn => 0;
        public int TriggerCountThisGame => 0;
        public List<Target> Targets { get; } = new();

        public Task<bool> CanTrigger(IGameEvent gameEvent, GameContext context)
        {
            return Task.FromResult(IsEarthSigilFieldEntry(gameEvent));
        }

        public bool CheckConditions(GameContext context)
        {
            return IsEarthSigilFieldEntry(context.TriggeringEvent);
        }

        public Task<TargetAvailabilityResult> CheckTargetAvailabilityAsync(GameContext context)
        {
            return Task.FromResult(new TargetAvailabilityResult(CheckConditions(context)));
        }

        public Task<TargetResolutionResult> ResolveTargetsAsync(GameContext context, bool isCancellable = false)
        {
            var targets = new Dictionary<string, List<TargetInfo>>();
            if (context.TriggeringEvent is SummonEvent summon && summon.SummonedCard != null)
            {
                targets["entering"] = new List<TargetInfo> { TargetInfo.ForCard(summon.SummonedCard) };
            }

            return Task.FromResult(new TargetResolutionResult
            {
                Status = ResolutionStatus.Completed,
                ResolvedTargets = targets
            });
        }

        public Task<List<Effect>> CreateEffectsAsync(GameContext context, TargetResolutionResult targetResult)
        {
            var effects = new List<Effect>();
            if (context.TriggeringEvent is not SummonEvent summon || summon.SummonedCard == null)
            {
                return Task.FromResult(effects);
            }

            var owner = context.CurrentGameState.GetPlayerState(summon.SummonedCard.OwnerPlayerId);
            if (owner == null)
            {
                return Task.FromResult(effects);
            }

            var selectedTargets = targetResult.ResolvedTargets.TryGetValue("entering", out var resolved)
                ? resolved
                : new List<TargetInfo> { TargetInfo.ForCard(summon.SummonedCard) };

            effects.Add(new Effect(
                new EffectConfig { Type = EffectType.SettleEarthSigil },
                owner.PlayerId,
                selectedTargets.ToList(),
                RuntimeId,
                summon.EventId,
                null)
            {
                SourceAbility = this,
                ResolvedTargets = targetResult.ResolvedTargets
            });

            return Task.FromResult(effects);
        }

        public void RecordTrigger(GameContext context)
        {
        }

        public bool HasKeyword(AbilityKeyword keyword) => false;

        public int? GetKeywordValue(AbilityKeyword keyword) => null;

        public int GetTriggerCountThisTurn(GameContext context) => TriggerCountThisTurn;

        public int GetTriggerCountThisGame(GameContext context) => TriggerCountThisGame;

        public void ResetTurnState(GameContext context)
        {
        }

        private static bool IsEarthSigilFieldEntry(IGameEvent? gameEvent)
        {
            return gameEvent is SummonEvent summon
                && summon.SummonedCard != null
                && summon.SummonedCard.CurrentZone == CardZone.Field
                && summon.SummonedCard.HasKeyword(CardKeyword.EarthSigil);
        }
    }
}
