using System.ComponentModel;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
public enum DiscoverMode { [Description("定义")] Definition, [Description("复制")] Copy, [Description("实例")] Instance }
[EffectMetadata("发现", "从范围内随机展示最多3张不同候选，自己选择1张加入手牌。", AbilityEditorCategories.SpecialId)]
public class DiscoverEffectParams : BaseEffectParams {
 [ParameterMetadata("发现模式",UiControlType.Dropdown)] public DiscoverMode Mode {get;set;}
 [ParameterMetadata("卡片数据ID列表",UiControlType.TextArea, DslMode=DslMode.Forced, Description="定义模式：返回卡片数据ID列表的DSL。")]
 public List<int> CardDefinitionIds {get;set;} = new();
 [ParameterMetadata("卡片实例列表",UiControlType.TextArea, DslMode=DslMode.Forced, Description="复制/实例模式：返回卡片实例列表的DSL。复制保留附加效果；实例仅发现牌堆中的原卡，移入自己手牌且不算抽牌，敌方卡片同时改为自己拥有。")]
 public object? CopySourceTargets {get;set;}
}
