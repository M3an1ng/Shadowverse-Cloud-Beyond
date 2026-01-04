using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace
{
    /// <summary>
    /// Parameters for the ModifyPP effect.
    /// </summary>
[EffectMetadata("修改PP", "修改玩家当前的PP值。忽略卡牌目标，按玩家范围结算。")]
public class ModifyPPEffectParams : BaseEffectParams // Corrected base class
{
    [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择受到影响的玩家（自己/对手/双方）。")]
    public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        /// <summary>
        /// The amount to modify the player's current Play Points by (can be positive or negative).
        /// </summary>
        [ParameterMetadata("修改量", UiControlType.ValueConfigEditor, DefaultValue = 1, Description = "要修改的PP数量。可以为正数（增加）或负数（减少）。")]
        public int ModifyAmount { get; set; }
    }
}
