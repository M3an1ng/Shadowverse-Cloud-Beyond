namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 消失效果参数
    /// 与摧毁效果类似，但不触发死亡相关效果（如LastWords）
    /// </summary>
    public class VanishEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 消失原因描述
        /// </summary>
        public string VanishReason { get; set; } = "Effect";

        /// <summary>
        /// 是否忽略消失抗性
        /// </summary>
        public bool IgnoreVanishResist { get; set; } = false;
    }
}
