// File: Server/Domain/Abilities/Effects/Handlers/Parameters/DrawCardEffectParams.cs
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities; // For PlayerState if needed later
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the Draw effect.
    /// </summary>
    [EffectMetadata("抽牌", "从牌库抽取指定数量的卡牌到手牌。检索模式可改为按卡牌列表抽取。")]
    public class DrawCardEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择受到影响的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        [ParameterMetadata("检索模式", UiControlType.Checkbox, DefaultValue = false, Description = "启用后使用卡牌列表作为抽取对象，忽略抽牌数量。")]
        public bool UseSearchMode { get; set; } = false;

        /// <summary>
        /// The target(s) of the draw effect. This should resolve to one or more players.
        /// </summary>

        /// <summary>
        /// The card instances to draw when search mode is enabled.
        /// Accepts a CardInstance list via DSL or static values.
        /// </summary>
        [ParameterMetadata("检索卡牌列表", UiControlType.TextArea, DefaultValue = null, Description = "检索模式下使用。填写返回卡牌实例列表的DSL表达式。")]
        public object? SearchTargets { get; set; }

        /// <summary>
        /// The number of cards to draw.
        /// Can be a static value or resolved from a DSL expression.
        /// Defaults to 1.
        /// </summary>
        [ParameterMetadata("抽牌数量", UiControlType.NumberStepper, DefaultValue = 1, Min = 1, Description = "配置要抽取的卡牌数量。可以是固定数值，也可以是基于游戏状态的动态公式。")]
        public int DrawAmount { get; set; } = 1;

        // Potential future parameters:
        // public string SourceZone { get; set; } = "Deck"; // e.g., Draw from Graveyard
        // public CardFilter DrawFilter { get; set; } // e.g., Draw a Follower
    }
}
