using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 回合开始效果参数
    /// 包含执行回合开始操作所需的所有参数
    /// </summary>
    [EffectMetadata("开始回合", "强制开始指定玩家的回合。忽略卡牌目标，按玩家范围结算。")]
    public class TurnStartEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择受到影响的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        /// <summary>
        /// 是否强制开始回合（跳过某些验证）
        /// </summary>
        [ParameterMetadata("强制开始", UiControlType.Checkbox, DefaultValue = false, Description = "是否强制开始回合，跳过某些验证。")]
        public bool ForceStartTurn { get; set; } = false;

    }
}
