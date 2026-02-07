using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    [EffectMetadata("改写事件参数", "在 Before 截取阶段改写触发事件的参数。", AbilityEditorCategories.SpecialId)]
    public class RewriteEventParamsEffectParams : BaseEffectParams
    {
        [ParameterMetadata("参数名", UiControlType.TextInput, Description = "要改写的事件参数名称（不区分大小写）。")]
        public string ParamName { get; set; } = string.Empty;

        [ParameterMetadata("新值", UiControlType.TextInput, Description = "参数的新值（支持 DSL 表达式）。")]
        public object? Value { get; set; }
    }
}
