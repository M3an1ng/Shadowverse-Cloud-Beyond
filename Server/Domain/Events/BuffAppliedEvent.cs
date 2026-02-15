using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a Buff is successfully applied (registered) to a target.
    /// This is now a committable event that handles the application logic.
    /// </summary>
    [GameEventMetadata("获得临时状态", "当一个临时状态被成功应用到目标时触发的事件。", EventType.BuffApplied)]
    public class BuffAppliedEvent : GameEventBase, ICommittableEvent
    {
        /// <summary>
        /// The Buff instance that was applied.
        /// Contains details about the source, target, duration, and effects.
        /// </summary>
        [GameEventParameter("应用的Buff", "被应用的Buff实例。")]
        public Buff AppliedBuff { get; }

        [GameEventParameter("Buff来源", "应用Buff的来源卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? BuffSource => Source?.Card;

        [GameEventParameter("Buff目标", "被应用Buff的目标卡牌。", DslDisplayTypeName = "事件目标")]
        public CardInstance? BuffTarget => Target.FirstOrDefault()?.Card;

        public BuffAppliedEvent(Buff appliedBuff, CardInstance? target, CardInstance? source, Guid? eventId = null, DateTime? timestamp = null)
            : base(EventType.BuffApplied, eventId, timestamp)
        {
            AppliedBuff = appliedBuff ?? throw new ArgumentNullException(nameof(appliedBuff));
            if (target != null)
            {
                Target.Add(TargetInfo.ForCard(target));
            }
            if (source != null)
            {
                Source = TargetInfo.ForCard(source);
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult { Success = true };
            var logger = context.ServiceProvider.GetService<ILogger<BuffAppliedEvent>>();
            var sourceAbility = context.AbilityManager.GetAbilityByRuntimeId(AppliedBuff.SourceAbilityId);
            var sourceCardId = sourceAbility?.SourceCardId ?? AppliedBuff.SourceCardInstanceId;
            var sourceCardName = sourceAbility?.SourceCard?.Name ?? AppliedBuff.SourceCardName;
            var sourceAbilityName = sourceAbility?.Name ?? AppliedBuff.SourceAbilityName;

            logger?.LogInformation(
                "[BuffAppliedEvent] Buff {BuffId} applied. TargetCardId={TargetCardId} EffectKey={EffectKey} SourceAbilityId={SourceAbilityId} SourceAbilityName={SourceAbilityName} SourceCardId={SourceCardId} SourceCardName={SourceCardName} SourceEffectIndex={SourceEffectIndex} DurationType={DurationType} DefinitionId={DefinitionId} IsActive={IsActive}",
                AppliedBuff.Id,
                AppliedBuff.TargetCardInstanceId,
                AppliedBuff.EffectKey ?? "<null>",
                AppliedBuff.SourceAbilityId,
                sourceAbilityName ?? "<null>",
                sourceCardId,
                sourceCardName ?? "<null>",
                AppliedBuff.SourceEffectIndex,
                AppliedBuff.DurationType,
                AppliedBuff.DefinitionId ?? "<null>",
                AppliedBuff.IsActive);

            if (string.IsNullOrWhiteSpace(AppliedBuff.EffectKey))
            {
                logger?.LogInformation(
                    "[BuffAppliedEvent] Buff {BuffId} has empty EffectKey. SourceAbilityId={SourceAbilityId} SourceEffectIndex={SourceEffectIndex}.",
                    AppliedBuff.Id,
                    AppliedBuff.SourceAbilityId,
                    AppliedBuff.SourceEffectIndex);
            }

            var targetCard = BuffTarget ?? context.CurrentGameState.FindCardInstance(AppliedBuff.TargetCardInstanceId);
            int? previousCurrentHealth = null;
            if (AppliedBuff.PreserveCurrentHealthOnApply && targetCard != null)
            {
                var previousMaxHealth = context.BuffService.GetEffectiveMaxHealth(targetCard, context);
                previousCurrentHealth = Math.Max(0, previousMaxHealth - targetCard.Damage);
            }

            // The commit action is now the single, atomic operation of registering the buff.
            // All business logic (stacking, etc.) has already been processed and resulted in this event.
            context.BuffManager.RegisterBuffInstance(this.AppliedBuff, context.CurrentGameState);
            context.CurrentGameState.IncrementBuffRevision();
            context.BuffService.InvalidateDerivedState(context.CurrentGameState, this.AppliedBuff.TargetCardInstanceId);

            if (AppliedBuff.PreserveCurrentHealthOnApply && targetCard != null && previousCurrentHealth.HasValue)
            {
                var newMaxHealth = context.BuffService.GetEffectiveMaxHealth(targetCard, context);
                var cappedCurrentHealth = Math.Min(previousCurrentHealth.Value, newMaxHealth);
                targetCard.Damage = Math.Max(0, newMaxHealth - cappedCurrentHealth);
            }

            if (targetCard != null)
            {
                foreach (var tribe in AppliedBuff.GetAddedTribes())
                {
                    targetCard.AddTribeMarker(tribe);
                }
            }

            var suppressionEffects = AppliedBuff.Effects.OfType<AbilitySuppressionBuffEffect>().ToList();
            if (suppressionEffects.Count > 0)
            {
                var abilities = context.AbilityManager.GetAbilitiesForCard(AppliedBuff.TargetCardInstanceId);
                var suppressedAbilities = AbilitySuppressionEvaluator.CollectSuppressedAbilities(abilities, suppressionEffects);
                var suppressedIds = suppressedAbilities.Select(ability => ability.RuntimeId).ToList();
                if (suppressedIds.Count > 0)
                {
                    var auraResult = context.AuraManager.UnregisterBySourceAbilities(suppressedIds, context);
                    result.Merge(auraResult);
                    if (!auraResult.Success)
                    {
                        return auraResult;
                    }
                }
            }

            return result;
        }
    }
}
