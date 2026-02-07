using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("抉择", "让玩家从多个分支中选择若干个结果。", AbilityEditorCategories.SpecialId)]
    public class ChooseEffectParams : BaseEffectParams
    {
        [ParameterMetadata("分支能力列表", UiControlType.AbilityListEditor, Description = "抉择的分支能力配置列表。")]
        public List<AbilityConfig> AbilityConfigs { get; set; } = new();

        [ParameterMetadata("选择数量", UiControlType.NumberStepper, DefaultValue = 1, Min = 1, Description = "固定选择的分支数量，超出可选数量时自动全选。")]
        public int SelectionCount { get; set; } = 1;
    }
}
