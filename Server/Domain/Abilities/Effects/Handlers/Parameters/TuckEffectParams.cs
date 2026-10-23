using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("置入牌堆", "将目标卡牌洗回牌库。使用卡牌目标。", AbilityEditorCategories.PositionChangeId, AbilityEditorCategories.PositionChangeLabel)]
    public class TuckEffectParams : BaseEffectParams
    {
        [ParameterMetadata("插入方式", UiControlType.Dropdown, DefaultValue = DeckInsertMode.Random, Description = "卡牌置入牌库时的插入方式。默认随机插入。")]
        public DeckInsertMode InsertMode { get; set; } = DeckInsertMode.Random;
    }
}
