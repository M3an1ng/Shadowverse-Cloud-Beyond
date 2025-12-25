using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace
{
    /// <summary>
    /// Parameters for the ModifyEP effect.
    /// </summary>
    [EffectMetadata("修改EP", "修改玩家当前的进化点数。")]
    public class ModifyEPEffectParams : BaseEffectParams // Corrected base class
    {
        /// <summary>
        /// The amount to modify the player's current Evolution Points by (can be positive or negative).
        /// </summary>
        [ParameterMetadata("修改量", UiControlType.ValueConfigEditor, DefaultValue = 1, Description = "要修改的EP数量。可以为正数（增加）或负数（减少）。")]
        public int ModifyAmount { get; set; }
    }
}