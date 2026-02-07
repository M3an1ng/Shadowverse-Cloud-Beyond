using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the SetAttackTimes effect.
    /// </summary>
    [EffectMetadata("设置攻击次数", "设置目标随从每回合可攻击次数上限。使用卡牌目标。", AbilityEditorCategories.TempStateId)]
    public class SetAttackTimesEffectParams : BaseEffectParams
    {
        [ParameterMetadata("最大攻击次数", UiControlType.NumberStepper, DefaultValue = 1, Min = 0, Description = "设置目标随从每回合可攻击次数上限。0 表示无法攻击。")]
        public int MaxAttacksPerTurn { get; set; } = 1;
    }
}
