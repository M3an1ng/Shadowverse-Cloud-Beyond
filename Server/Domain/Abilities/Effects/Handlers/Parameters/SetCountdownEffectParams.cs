using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("修改倒计时", "修改护符倒计时的变化量。使用卡牌目标。", AbilityEditorCategories.TurnFlowId)]
    public class SetCountdownEffectParams : BaseEffectParams
    {
        [ParameterMetadata("变化量", UiControlType.ValueConfigEditor, DefaultValue = -1, Description = "倒计时的变化量。负数表示减少，正数表示增加。")]
        public int ChangeAmount { get; set; }
    }
}
