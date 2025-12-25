using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 回合开始效果参数
    /// 包含执行回合开始操作所需的所有参数
    /// </summary>
    [EffectMetadata("开始回合", "强制开始指定玩家的回合。")]
    public class TurnStartEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 开始回合的玩家ID
        /// </summary>
        [ParameterMetadata("玩家ID", UiControlType.TextInput, DefaultValue = "00000000-0000-0000-0000-000000000000", Description = "要开始回合的玩家唯一标识符。")]
        public Guid StartingPlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// 是否强制开始回合（跳过某些验证）
        /// </summary>
        [ParameterMetadata("强制开始", UiControlType.Checkbox, DefaultValue = false, Description = "是否强制开始回合，跳过某些验证。")]
        public bool ForceStartTurn { get; set; } = false;

        /// <summary>
        /// 回合开始原因描述
        /// </summary>
        [ParameterMetadata("开始原因", UiControlType.TextInput, DefaultValue = "TurnTransition", Description = "回合开始的原因描述，用于日志记录和特殊逻辑判断。")]
        public string StartTurnReason { get; set; } = "TurnTransition";
    }
}
