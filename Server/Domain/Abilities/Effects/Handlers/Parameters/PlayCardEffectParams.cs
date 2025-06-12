namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 出牌效果参数
    /// 包含执行出牌操作所需的所有参数
    /// </summary>
    public class PlayCardEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 要打出的卡牌实例ID
        /// </summary>
        public Guid CardInstanceId { get; set; } = Guid.Empty;

        /// <summary>
        /// 目标ID（如果卡牌需要目标）
        /// </summary>
        public Guid? TargetId { get; set; }

        /// <summary>
        /// 卡牌定义ID（用于获取卡牌基础信息）
        /// </summary>
        public int CardDefinitionId { get; set; }

        /// <summary>
        /// 是否强制执行（跳过某些验证）
        /// </summary>
        public bool ForcePlay { get; set; } = false;

        /// <summary>
        /// 出牌原因描述
        /// </summary>
        public string PlayReason { get; set; } = "PlayerAction";
    }
}
