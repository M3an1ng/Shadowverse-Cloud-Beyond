namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 回合结束效果参数
    /// 包含执行回合结束操作所需的所有参数
    /// </summary>
    public class TurnEndEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 结束回合的玩家ID
        /// </summary>
        public Guid EndingPlayerId { get; set; } = Guid.Empty;

        /// <summary>
        /// 当前回合数
        /// </summary>
        public int TurnNumber { get; set; } = 0;

        /// <summary>
        /// 是否强制结束回合（跳过某些验证）
        /// </summary>
        public bool ForceEndTurn { get; set; } = false;

        /// <summary>
        /// 回合结束原因描述
        /// </summary>
        public string EndTurnReason { get; set; } = "PlayerAction";
    }
}
