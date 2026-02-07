using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the RemoveAbility effect.
    /// </summary>
    [EffectMetadata("移除能力", "抑制目标卡牌的能力（可按ID/关键字/全部）。", AbilityEditorCategories.TempStateId)]
    public class RemoveAbilityEffectParams : BaseEffectParams
    {
        [ParameterMetadata("移除模式", UiControlType.Dropdown, DefaultValue = RemoveAbilityMode.Id, Description = "能力移除模式：ID/关键字/全部。", EnumName = nameof(RemoveAbilityMode))]
        public RemoveAbilityMode RemoveMode { get; set; } = RemoveAbilityMode.Id;

        [ParameterMetadata("能力ID列表", UiControlType.AbilityDefinitionPicker, Description = "按能力定义ID移除的列表。")]
        public List<long> AbilityDefinitionIds { get; set; } = new();

        [ParameterMetadata("能力关键字", UiControlType.Dropdown, DefaultValue = AbilityKeyword.None, Description = "按关键字移除能力（如谢幕曲）。")]
        public AbilityKeyword AbilityKeyword { get; set; } = AbilityKeyword.None;

        public DurationType DurationType { get; set; } = DurationType.Permanent;

        public int DurationParameter { get; set; } = 0;

        public bool CanStack { get; set; } = true;
    }
}
