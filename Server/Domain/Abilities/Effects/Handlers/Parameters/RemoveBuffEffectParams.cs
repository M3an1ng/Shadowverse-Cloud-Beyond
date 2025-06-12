namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 移除Buff效果参数
    /// </summary>
    public class RemoveBuffEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 要移除的Buff名称（为空则移除所有Buff）
        /// </summary>
        public string? BuffName { get; set; }

        /// <summary>
        /// 移除Buff的数量（-1表示移除所有匹配的Buff）
        /// </summary>
        public int RemoveCount { get; set; } = -1;

        /// <summary>
        /// 是否只移除负面Buff
        /// </summary>
        public bool OnlyNegativeBuffs { get; set; } = false;

        /// <summary>
        /// 是否只移除正面Buff
        /// </summary>
        public bool OnlyPositiveBuffs { get; set; } = false;
    }
}
