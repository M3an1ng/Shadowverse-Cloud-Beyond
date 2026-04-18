using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when one or more buffs are removed from a single target.
    /// </summary>
    [GameEventMetadata("Buff移除事件", "当一个或多个Buff从目标上被移除时触发。", EventType.BuffRemoved)]
    public class BuffRemovedEvent : GameEventBase, ICommittableEvent
    {
        private readonly IReadOnlyList<Buff> _removedBuffs;

        [GameEventParameter("被移除的Buff", "本次事件移除的Buff实例列表。")]
        public IReadOnlyList<Buff> RemovedBuffs => _removedBuffs;

        // Keep the original single-item accessor for compatibility with existing call sites.
        public Buff RemovedBuff => _removedBuffs[0];

        [GameEventParameter("移除原因", "这些Buff被移除的原因。")]
        public string RemovalReason { get; }

        [GameEventParameter("Buff来源", "施加这些被移除Buff的来源卡牌。", DslDisplayTypeName = "Card")]
        public CardInstance? RemovalSource => Source?.Card;

        [GameEventParameter("Buff目标", "这些Buff被移除时对应的目标卡牌。", DslDisplayTypeName = "Card")]
        public CardInstance? RemovalTarget => Target.FirstOrDefault()?.Card;

        public bool IsBatch => _removedBuffs.Count > 1;

        public BuffRemovedEvent(
            Buff removedBuff,
            CardInstance? target,
            CardInstance? source,
            string removalReason = "manual",
            Guid? eventId = null,
            DateTime? timestamp = null)
            : this(new[] { removedBuff }, target, source, removalReason, eventId, timestamp)
        {
        }

        public BuffRemovedEvent(
            IEnumerable<Buff> removedBuffs,
            CardInstance? target,
            CardInstance? source,
            string removalReason = "manual",
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.BuffRemoved, eventId, timestamp)
        {
            if (removedBuffs == null)
            {
                throw new ArgumentNullException(nameof(removedBuffs));
            }

            var buffs = removedBuffs.Where(static buff => buff != null).ToList();
            if (buffs.Count == 0)
            {
                throw new ArgumentException("BuffRemovedEvent requires at least one removed buff.", nameof(removedBuffs));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target), "BuffRemovedEvent requires a single target card.");
            }

            if (buffs.Any(buff => buff.TargetCardInstanceId != target.InstanceId))
            {
                throw new ArgumentException("All removed buffs must belong to the provided target card.", nameof(removedBuffs));
            }

            if (source != null && buffs.Any(buff =>
                    buff.SourceCardInstanceId.HasValue
                    && buff.SourceCardInstanceId.Value != Guid.Empty
                    && buff.SourceCardInstanceId.Value != source.InstanceId))
            {
                throw new ArgumentException("All removed buffs must share the provided source card.", nameof(removedBuffs));
            }

            _removedBuffs = buffs.AsReadOnly();
            RemovalReason = removalReason ?? "unknown";

            Target.Add(TargetInfo.ForCard(target));
            if (source != null)
            {
                Source = TargetInfo.ForCard(source);
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult { Success = true };
            var targetCard = RemovalTarget;

            foreach (var removedBuff in _removedBuffs)
            {
                var singleResult = CommitSingleRemoval(removedBuff, targetCard, context);
                result.Merge(singleResult);
                if (!singleResult.Success)
                {
                    return singleResult;
                }
            }

            return result;
        }

        private DomainOperationResult CommitSingleRemoval(Buff removedBuff, CardInstance? targetCard, GameContext context)
        {
            var result = new DomainOperationResult { Success = true };
            targetCard ??= context.CurrentGameState.FindCardInstance(removedBuff.TargetCardInstanceId);

            var affectsHealth = removedBuff.GetStatModifications().Any(mod => mod.Stat == StatType.Health);
            var previousCurrentHealth = 0;
            if (affectsHealth && targetCard != null && (targetCard.IsFollower || targetCard.IsLeader))
            {
                var previousMaxHealth = context.BuffService.GetEffectiveMaxHealth(targetCard, context);
                previousCurrentHealth = Math.Max(0, previousMaxHealth - targetCard.Damage);
            }

            var hasSuppressionEffects = removedBuff.Effects.OfType<AbilitySuppressionBuffEffect>().Any();
            HashSet<Guid>? effectiveBeforeSuppressionRemoval = null;
            if (hasSuppressionEffects && targetCard != null)
            {
                effectiveBeforeSuppressionRemoval = context.AbilityManager
                    .GetEffectiveAbilitiesForCard(targetCard.InstanceId, context)
                    .Select(ability => ability.RuntimeId)
                    .ToHashSet();
            }

            var logger = context.ServiceProvider.GetService<ILogger<BuffRemovedEvent>>();
            var sourceAbility = context.AbilityManager.GetAbilityByRuntimeId(removedBuff.SourceAbilityId);
            var sourceCardId = sourceAbility?.SourceCardId ?? removedBuff.SourceCardInstanceId;
            var sourceCardName = sourceAbility?.SourceCard?.Name ?? removedBuff.SourceCardName;
            var sourceAbilityName = sourceAbility?.Name ?? removedBuff.SourceAbilityName;

            logger?.LogInformation(
                "[BuffRemovedEvent] Buff {BuffId} removed. TargetCardId={TargetCardId} EffectKey={EffectKey} SourceAbilityId={SourceAbilityId} SourceAbilityName={SourceAbilityName} SourceCardId={SourceCardId} SourceCardName={SourceCardName} SourceEffectIndex={SourceEffectIndex} DefinitionId={DefinitionId} RemovalReason={RemovalReason}",
                removedBuff.Id,
                removedBuff.TargetCardInstanceId,
                removedBuff.EffectKey ?? "<null>",
                removedBuff.SourceAbilityId,
                sourceAbilityName ?? "<null>",
                sourceCardId,
                sourceCardName ?? "<null>",
                removedBuff.SourceEffectIndex,
                removedBuff.DefinitionId ?? "<null>",
                RemovalReason ?? "<null>");

            var abilityRuntimeIds = context.AbilityManager.GetAbilityRuntimeIdsForBuff(removedBuff.Id);

            context.BuffManager.UnregisterBuffInstance(removedBuff.Id);
            context.CurrentGameState.IncrementBuffRevision();
            context.BuffService.InvalidateDerivedState(context.CurrentGameState, removedBuff.TargetCardInstanceId);

            if (targetCard != null)
            {
                var activeBuffs = context.BuffManager.GetBuffsForCard(targetCard.InstanceId);
                foreach (var tribe in removedBuff.GetAddedTribes())
                {
                    var stillProvided = activeBuffs.Any(buff =>
                        buff.Effects.OfType<TribeBuffEffect>()
                            .Any(effect => effect.AddedTribes.Any(existing => string.Equals(existing, tribe, StringComparison.OrdinalIgnoreCase))));

                    if (!stillProvided)
                    {
                        targetCard.RemoveTribeMarker(tribe);
                    }
                }
            }

            if (affectsHealth && targetCard != null && (targetCard.IsFollower || targetCard.IsLeader))
            {
                var newMaxHealth = context.BuffService.GetEffectiveMaxHealth(targetCard, context);
                var cappedCurrentHealth = Math.Min(previousCurrentHealth, newMaxHealth);
                targetCard.Damage = Math.Max(0, newMaxHealth - cappedCurrentHealth);
            }

            if (abilityRuntimeIds.Count > 0)
            {
                var auraResult = context.AuraManager.UnregisterBySourceAbilities(abilityRuntimeIds, context);
                result.Merge(auraResult);
                if (!auraResult.Success)
                {
                    return auraResult;
                }
            }

            if (hasSuppressionEffects && targetCard != null && effectiveBeforeSuppressionRemoval != null)
            {
                var effectiveAfterSuppressionRemoval = context.AbilityManager
                    .GetEffectiveAbilitiesForCard(targetCard.InstanceId, context)
                    .OfType<Ability>()
                    .ToList();

                var newlyUnsuppressedAbilities = effectiveAfterSuppressionRemoval
                    .Where(ability => !effectiveBeforeSuppressionRemoval.Contains(ability.RuntimeId))
                    .ToList();

                if (newlyUnsuppressedAbilities.Count > 0)
                {
                    foreach (var ability in newlyUnsuppressedAbilities)
                    {
                        if (ability.Type != AbilityType.Continuous || ability.Config.Auras.All(aura => aura == null || !aura.IsEnabled))
                        {
                            continue;
                        }

                        var refreshResult = context.AbilityManager.RefreshAuraForAbility(ability, context);
                        result.Merge(refreshResult);
                        if (!refreshResult.Success)
                        {
                            return refreshResult;
                        }
                    }
                }
            }

            if (targetCard != null)
            {
                var affectedActionTypes = removedBuff.GetActionUsageLimitModifications()
                    .Select(static modification => modification.ActionType)
                    .Distinct()
                    .ToList();

                foreach (var actionType in affectedActionTypes)
                {
                    switch (actionType)
                    {
                        case CardActionUsageType.Attack:
                            result.GeneratedEvents.Add(new AttackStateUpdatedEvent(targetCard, targetCard.CanAttack));
                            break;
                        case CardActionUsageType.Act:
                            result.GeneratedEvents.Add(new ActStateUpdatedEvent(targetCard, targetCard.ActsThisTurn, targetCard.MaxActsPerTurn));
                            break;
                        case CardActionUsageType.Fusion:
                            result.GeneratedEvents.Add(new FusionStateUpdatedEvent(targetCard, targetCard.FusionsThisTurn, targetCard.MaxFusionsPerTurn));
                            break;
                    }
                }
            }

            return result;
        }
    }
}
