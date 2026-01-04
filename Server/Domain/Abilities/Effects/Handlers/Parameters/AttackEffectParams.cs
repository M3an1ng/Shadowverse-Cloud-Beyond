using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 攻击效果参数
    /// 包含执行攻击操作所需的特定行为修饰符。
    /// </summary>
    [EffectMetadata("攻击", "执行攻击操作，可以强制攻击或指定攻击原因。使用卡牌目标作为攻击对象。")]
    public class AttackEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 是否强制执行攻击（跳过某些验证，例如攻击次数限制）。
        /// </summary>
        [ParameterMetadata("强制攻击", UiControlType.Checkbox, DefaultValue = false, Description = "是否强制执行攻击，跳过攻击次数限制等验证。")]
        public bool ForceAttack { get; set; } = false;
    }

    // AttackTargetType 枚举不再需要，因为目标由 SelectedTargets 决定。
}
