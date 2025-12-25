using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 移除Buff效果参数
    /// </summary>
    [EffectMetadata("移除Buff", "移除目标身上的增益或减益效果。")]
    public class RemoveBuffEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 要移除的Buff名称（为空则移除所有Buff）
        /// </summary>
        [ParameterMetadata("Buff名称", UiControlType.TextInput, DefaultValue = "", Description = "要移除的Buff名称。留空表示移除所有Buff。")]
        public string? BuffName { get; set; }

        /// <summary>
        /// 移除Buff的数量（-1表示移除所有匹配的Buff）
        /// </summary>
        [ParameterMetadata("移除数量", UiControlType.NumberStepper, DefaultValue = -1, Min = -1, Description = "要移除的Buff数量。-1表示移除所有匹配的Buff。")]
        public int RemoveCount { get; set; } = -1;

        /// <summary>
        /// 是否只移除负面Buff
        /// </summary>
        [ParameterMetadata("仅负面Buff", UiControlType.Checkbox, DefaultValue = false, Description = "是否只移除负面效果的Buff。")]
        public bool OnlyNegativeBuffs { get; set; } = false;

        /// <summary>
        /// 是否只移除正面Buff
        /// </summary>
        [ParameterMetadata("仅正面Buff", UiControlType.Checkbox, DefaultValue = false, Description = "是否只移除正面效果的Buff。")]
        public bool OnlyPositiveBuffs { get; set; } = false;
    }
}
