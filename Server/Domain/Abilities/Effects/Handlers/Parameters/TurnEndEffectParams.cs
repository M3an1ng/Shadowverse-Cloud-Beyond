using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 回合结束效果参数
    /// 包含执行回合结束操作所需的所有参数
    /// </summary>
    [EffectMetadata("结束回合", "强制结束指定玩家的回合。")]
    public class TurnEndEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 结束回合的玩家ID
        /// </summary>
        [ParameterMetadata("玩家ID", UiControlType.TextInput, DefaultValue = "00000000-0000-0000-0000-000000000000", Description = "要结束回合的玩家唯一标识符。")]
        public Guid EndingPlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// 是否强制结束回合（跳过某些验证）
        /// </summary>
        [ParameterMetadata("强制结束", UiControlType.Checkbox, DefaultValue = false, Description = "是否强制结束回合，跳过某些验证。")]
        public bool ForceEndTurn { get; set; } = false;

        /// <summary>
        /// 回合结束原因描述
        /// </summary>
        [ParameterMetadata("结束原因", UiControlType.TextInput, DefaultValue = "PlayerAction", Description = "回合结束的原因描述，用于日志记录和特殊逻辑判断。")]
        public string EndTurnReason { get; set; } = "PlayerAction";
    }
}
