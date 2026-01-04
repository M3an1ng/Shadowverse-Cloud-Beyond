// File: Server/Domain/Abilities/Effects/Handlers/Parameters/DestroyUnitEffectParams.cs
using ShadowverseBeyond.Server.Domain.Entities; // For CardZone if needed later
using ShadowverseBeyond.Server.Domain.Constants; // For potential enums like DestroyReason
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the DestroyUnit effect.
    /// </summary>
    [EffectMetadata("摧毁", "直接摧毁目标单位，触发其死亡效果。使用卡牌目标。")]
    public class DestroyEffectParams : BaseEffectParams
    {
        /// <summary>
        /// The reason for the destruction (e.g., "Effect", "Combat", "Ability").
        /// Useful for logging or triggering specific Last Words variations.
        /// Defaults to "Effect".
        /// </summary>
        // [ParameterMetadata("摧毁原因", UiControlType.TextInput, DefaultValue = "Effect", Description = "摧毁的原因描述，用于日志记录和特殊逻辑判断。")]
        // public string DestroyReason { get; set; } = "Effect";

        // Potential future parameters:
        // public bool IgnoreWard { get; set; } = false; // If destruction targets through Ward
        // public bool IgnoreDestroyResist { get; set; } = false; // If destruction bypasses immunity
    }
}
