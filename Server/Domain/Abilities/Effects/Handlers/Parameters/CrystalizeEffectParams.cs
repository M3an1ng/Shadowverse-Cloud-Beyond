using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("结晶", "声明结晶对应的护符卡，费用填写在能力关键字中；通过出牌事务使用，不重复支付费用。", AbilityEditorCategories.SpecialId)]
    public class CrystalizeEffectParams : BaseEffectParams
    {
        [ParameterMetadata("对应卡牌", UiControlType.CardDefinitionPicker, AllowDsl = false, Description = "选择对应的护符卡。")]
        public int CardDefinitionId { get; set; }
    }
}
