using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("序列", "按依序或逆序选取若干分支并触发对应能力。", AbilityEditorCategories.SpecialId)]
    public class SequenceEffectParams : BaseEffectParams
    {
        [ParameterMetadata("分支能力列表", UiControlType.AbilityListEditor, Description = "依序分支的能力配置列表。")]
        public List<AbilityConfig> AbilityConfigs { get; set; } = new();

        [ParameterMetadata("选择数量", UiControlType.NumberStepper, DefaultValue = 1, Min = 1, Description = "每次执行选择的分支数量。")]
        public int SelectionCount { get; set; } = 1;

        [ParameterMetadata("可循环", UiControlType.Checkbox, DefaultValue = false, Description = "开启后会在末尾回绕继续选取。")]
        public bool LoopSelection { get; set; } = false;

        [ParameterMetadata("执行顺序", UiControlType.Dropdown, DefaultValue = SequenceOrder.Forward, Description = "可选依序或逆序。")]
        public SequenceOrder SelectionOrder { get; set; } = SequenceOrder.Forward;
    }
}
