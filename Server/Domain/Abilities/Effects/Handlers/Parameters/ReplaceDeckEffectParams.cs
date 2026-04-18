using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("替换牌堆", "按顺序使用卡牌定义ID列表重建目标玩家的整副牌堆；旧牌会被移出游戏，但不属于消失。")]
    public class ReplaceDeckEffectParams : BaseEffectParams
    {
        [ParameterMetadata("玩家范围", UiControlType.Dropdown, DefaultValue = PlayerScope.Self, Description = "选择要替换牌堆的玩家（自己/对手/双方）。")]
        public PlayerScope PlayerScope { get; set; } = PlayerScope.Self;

        [ParameterMetadata("卡牌ID", UiControlType.CardDefinitionPicker, DefaultValue = null, Description = "按输入顺序重建牌堆的卡牌定义ID列表。")]
        public List<int> CardDefinitionIds { get; set; } = new();
    }
}
