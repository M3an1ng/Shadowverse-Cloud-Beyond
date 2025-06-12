// File: Server/Domain/Abilities/Effects/Handlers/Parameters/BaseTargetEffectParams.cs
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetConfig if needed later

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Base parameters for effects that operate on one or more targets.
    /// Inherits common parameters from BaseEffectParams.
    /// </summary>
    public abstract class BaseTargetEffectParams : BaseEffectParams
    {
        // Currently, target selection logic is primarily handled by the EffectConfig's TargetSelectorId
        // and resolved by the TargetSelectionService before the handler is called.
        // The resolved targets are passed via GameContext.ResolvedTargets.
        // Therefore, this base class might not need additional target-specific properties right now,
        // but serves as a marker for target-based effects.

        // Example potential future property:
        // /// <summary>
        // /// Overrides the default target selection logic if specified.
        // /// </summary>
        // public TargetConfig? OverrideTargetConfig { get; set; }
    }
}