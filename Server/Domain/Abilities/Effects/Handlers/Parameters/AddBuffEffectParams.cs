using System;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

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
        [ParameterMetadata("Buff定义ID", UiControlType.TextInput, DefaultValue = "00000000-0000-0000-0000-000000000000", Description = "要添加的Buff定义的唯一标识符。")]
        public Guid BuffDefinitionId { get; set; }

        /// <summary>
        /// Buff持续时间（回合数，-1表示永久）
        /// </summary>
        [ParameterMetadata("持续时间", UiControlType.NumberStepper, DefaultValue = -1, Min = -1, Description = "Buff的持续回合数。-1表示永久效果。")]
        public int Duration { get; set; } = -1;

        /// <summary>
        /// 是否可以叠加
        /// </summary>
        [ParameterMetadata("可叠加", UiControlType.Checkbox, DefaultValue = true, Description = "该Buff是否可以与同类型Buff叠加。")]
        public bool CanStack { get; set; } = true;
    }
}
