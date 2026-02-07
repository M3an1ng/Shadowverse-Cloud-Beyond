using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

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
        [ParameterMetadata("移除数量", UiControlType.NumberStepper, DefaultValue = -1, Min = -1, Description = "要移除的Buff数量。-1表示移除所有匹配的Buff。")]
        public int RemoveCount { get; set; } = -1;
    }
}
