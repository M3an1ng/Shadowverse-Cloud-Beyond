using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters // Corrected namespace
{
    /// <summary>
    /// Parameters for the ModifyPPMax effect (e.g., GainPPOrb).
    /// </summary>
[EffectMetadata("修改PP上限", "修改玩家的最大PP值（如获得PP水晶）。忽略卡牌目标，按玩家范围结算。")]
public class ModifyPPMaxEffectParams : BaseEffectParams // Corrected base class
{
    [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择受到影响的玩家（自己/对手/双方）。")]
    public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        /// <summary>
        /// The amount to modify the player's maximum Play Points by (usually positive, e.g., 1 for GainPPOrb).
        /// </summary>
        [ParameterMetadata("修改量", UiControlType.NumberStepper, DefaultValue = 1, Min = 1, Description = "要修改的最大PP数量。通常为正数，如获得PP水晶时为1。")]
        public int ModifyAmount { get; set; } = 1; // Default to 1 for common GainPPOrb case
    }
}
