using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace
{
    /// <summary>
    /// Parameters for the Transform effect.
    /// </summary>
    [EffectMetadata("变形", "将目标变形为指定的卡牌，不触发死亡效果。使用卡牌目标。")]
    public class TransformEffectParams : BaseEffectParams // Corrected base class
    {
        /// <summary>
        /// The ID of the CardDefinition the target card instance should transform into.
        /// </summary>
        [ParameterMetadata("目标卡牌ID", UiControlType.NumberStepper, DefaultValue = 0, Min = 1, Description = "目标要变形成的卡牌定义ID。")]
        public int TargetCardDefinitionId { get; set; }
    }
}
