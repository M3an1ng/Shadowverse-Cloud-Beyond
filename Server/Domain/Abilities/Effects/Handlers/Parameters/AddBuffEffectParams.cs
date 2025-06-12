namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 添加Buff效果参数
    /// </summary>
    public class AddBuffEffectParams : BaseEffectParams
    {
        /// <summary>
        /// Buff定义ID
        /// </summary>
        public Guid BuffDefinitionId { get; set; }

        /// <summary>
        /// Buff持续时间（回合数，-1表示永久）
        /// </summary>
        public int Duration { get; set; } = -1;

        /// <summary>
        /// Buff强度或层数
        /// </summary>
        public int Intensity { get; set; } = 1;

        /// <summary>
        /// 是否可以叠加
        /// </summary>
        public bool CanStack { get; set; } = true;
    }
}
