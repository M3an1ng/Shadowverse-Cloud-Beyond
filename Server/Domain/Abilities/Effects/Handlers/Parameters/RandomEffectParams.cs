using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("随机", "按权重随机选择若干分支并触发对应连锁能力。", AbilityEditorCategories.SpecialId)]
    public class RandomEffectParams : BaseEffectParams
    {
        [ParameterMetadata("分支能力列表", UiControlType.AbilityListEditor, Description = "随机分支的能力配置列表。")]
        public List<AbilityConfig> AbilityConfigs { get; set; } = new();

        [ParameterMetadata("选择数量", UiControlType.NumberStepper, DefaultValue = 1, Min = 1, Description = "固定选择的分支数量，超出可选数量时自动全选（若允许重复则按配置数量抽取）。")]
        public int SelectionCount { get; set; } = 1;

        [ParameterMetadata("权重列表", UiControlType.TextInput, DefaultValue = "", Description = "分支权重列表，按分支顺序填写，使用逗号分隔。留空表示所有分支等权重。")]
        public string Weights { get; set; } = string.Empty;

        [ParameterMetadata("允许重复", UiControlType.Checkbox, DefaultValue = false, Description = "允许同一分支被重复选中。")]
        public bool AllowRepeat { get; set; } = false;
    }
}
