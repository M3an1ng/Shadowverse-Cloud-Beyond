using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 攻击效果参数
    /// 包含执行攻击操作所需的特定行为修饰符。
    /// </summary>
    [EffectMetadata("攻击", "执行攻击操作，可以强制攻击或指定攻击原因。")]
    public class AttackEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 是否强制执行攻击（跳过某些验证，例如攻击次数限制）。
        /// </summary>
        [ParameterMetadata("强制攻击", UiControlType.Checkbox, DefaultValue = false, Description = "是否强制执行攻击，跳过攻击次数限制等验证。")]
        public bool ForceAttack { get; set; } = false;

        /// <summary>
        /// 攻击原因描述，用于日志或特殊逻辑。
        /// </summary>
        [ParameterMetadata("攻击原因", UiControlType.TextInput, DefaultValue = "PlayerAction", Description = "攻击的原因描述，用于日志记录和特殊逻辑判断。")]
        public string AttackReason { get; set; } = "PlayerAction";
    }

    // AttackTargetType 枚举不再需要，因为目标由 SelectedTargets 决定。
}
