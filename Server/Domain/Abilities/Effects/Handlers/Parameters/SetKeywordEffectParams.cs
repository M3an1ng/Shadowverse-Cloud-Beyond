using System.ComponentModel;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    public enum KeywordOperation
    {
        [Description("添加")]
        Add = 0,
        [Description("移除")]
        Remove = 1
    }

    /// <summary>
    /// Parameters for the SetKeyword effect.
    /// </summary>
    [EffectMetadata("设置关键字", "为目标添加或移除指定的关键字能力。使用卡牌目标。")]
    public class SetKeywordEffectParams : BaseEffectParams
    {
        [ParameterMetadata("操作", UiControlType.Dropdown, DefaultValue = KeywordOperation.Add, Description = "添加或移除关键字。")]
        public KeywordOperation Operation { get; set; } = KeywordOperation.Add;

        [ParameterMetadata("关键字", UiControlType.Dropdown, DefaultValue = CardKeyword.None, Description = "要添加/移除的关键字能力。")]
        public CardKeyword Keyword { get; set; } = CardKeyword.None;

        [ParameterMetadata("持续时间", UiControlType.Dropdown, DefaultValue = DurationType.Permanent, Description = "关键字持续时间（仅对添加/移除关键字的Buff生效）。")]
        public DurationType DurationType { get; set; } = DurationType.Permanent;

        [ParameterMetadata("持续参数", UiControlType.NumberStepper, DefaultValue = 0, Min = 0, Description = "持续时间参数（回合开始/结束时生效）。0表示默认一次。")]
        public int DurationParameter { get; set; } = 0;

        public bool CanStack { get; set; } = true;
    }
}

