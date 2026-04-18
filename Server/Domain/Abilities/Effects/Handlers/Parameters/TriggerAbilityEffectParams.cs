using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Metadata;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("触发能力", "按关键字、能力ID或触发事件类型匹配并触发目标卡当前拥有的运行时能力。", AbilityEditorCategories.SpecialId)]
    public class TriggerAbilityEffectParams : BaseEffectParams
    {
        [ParameterMetadata("触发模式", UiControlType.Dropdown, DefaultValue = TriggerAbilityMode.ByKeyword, Description = "按关键字、能力定义ID或触发事件类型匹配目标能力。", EnumName = nameof(TriggerAbilityMode))]
        public TriggerAbilityMode TriggerMode { get; set; } = TriggerAbilityMode.ByKeyword;

        [ParameterMetadata("能力关键字", UiControlType.Dropdown, DefaultValue = AbilityKeyword.None, Description = "按关键字触发时使用，例如入场曲或谢幕曲。")]
        public AbilityKeyword AbilityKeyword { get; set; } = AbilityKeyword.None;

        [ParameterMetadata("能力ID列表", UiControlType.AbilityDefinitionPicker, Description = "按能力定义ID触发时使用。命中的运行时能力都会被触发。")]
        public List<long> AbilityDefinitionIds { get; set; } = new();

        [ParameterMetadata("触发事件", UiControlType.Dropdown, DefaultValue = EventType.Unknown, Description = "按触发事件类型匹配时使用，例如 Summon / Destroy。")]
        public EventType TriggerEventType { get; set; } = EventType.Unknown;

        [ParameterMetadata("自动随机选择目标", UiControlType.Checkbox, DefaultValue = false, Description = "当被触发能力需要玩家选目标时，改为自动随机选择合法目标。")]
        public bool AutoRandomTargetSelection { get; set; } = false;
    }

}
