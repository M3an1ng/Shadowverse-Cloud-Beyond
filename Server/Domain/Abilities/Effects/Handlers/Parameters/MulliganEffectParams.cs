using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 换牌效果参数
    /// 包含执行换牌操作所需的所有参数
    /// 目标玩家通过 GameContext.SelectedTargets 传递，遵循统一的 EffectHandler 架构模式
    /// </summary>
    [EffectMetadata("换牌", "允许玩家重新抽取指定的手牌。")]
    public class MulliganEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 要替换的卡牌UID列表
        /// </summary>
        [ParameterMetadata("卡牌UID列表", UiControlType.TextInput, DefaultValue = "", Description = "要替换的卡牌唯一标识符列表，以逗号分隔。")]
        public List<Guid> CardUidsToReplace { get; set; } = new List<Guid>();

        /// <summary>
        /// 是否强制完成换牌（跳过某些验证）
        /// </summary>
        [ParameterMetadata("强制换牌", UiControlType.Checkbox, DefaultValue = false, Description = "是否强制完成换牌操作，跳过某些验证。")]
        public bool ForceMulligan { get; set; } = false;

        /// <summary>
        /// 换牌原因描述
        /// </summary>
        [ParameterMetadata("换牌原因", UiControlType.TextInput, DefaultValue = "PlayerChoice", Description = "换牌的原因描述，用于日志记录和特殊逻辑判断。")]
        public string MulliganReason { get; set; } = "PlayerChoice";
    }
}
