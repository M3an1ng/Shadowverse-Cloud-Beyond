using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("抉择", "让玩家从多个分支中选择若干个结果。")]
    public class ChooseEffectParams : BaseEffectParams
    {
        [ParameterMetadata("选择数量", UiControlType.ValueConfigEditor, DefaultValue = 1, Min = 1, Description = "固定选择的分支数量，超出可选数量时自动全选。")]
        public int SelectionCount { get; set; } = 1;
    }
}
