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

        /// <summary> The ID of the card instance this buff is applied to. </summary>
        public Guid TargetCardInstanceId { get; init; }

        /// <summary> Indicates if the buff is currently active. Can be set to false by Silence effects or other mechanics. </summary>
        public bool IsActive { get; set; } = true;

        /// <summary> Turn number when the buff was applied. </summary>
        public int AppliedTurn { get; init; }

        // --- Duration & Removal Logic ---
        /// <summary> Specifies how long the buff lasts or how it gets removed. </summary>
        public DurationType DurationType { get; init; } = DurationType.Permanent;

        /// <summary> Parameter associated with the duration type (e.g., number of turns, specific event name). </summary>
        public object? DurationParameter { get; init; }

        /// <summary> Optional trigger condition that can cause the buff to be removed. </summary>
        public TriggerConfig? RemovalTrigger { get; init; }

        /// <summary> Optional condition that must be met for the RemovalTrigger to actually remove the buff. </summary>
        public ConditionConfig? RemovalCondition { get; init; }

        /// <summary> 指示此Buff是否不可叠加。当为true时，相同源的新Buff会替换此Buff。 </summary>
        public bool IsNonStackable { get; init; } = false;

        // --- Applicability Condition (for Conditional/Aura Buffs) ---
        /// <summary>
        /// If not null, the buff's effects are only applied when this condition evaluates to true in the current game context.
        /// Checked during state calculation.
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
            object? durationParameter = null,
            TriggerConfig? removalTrigger = null,
            ConditionConfig? removalCondition = null,
            ConditionConfig? applicabilityCondition = null,
            bool isNonStackable = false)
        {
            // Guid.Empty is a valid "No-Source" marker for system-generated buffs.
            SourceAbilityId = sourceAbilityId;
            TargetCardInstanceId = targetCardInstanceId == Guid.Empty ? throw new ArgumentException("TargetCardInstanceId cannot be empty", nameof(targetCardInstanceId)) : targetCardInstanceId;
            AppliedTurn = currentTurn;
            Effects = effects == null ? throw new ArgumentNullException(nameof(effects)) : new List<IBuffEffect>(effects);
            DefinitionId = definitionId; // Set DefinitionId
            DurationType = durationType;
            DurationParameter = durationParameter;
            RemovalTrigger = removalTrigger;
            RemovalCondition = removalCondition;
            ApplicabilityCondition = applicabilityCondition;
            IsNonStackable = isNonStackable;
        }

        // --- Helper methods to access specific effect types ---
        public IEnumerable<StatModification> GetStatModifications() => Effects.OfType<StatBuffEffect>().SelectMany(e => e.Modifications);
        public IEnumerable<CardKeyword> GetGrantedKeywords() => Effects.OfType<KeywordBuffEffect>().SelectMany(e => e.GrantedKeywords);
        public IEnumerable<AbilityConfig> GetGrantedAbilityConfigs() => Effects.OfType<AbilityBuffEffect>().SelectMany(e => e.GrantedAbilityConfigs);
    }
}
