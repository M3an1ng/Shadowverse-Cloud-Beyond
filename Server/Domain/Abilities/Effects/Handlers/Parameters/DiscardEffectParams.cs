namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 弃牌效果参数
    /// </summary>
    public class DiscardEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 弃牌原因描述
        /// </summary>
        public string DiscardReason { get; set; } = "Effect";
    }
}
