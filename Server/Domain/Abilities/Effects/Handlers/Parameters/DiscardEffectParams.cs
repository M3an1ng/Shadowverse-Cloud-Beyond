using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 弃牌效果参数
    /// </summary>
    [EffectMetadata("弃牌", "强制弃置手牌中的卡牌。使用卡牌目标。")]
    public class DiscardEffectParams : BaseEffectParams
    {
        // /// <summary>
        // /// 弃牌原因描述
        // /// </summary>
        // [ParameterMetadata("弃牌原因", UiControlType.TextInput, DefaultValue = "Effect", Description = "弃牌的原因描述，用于日志记录和特殊逻辑判断。")]
        // public string DiscardReason { get; set; } = "Effect";
    }
}
