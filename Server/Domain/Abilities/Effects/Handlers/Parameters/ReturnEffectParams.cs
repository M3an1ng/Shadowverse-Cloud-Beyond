using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 返回效果参数
    /// 将目标卡牌返回到指定区域（通常是手牌或牌库）
    /// </summary>
    public class ReturnEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 返回目标区域
        /// </summary>
        public CardZone TargetZone { get; set; } = CardZone.Hand;

        /// <summary>
        /// 返回原因描述
        /// </summary>
        public string ReturnReason { get; set; } = "Effect";

        /// <summary>
        /// 是否触发返回相关效果
        /// </summary>
        public bool TriggerReturnEffects { get; set; } = true;
    }
}
