using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("融合", "从己方手牌选择素材并结算融合；素材默认移出游戏。")]
    public class FusionEffectParams : BaseEffectParams
    {
        [ParameterMetadata("素材去向", UiControlType.Dropdown, DefaultValue = CardZone.Removed, Description = "融合素材的去向。默认移出游戏。")]
        public CardZone DestinationZone { get; set; } = CardZone.Removed;

        [ParameterMetadata("融合原因", UiControlType.TextInput, DefaultValue = "Fusion", Description = "融合素材被移出的原因描述，用于日志记录或特殊逻辑判断。")]
        public string FusionReason { get; set; } = "Fusion";
    }
}

