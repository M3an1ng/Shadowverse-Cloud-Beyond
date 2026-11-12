using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;

[EffectMetadata("证据数修改", "按玩家范围查找战场上的证据护符，增加、设置或消耗证据数。证据数不足时消耗失败。", AbilityEditorCategories.ResourceValueId)]
public class ModifyEvidenceEffectParams : BaseEffectParams
{
    [ParameterMetadata("操作", UiControlType.Dropdown, DefaultValue = CounterOperation.Add)]
    public CounterOperation Operation { get; set; } = CounterOperation.Add;

    [ParameterMetadata("数值", UiControlType.NumberStepper, DefaultValue = 1, Min = 0, Description = "增加、设置或消耗的证据数，不能为负数。")]
    public int Amount { get; set; } = 1;

    [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "自动查找该范围内玩家战场上的证据护符。")]
    public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;
}

public class SettleEvidenceEffectParams : BaseEffectParams { }
