using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("激奏", "声明激奏对应的法术卡，费用填写在能力关键字中；通过出牌事务使用，不重复支付费用。", AbilityEditorCategories.SpecialId)]
    public class AccelerateEffectParams : BaseEffectParams
    {
        [ParameterMetadata("对应卡牌", UiControlType.CardDefinitionPicker, AllowDsl = false, Description = "选择对应的法术卡。")]
        public int CardDefinitionId { get; set; }
    }
}
