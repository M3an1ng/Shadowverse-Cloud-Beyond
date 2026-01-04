using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the AddEmblem effect.
    /// </summary>
    [EffectMetadata("添加纹章", "将纹章添加到纹章区。定义模式按玩家范围创建新卡；实例模式使用卡牌目标。")]
    public class AddEmblemEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择受到影响的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        [ParameterMetadata("卡牌来源", UiControlType.Dropdown, DefaultValue = CardSourceMode.Definition, Description = "定义模式创建新卡；实例模式使用已选卡牌目标。")]
        public CardSourceMode CardSourceMode { get; set; } = CardSourceMode.Definition;

        /// <summary>
        /// The ID of the card definition to add to the emblem zone.
        /// </summary>
        [ParameterMetadata("卡牌ID", UiControlType.NumberStepper, DefaultValue = 0, Min = 1, Description = "要添加到纹章区的卡牌定义ID。")]
        public int CardDefinitionId { get; set; }

        /// <summary>
        /// The number of cards to add. Defaults to 1.
        /// </summary>
        [ParameterMetadata("数量", UiControlType.ValueConfigEditor, DefaultValue = 1, Min = 1, Description = "要添加的卡牌数量。可以是固定数值，也可以是基于游戏状态的动态公式。")]
        public int Amount { get; set; } = 1;

        [ParameterMetadata("同名检测", UiControlType.Checkbox, DefaultValue = false, Description = "启用时若纹章区已有相同静态ID的纹章则不再添加。")]
        public bool PreventDuplicate { get; set; } = false;
    }
}
