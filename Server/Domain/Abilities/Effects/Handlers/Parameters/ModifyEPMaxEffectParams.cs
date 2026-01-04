using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// Parameters for the ModifyEPMax effect.
    /// </summary>
    [EffectMetadata("修改EP上限", "修改玩家最大进化点数。忽略卡牌目标，按玩家范围结算。")]
    public class ModifyEPMaxEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择受到影响的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        /// <summary>
        /// The amount to modify the player's maximum Evolution Points by (can be positive or negative).
        /// </summary>
        [ParameterMetadata("修改量", UiControlType.ValueConfigEditor, DefaultValue = 1, Description = "要修改的最大EP数量。可以为正数（增加）或负数（减少）。")]
        public int ModifyAmount { get; set; }
    }
}
