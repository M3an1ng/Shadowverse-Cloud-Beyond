using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("超进化", "使目标随从进行超进化，触发超进化流程。", AbilityEditorCategories.BaseActionId)]
    public class SuperEvolveEffectParams : BaseEffectParams
    {
        [ParameterMetadata("是否视为手动进化", UiControlType.Checkbox, DefaultValue = false, Description = "开启后生成的进化/超进化事件会标记为手动进化。默认关闭，用于兼容已有卡牌效果。")]
        public bool IsManualEvolution { get; set; } = false;
    }
}
