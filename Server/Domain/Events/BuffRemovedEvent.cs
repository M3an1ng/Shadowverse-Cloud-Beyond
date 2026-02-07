using System;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // DomainOperationResult
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a Buff is removed from a target.
    /// As a committable event, it performs the removal via BuffService.
    /// </summary>
    [GameEventMetadata("失去临时状态", "当一个临时状态从目标身上被移除时触发的事件。", EventType.BuffRemoved)]
    public class BuffRemovedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("移除的Buff", "被移除的Buff实例。")]
        public Buff RemovedBuff { get; }

        [GameEventParameter("移除原因", "Buff被移除的原因。")]
        public string RemovalReason { get; }

        [GameEventParameter("移除来源", "导致Buff移除的来源卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? RemovalSource => Source?.Card;

        public BuffRemovedEvent(Buff removedBuff, CardInstance? source, string removalReason = "manual", Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.BuffRemoved, eventId, timestamp)
        {
            RemovedBuff = removedBuff ?? throw new ArgumentNullException(nameof(removedBuff));
            RemovalReason = removalReason ?? "unknown";
            if (source != null)
            {
                Source = TargetInfo.ForCard(source);
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult { Success = true };
            var targetCard = context.CurrentGameState.FindCardInstance(RemovedBuff.TargetCardInstanceId);
            var affectsHealth = RemovedBuff.GetStatModifications().Any(mod => mod.Stat == StatType.Health);
            var previousCurrentHealth = 0;
            if (affectsHealth && targetCard != null && (targetCard.IsFollower || targetCard.IsLeader))
            {
                var previousMaxHealth = context.BuffService.GetEffectiveMaxHealth(targetCard, context);
                previousCurrentHealth = Math.Max(0, previousMaxHealth - targetCard.Damage);
            }

            var logger = context.ServiceProvider.GetService<ILogger<BuffRemovedEvent>>();
            var sourceAbility = context.AbilityManager.GetAbilityByRuntimeId(RemovedBuff.SourceAbilityId);
            var sourceCardId = sourceAbility?.SourceCardId ?? RemovedBuff.SourceCardInstanceId;
            var sourceCardName = sourceAbility?.SourceCard?.Name ?? RemovedBuff.SourceCardName;
            var sourceAbilityName = sourceAbility?.Name ?? RemovedBuff.SourceAbilityName;

            logger?.LogInformation(
                "[BuffRemovedEvent] Buff {BuffId} removed. TargetCardId={TargetCardId} EffectKey={EffectKey} SourceAbilityId={SourceAbilityId} SourceAbilityName={SourceAbilityName} SourceCardId={SourceCardId} SourceCardName={SourceCardName} SourceEffectIndex={SourceEffectIndex} DefinitionId={DefinitionId} RemovalReason={RemovalReason}",
                RemovedBuff.Id,
                RemovedBuff.TargetCardInstanceId,
                RemovedBuff.EffectKey ?? "<null>",
                RemovedBuff.SourceAbilityId,
                sourceAbilityName ?? "<null>",
                sourceCardId,
                sourceCardName ?? "<null>",
                RemovedBuff.SourceEffectIndex,
                RemovedBuff.DefinitionId ?? "<null>",
                RemovalReason ?? "<null>");

            var abilityRuntimeIds = context.AbilityManager.GetAbilityRuntimeIdsForBuff(RemovedBuff.Id);

            // The commit action is the single, atomic operation of unregistering the buff.
            context.BuffManager.UnregisterBuffInstance(this.RemovedBuff.Id);
            context.CurrentGameState.IncrementBuffRevision();
            context.BuffService.InvalidateDerivedState(context.CurrentGameState, this.RemovedBuff.TargetCardInstanceId);

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

            var suppressionEffects = RemovedBuff.Effects.OfType<AbilitySuppressionBuffEffect>().ToList();
            if (suppressionEffects.Count > 0 && targetCard != null)
            {
                var abilities = context.AbilityManager
                    .GetAbilitiesForCard(targetCard.InstanceId)
                    .OfType<Ability>()
                    .ToList();

                var affectedAbilities = AbilitySuppressionEvaluator.CollectSuppressedAbilities(abilities, suppressionEffects)
                    .OfType<Ability>()
                    .ToList();

                if (affectedAbilities.Count > 0)
                {
                    var effectiveIds = new HashSet<Guid>(
                        context.AbilityManager
                            .GetEffectiveAbilitiesForCard(targetCard.InstanceId, context)
                            .Select(ability => ability.RuntimeId));

                    foreach (var ability in affectedAbilities)
                    {
                        if (!effectiveIds.Contains(ability.RuntimeId))
                        {
                            continue;
                        }

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

            return result;
        }
    }
}
