using System;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 回合开始效果参数
    /// 包含执行回合开始操作所需的所有参数
    /// </summary>
    public class TurnStartEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 开始回合的玩家ID
        /// </summary>
        public Guid StartingPlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// 当前回合数
        /// TODO: 删除，从PlayerState获取
        /// </summary>
        public int TurnNumber { get; set; } = 0;

        /// <summary>
        /// 是否强制开始回合（跳过某些验证）
        /// </summary>
        public bool ForceStartTurn { get; set; } = false;

        /// <summary>
        /// 回合开始原因描述
        /// </summary>
        public string StartTurnReason { get; set; } = "TurnTransition";
    }
}
