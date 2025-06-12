namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 攻击效果参数
    /// 包含执行攻击操作所需的所有参数
    /// </summary>
    public class AttackEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 攻击者的卡牌实例ID
        /// </summary>
        public Guid AttackerInstanceId { get; set; } = Guid.Empty;

        /// <summary>
        /// 攻击目标的ID
        /// 可以是敌方单位的实例ID或敌方玩家的ID
        /// </summary>
        public Guid TargetId { get; set; } = Guid.Empty;

        /// <summary>
        /// 目标类型：单位或玩家
        /// </summary>
        public AttackTargetType TargetType { get; set; } = AttackTargetType.Unit;

        /// <summary>
        /// 是否强制执行攻击（跳过某些验证）
        /// </summary>
        public bool ForceAttack { get; set; } = false;

        /// <summary>
        /// 攻击原因描述
        /// </summary>
        public string AttackReason { get; set; } = "PlayerAction";
    }

    /// <summary>
    /// 攻击目标类型枚举
    /// </summary>
    public enum AttackTargetType
    {
        /// <summary>
        /// 攻击敌方单位
        /// </summary>
        Unit,

        /// <summary>
        /// 攻击敌方玩家（领袖）
        /// </summary>
        Player
    }
}
