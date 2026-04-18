using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    /// <summary>
    /// Represents a dynamic effect state applied to a CardInstance.
    /// It manages its own lifecycle (duration, removal) and contains specific effects (IBuffEffect).
    /// </summary>
    public class Buff
    {
        // --- Core Identification & Lifecycle ---
        /// <summary> Unique identifier for this specific buff *instance*. </summary>
        public Guid Id { get; init; } = Guid.NewGuid(); // Auto-generate instance ID

        /// <summary> Identifier for the predefined buff template, if applicable. Null for inline buffs. </summary>
        public string? DefinitionId { get; init; } // Added for predefined buffs

        /// <summary> The ID of the ability instance that created this buff. </summary>
        public Guid SourceAbilityId { get; init; }

        /// <summary>
        /// Display-only root ability runtime id used to group buff summaries by primary ability.
        /// </summary>
        public Guid? SummaryRootAbilityId { get; set; }

        /// <summary> Index of the effect within the source ability that created this buff. </summary>
        public int? SourceEffectIndex { get; set; }

        /// <summary> Non-stackable key for replacement scope; null/empty means stackable. </summary>
        public string? EffectKey { get; set; }

        /// <summary> The ID of the source card instance, if available. </summary>
        public Guid? SourceCardInstanceId { get; set; }

        /// <summary> Cached source card name for UI summaries when abilities are no longer available. </summary>
        public string? SourceCardName { get; set; }

        /// <summary> Cached source ability name for UI summaries when abilities are no longer available. </summary>
        public string? SourceAbilityName { get; set; }

        /// <summary> The ID of the card instance this buff is applied to. </summary>
        public Guid TargetCardInstanceId { get; init; }

        /// <summary> Indicates if the buff is currently active. Can be set to false by Silence effects or other mechanics. </summary>
        public bool IsActive { get; set; } = true;

        /// <summary> Turn number when the buff was applied. </summary>
        public int AppliedTurn { get; init; }

        // --- Duration & Removal Logic ---
        /// <summary> Specifies how long the buff lasts or how it gets removed. </summary>
        public DurationType DurationType { get; init; } = DurationType.Permanent;

        /// <summary> Parameter associated with the duration type (e.g., number of turns). </summary>
        public int? DurationParameter { get; set; }

        /// <summary>
        /// When true, applying this buff keeps the target's current health unchanged (capped by new max health).
        /// This is evaluated during BuffAppliedEvent commit, after the buff becomes active.
        /// </summary>
        public bool PreserveCurrentHealthOnApply { get; set; } = false;

        /// <summary> Optional trigger condition that can cause the buff to be removed. </summary>
        public TriggerConfig? RemovalTrigger { get; init; }

        /// <summary> Optional condition that must be met for the RemovalTrigger to actually remove the buff. </summary>
        public ConditionConfig? RemovalCondition { get; init; }

        // --- Applicability Condition (for Conditional/Aura Buffs) ---
        /// <summary>
        /// Deprecated: ApplicabilityCondition is no longer evaluated at runtime.
        /// Conditional buffs should be implemented via aura refresh logic instead.
        /// </summary>
        public ConditionConfig? ApplicabilityCondition { get; init; }

        // --- Contained Effects ---
        /// <summary>
        /// List of specific effects (stat changes, keywords, granted abilities) contained within this buff.
        /// </summary>
        public List<IBuffEffect> Effects { get; init; } = new();

        // --- Constructor ---
        // Note: A more robust constructor or factory method might be needed depending on how Buffs are created.
        public Buff(
            Guid sourceAbilityId,
            Guid targetCardInstanceId,
            int currentTurn,
            List<IBuffEffect> effects,
            string? definitionId = null, // Added optional definitionId
            DurationType durationType = DurationType.Permanent, // Allow setting duration etc. via constructor
            int? durationParameter = null,
            TriggerConfig? removalTrigger = null,
            ConditionConfig? removalCondition = null,
            ConditionConfig? applicabilityCondition = null)
        {
            // Guid.Empty is a valid "No-Source" marker for system-generated buffs.
            SourceAbilityId = sourceAbilityId;
            TargetCardInstanceId = targetCardInstanceId == Guid.Empty ? throw new ArgumentException("TargetCardInstanceId cannot be empty", nameof(targetCardInstanceId)) : targetCardInstanceId;
            AppliedTurn = currentTurn;
            Effects = effects == null ? throw new ArgumentNullException(nameof(effects)) : new List<IBuffEffect>(effects);
            DefinitionId = definitionId; // Set DefinitionId
            DurationType = durationType;
            DurationParameter = durationType == DurationType.ConditionalRemoval
                ? null
                : durationParameter;
            RemovalTrigger = removalTrigger;
            RemovalCondition = removalCondition;
            ApplicabilityCondition = applicabilityCondition;
        }

        // --- Helper methods to access specific effect types ---
        public IEnumerable<StatModification> GetStatModifications() => Effects.OfType<StatBuffEffect>().SelectMany(e => e.Modifications);
        public IEnumerable<ActionUsageLimitModification> GetActionUsageLimitModifications() => Effects.OfType<ActionUsageLimitBuffEffect>().SelectMany(e => e.Modifications);
        public IEnumerable<CardKeyword> GetGrantedKeywords() => Effects.OfType<KeywordBuffEffect>().SelectMany(e => e.GrantedKeywords);
        public IEnumerable<AbilityConfig> GetGrantedAbilityConfigs() => Effects.OfType<AbilityBuffEffect>().SelectMany(e => e.GrantedAbilityConfigs);
        public IEnumerable<string> GetAddedTribes() => Effects.OfType<TribeBuffEffect>().SelectMany(e => e.AddedTribes);
    }
}
