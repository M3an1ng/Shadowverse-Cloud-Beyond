using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the AddCardToDeck effect.
    /// </summary>
    [EffectMetadata("添加卡牌到牌库", "将卡牌添加到牌库。定义模式按玩家范围创建新卡；实例模式使用卡牌目标；复制模式复制卡牌目标并按玩家范围归属。")]
    public class AddCardToDeckEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择受到影响的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        [ParameterMetadata("卡牌来源", UiControlType.Dropdown, DefaultValue = CardSourceMode.Definition, Description = "定义模式创建新卡；实例模式使用已选卡牌目标；复制模式复制已选卡牌目标。")]
        public CardSourceMode CardSourceMode { get; set; } = CardSourceMode.Definition;

        /// <summary>
        /// The ID of the card definition to add to the deck.
        /// </summary>
        [ParameterMetadata("卡牌ID", UiControlType.NumberStepper, DefaultValue = 0, Min = 1, Description = "要添加到牌库的卡牌定义ID。")]
        public int CardDefinitionId { get; set; }

        /// <summary>
        /// The number of cards to add. Defaults to 1.
        /// </summary>
        [ParameterMetadata("数量", UiControlType.ValueConfigEditor, DefaultValue = 1, Min = 1, Description = "要添加的卡牌数量。可以是固定数值，也可以是基于游戏状态的动态公式。")]
        public int Amount { get; set; } = 1;
    }
}
