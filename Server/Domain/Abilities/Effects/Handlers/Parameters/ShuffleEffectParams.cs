using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("洗回牌堆", "将手牌目标洗回牌库并洗牌。使用卡牌目标。", AbilityEditorCategories.PositionChangeId, AbilityEditorCategories.PositionChangeLabel)]
    public class ShuffleEffectParams : BaseEffectParams
    {
        [ParameterMetadata("插入方式", UiControlType.Dropdown, DefaultValue = DeckInsertMode.Random, Description = "卡牌置入牌库时的插入方式。默认随机插入。")]
        public DeckInsertMode InsertMode { get; set; } = DeckInsertMode.Random;
    }
}
