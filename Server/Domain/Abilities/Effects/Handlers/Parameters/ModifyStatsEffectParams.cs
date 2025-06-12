// File: Server/Domain/Abilities/Effects/Handlers/Parameters/ModifyStatsEffectParams.cs
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters specific to the ModifyStats effect.
    /// Allows modifying Attack, Health, and MaxHealth of target CardInstances.
    /// Modifications are relative (added to the current value). Use null or 0 for no change.
    /// </summary>
    public class ModifyStatsEffectParams : BaseEffectParams
    {
        /// <summary>
        /// The amount to add to the target's current Attack. Can be negative.
        /// Null or 0 means no change to Attack.
        /// </summary>
        public int? AttackModifier { get; set; }

        /// <summary>
        /// The amount to add to the target's current Health. Can be negative.
        /// Health will not exceed MaxHealth or go below 1 (unless destroyed).
        /// Null or 0 means no change to Health.
        /// </summary>
        public int? HealthModifier { get; set; }

        /// <summary>
        /// The amount to add to the target's MaxHealth. Can be negative.
        /// If MaxHealth increases, CurrentHealth typically increases by the same amount.
        /// If MaxHealth decreases, CurrentHealth is capped at the new MaxHealth.
        /// Null or 0 means no change to MaxHealth.
        /// </summary>
        public int? MaxHealthModifier { get; set; }

        // Consider adding flags for absolute set vs relative modify later if needed
        // public bool SetAttackAbsolute { get; set; } = false;
        // public bool SetHealthAbsolute { get; set; } = false;
        // public bool SetMaxHealthAbsolute { get; set; } = false;
    }
}