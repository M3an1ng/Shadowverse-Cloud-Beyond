using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 返回效果参数
    /// 将目标卡牌返回到指定区域（通常是手牌或牌库）
    /// </summary>
    [EffectMetadata("返回", "将目标卡牌返回到指定区域。使用卡牌目标。")]
    public class ReturnEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 返回目标区域
        /// </summary>
        [ParameterMetadata("目标区域", UiControlType.Dropdown, DefaultValue = CardZone.Hand, Description = "卡牌要返回到的目标区域。")]
        public CardZone TargetZone { get; set; } = CardZone.Hand;

        /// <summary>
        /// 返回原因描述
        /// </summary>
        [ParameterMetadata("返回原因", UiControlType.TextInput, DefaultValue = "Effect", Description = "返回的原因描述，用于日志记录和特殊逻辑判断。")]
        public string ReturnReason { get; set; } = "Effect";

        /// <summary>
        /// 是否触发返回相关效果
        /// </summary>
        [ParameterMetadata("触发返回效果", UiControlType.Checkbox, DefaultValue = true, Description = "是否触发与返回相关的其他效果。")]
        public bool TriggerReturnEffects { get; set; } = true;
    }
}
