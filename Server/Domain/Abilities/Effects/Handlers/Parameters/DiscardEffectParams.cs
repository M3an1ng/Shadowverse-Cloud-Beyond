namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 弃牌效果参数
    /// </summary>
    public class DiscardEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 弃牌数量
        /// </summary>
        public int DiscardCount { get; set; } = 1;

        /// <summary>
        /// 是否随机弃牌（false表示玩家选择）
        /// </summary>
        public bool IsRandom { get; set; } = false;

        /// <summary>
        /// 弃牌原因描述
        /// </summary>
        public string DiscardReason { get; set; } = "Effect";

        /// <summary>
        /// 是否触发弃牌相关效果
        /// </summary>
        public bool TriggerDiscardEffects { get; set; } = true;
    }
}
