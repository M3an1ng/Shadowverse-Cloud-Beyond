// using ShadowverseBeyond.Server.Domain.Abilities.Effects.Parameters; // No longer needed if base class is in the same new namespace or accessible
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace to match file location
{
    /// <summary>
    /// Parameters for the AddCardToHand effect.
    /// </summary>
    // Inherit from BaseEffectParams which contains common properties like GenerateBattleLog
    [EffectMetadata("添加卡牌到手牌", "将指定的卡牌添加到玩家手牌中。")]
    public class AddCardToHandEffectParams : BaseEffectParams // Changed base class to BaseEffectParams
    {
        /// <summary>
        /// The ID of the card definition to add to the hand.
        /// </summary>
        [ParameterMetadata("卡牌ID", UiControlType.NumberStepper, DefaultValue = 0, Min = 1, Description = "要添加到手牌的卡牌定义ID。")]
        public int CardDefinitionId { get; set; }

        /// <summary>
        /// The number of cards to add. Defaults to 1.
        /// </summary>
        [ParameterMetadata("数量", UiControlType.ValueConfigEditor, DefaultValue = 1, Min = 1, Description = "要添加的卡牌数量。可以是固定数值，也可以是基于游戏状态的动态公式。")]
        public int Amount { get; set; } = 1;
    }
}