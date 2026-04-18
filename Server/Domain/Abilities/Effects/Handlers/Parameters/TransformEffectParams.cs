using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the Transform effect.
    /// </summary>
    [EffectMetadata("变形", "将目标变形为指定卡牌，或变形为 DSL 参数指定复制源的复制，不触发死亡效果。使用卡牌目标。", AbilityEditorCategories.TempStateId)]
    public class TransformEffectParams : BaseEffectParams
    {
        [ParameterMetadata("卡牌来源", UiControlType.Dropdown, DefaultValue = CardSourceMode.Definition, Description = "定义模式使用目标卡牌ID；复制模式使用 DSL 参数选择复制源卡牌。")]
        public CardSourceMode CardSourceMode { get; set; } = CardSourceMode.Definition;

        /// <summary>
        /// The ID of the CardDefinition the target card instance should transform into.
        /// </summary>
        [ParameterMetadata("目标卡牌ID", UiControlType.CardDefinitionPicker, DefaultValue = 0, Min = 1, Description = "目标要变形成的卡牌定义ID。")]
        public int TargetCardDefinitionId { get; set; }

        [ParameterMetadata("复制源卡牌", UiControlType.TextArea, DefaultValue = null, Description = "复制模式下使用。填写返回单张卡牌实例的 DSL 表达式，也可传入卡牌实例 / TargetInfo / Guid。")]
        public object? CopySourceTargets { get; set; }
    }
}
