using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events; // For GameContext
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        /// <summary> The ability instance that created this buff. </summary>
        public Ability SourceAbility { get; init; }

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
            Ability sourceAbility,
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
            SourceAbility = sourceAbility ?? throw new ArgumentNullException(nameof(sourceAbility));
            TargetCardInstanceId = targetCardInstanceId == Guid.Empty ? throw new ArgumentException("TargetCardInstanceId cannot be empty", nameof(targetCardInstanceId)) : targetCardInstanceId;
            AppliedTurn = currentTurn;
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
            DefinitionId = definitionId; // Set DefinitionId
            DurationType = durationType;
            DurationParameter = durationParameter;
            RemovalTrigger = removalTrigger;
            RemovalCondition = removalCondition;
            ApplicabilityCondition = applicabilityCondition;
            IsNonStackable = isNonStackable;
        }

        // --- Core Logic Methods ---

        /// <summary>
        /// Checks if this Buff's effects should be considered active and applicable in the current game state.
        /// This involves checking IsActive, Duration/Removal logic, and the ApplicabilityCondition.
        /// </summary>
        /// <param name="target">The card instance the buff is applied to.</param>
        /// <param name="context">The current game context.</param>
        /// <param name="conditionEvaluator">Service to evaluate conditions.</param>
        /// <returns>True if the buff's effects are currently applicable, false otherwise.</returns>
        public async Task<bool> IsCurrentlyApplicableAsync(CardInstance target, GameContext context, IConditionEvaluator conditionEvaluator)
        {
            // 1. Basic active check
            if (!IsActive)
            {
                return false;
            }

            // 2. Check if expired based on duration/removal logic (Needs implementation)
            if (await HasExpiredAsync(context, conditionEvaluator)) // Pass evaluator if needed for RemovalCondition
            {
                return false;
            }

            // 3. Check applicability condition if it exists
            if (ApplicabilityCondition != null)
            {
                // Use the GameContext directly for condition evaluation
                bool conditionMet = await conditionEvaluator.EvaluateAsync(ApplicabilityCondition, context);
                if (!conditionMet)
                {
                    return false;
                }
            }

            // 4. If all checks pass, the buff is applicable
            return true;
        }

        /// <summary>
        /// Determines if the buff has expired based on its DurationType, DurationParameter, and RemovalTrigger/Condition.
        /// Needs detailed implementation based on game rules.
        /// </summary>
        private async Task<bool> HasExpiredAsync(GameContext context, IConditionEvaluator conditionEvaluator)
        {
            // TODO: Implement expiration logic based on DurationType
            switch (DurationType)
            {
                case DurationType.Permanent:
                    return false; // Permanent buffs don't expire on their own

                case DurationType.EndOfTurn:
                    // Check if the current phase is after the EndOfTurn phase of the turn it was applied + duration
                    // Requires tracking turn count or specific phase events.
                    // Example: return context.CurrentTurn > AppliedTurn && context.CurrentPhase == GamePhase.EndOfTurn; (Simplified)
                    break;

                case DurationType.StartOfNextTurn:
                    // Check if the current phase is StartOfTurn and turn number > AppliedTurn
                    // Example: return context.CurrentTurn > AppliedTurn && context.CurrentPhase == GamePhase.StartOfTurn; (Simplified)
                    break;

                case DurationType.LimitedTurns:
                    if (DurationParameter is int turns)
                    {
                        // Example: return context.CurrentTurn > AppliedTurn + turns;
                    }
                    break;

                case DurationType.SpecificEvent:
                    // This type implies removal is handled externally by listening for the specific event
                    // and calling BuffManager.RemoveBuff(). So, HasExpired itself might return false here.
                    // Alternatively, RemovalTrigger could be used.
                    return false; // Removal handled elsewhere

                case DurationType.ConditionalRemoval:
                    // This relies on RemovalTrigger and RemovalCondition being checked externally,
                    // potentially by the BuffManager listening to events specified in RemovalTrigger.
                    // Or, if checked here, needs access to the triggering event.
                    // Example (if checked periodically):
                    // if (RemovalCondition != null) {
                    //     var evalContext = context.CreateEvaluationContext(...);
                    //     return await conditionEvaluator.EvaluateAsync(RemovalCondition, evalContext);
                    // }
                    return false; // Assume removal is handled externally based on trigger/condition
            }

            // Default: Not expired if logic not fully implemented
            return false;
        }


        // --- Helper methods to access specific effect types ---
        public IEnumerable<StatModification> GetStatModifications() => Effects.OfType<StatBuffEffect>().SelectMany(e => e.Modifications);
        public IEnumerable<string> GetGrantedKeywords() => Effects.OfType<KeywordBuffEffect>().SelectMany(e => e.GrantedKeywords);
        public IEnumerable<AbilityConfig> GetGrantedAbilityConfigs() => Effects.OfType<AbilityBuffEffect>().SelectMany(e => e.GrantedAbilityConfigs);
    }
}