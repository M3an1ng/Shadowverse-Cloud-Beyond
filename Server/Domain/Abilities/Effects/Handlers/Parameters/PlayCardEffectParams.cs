using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 出牌效果参数
    /// 包含执行出牌操作所需的所有参数
    /// </summary>
    [EffectMetadata("使用卡牌", "强制打出指定的卡牌。忽略卡牌目标，按玩家范围结算。")]
    public class PlayCardEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择执行出牌的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        /// <summary>
        /// 要打出的卡牌实例ID
        /// </summary>
        [ParameterMetadata("卡牌实例ID", UiControlType.TextInput, DefaultValue = "00000000-0000-0000-0000-000000000000", Description = "要打出的卡牌实例的唯一标识符。")]
        public Guid CardInstanceId { get; set; } = Guid.Empty;

        /// <summary>
        /// 目标ID列表（如果卡牌需要目标）
        /// </summary>
        [ParameterMetadata("目标ID列表", UiControlType.TextInput, DefaultValue = "", Description = "卡牌目标的唯一标识符列表，以逗号分隔。")]
        public List<Guid>? TargetIds { get; set; }

        /// <summary>
        /// 卡牌定义ID（用于获取卡牌基础信息）
        /// </summary>
        [ParameterMetadata("卡牌定义ID", UiControlType.CardDefinitionPicker, DefaultValue = 0, Min = 1, Description = "卡牌定义的ID，用于获取卡牌基础信息。")]
        public int CardDefinitionId { get; set; }

        /// <summary>
        /// 是否强制执行（跳过某些验证）
        /// </summary>
        [ParameterMetadata("强制出牌", UiControlType.Checkbox, DefaultValue = false, Description = "是否强制执行出牌操作，跳过某些验证。")]
        public bool ForcePlay { get; set; } = false;

        /// <summary>
        /// 出牌原因描述
        /// </summary>
        [ParameterMetadata("出牌原因", UiControlType.TextInput, DefaultValue = "PlayerAction", Description = "出牌的原因描述，用于日志记录和特殊逻辑判断。")]
        public string PlayReason { get; set; } = "PlayerAction";
    }
}
