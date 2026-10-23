using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace
{
    /// <summary>
    /// Parameters for the Evolve effect. Currently empty as the target is the primary input.
    /// </summary>
    [EffectMetadata("进化", "使目标随从进化，获得进化后的属性和能力。使用卡牌目标。")]
    public class EvolveEffectParams : BaseEffectParams // Corrected base class
    {
        [ParameterMetadata("是否视为手动进化", UiControlType.Checkbox, DefaultValue = false, Description = "开启后生成的进化事件会标记为手动进化。默认关闭，用于兼容已有卡牌效果。")]
        public bool IsManualEvolution { get; set; } = false;
    }
}
