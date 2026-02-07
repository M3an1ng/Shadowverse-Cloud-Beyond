using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 消失效果参数
    /// 与摧毁效果类似，但不触发死亡相关效果（如LastWords）
    /// </summary>
    [EffectMetadata("消失", "使目标单位消失，不触发死亡效果。使用卡牌目标。")]
    public class VanishEffectParams : BaseEffectParams
    {
        // /// <summary>
        // /// 消失原因描述
        // /// </summary>
        // [ParameterMetadata("消失原因", UiControlType.TextInput, DefaultValue = "Effect", Description = "消失的原因描述，用于日志记录和特殊逻辑判断。")]
        // public string VanishReason { get; set; } = "Effect";

        // /// <summary>
        // /// 是否忽略消失抗性
        // /// </summary>
        // [ParameterMetadata("忽略抗性", UiControlType.Checkbox, DefaultValue = false, Description = "是否忽略目标的消失抗性。")]
        // public bool IgnoreVanishResist { get; set; } = false;
    }
}
