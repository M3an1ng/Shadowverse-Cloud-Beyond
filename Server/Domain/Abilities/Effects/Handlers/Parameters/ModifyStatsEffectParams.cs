// File: Server/Domain/Abilities/Effects/Handlers/Parameters/ModifyStatsEffectParams.cs

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the ModifyStats effect, used for simple stat additions/subtractions.
    /// HealthModifier affects MaxHealth, and CurrentHealth is adjusted accordingly.
    /// </summary>
    public class ModifyStatsEffectParams : BaseEffectParams
    {
        /// <summary>
        /// The amount to add to the target's Attack. Can be negative.
        /// Null or 0 means no change to Attack.
        /// </summary>
        public int? AttackModifier { get; set; }

        /// <summary>
        /// The amount to add to the target's MaxHealth. Can be negative.
        /// CurrentHealth will be adjusted by the same amount, maintaining the proportion or being capped.
        /// Null or 0 means no change to Health.
        /// </summary>
        public int? HealthModifier { get; set; }

        /// <summary>
        /// The amount to add to the target's Cost. Can be negative.
        /// Null or 0 means no change to Cost.
        /// </summary>
        public int? CostModifier { get; set; }
    }
}