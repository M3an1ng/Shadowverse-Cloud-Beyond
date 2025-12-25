using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace
{
    /// <summary>
    /// Parameters for the ModifyPP effect.
    /// </summary>
    [EffectMetadata("修改PP", "修改玩家当前的PP值。")]
    public class ModifyPPEffectParams : BaseEffectParams // Corrected base class
    {
        /// <summary>
        /// The amount to modify the player's current Play Points by (can be positive or negative).
        /// </summary>
        [ParameterMetadata("修改量", UiControlType.ValueConfigEditor, DefaultValue = 1, Description = "要修改的PP数量。可以为正数（增加）或负数（减少）。")]
        public int ModifyAmount { get; set; }
    }
}