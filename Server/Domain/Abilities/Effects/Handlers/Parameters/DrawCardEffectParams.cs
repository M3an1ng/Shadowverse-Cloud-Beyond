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
    [EffectMetadata("抽牌", "从牌库抽取指定数量的卡牌到手牌。忽略卡牌目标，按玩家范围结算。")]
    public class DrawCardEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择受到影响的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        /// <summary>
        /// The target(s) of the draw effect. This should resolve to one or more players.
        /// </summary>

        /// <summary>
        /// The number of cards to draw.
        /// Can be a static value or resolved from a DSL expression.
        /// Defaults to 1.
        /// </summary>
        [ParameterMetadata("抽牌数量", UiControlType.ValueConfigEditor, DefaultValue = 1, Min = 1, Description = "配置要抽取的卡牌数量。可以是固定数值，也可以是基于游戏状态的动态公式。")]
        public int DrawAmount { get; set; } = 1;

        // Potential future parameters:
        // public string SourceZone { get; set; } = "Deck"; // e.g., Draw from Graveyard
        // public CardFilter DrawFilter { get; set; } // e.g., Draw a Follower
    }
}
