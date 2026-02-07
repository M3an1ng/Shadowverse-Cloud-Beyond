using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 返回效果参数
    /// 将目标卡牌返回到指定区域（通常是手牌或牌库）
    /// </summary>
    [EffectMetadata("返回", "将目标卡牌返回手牌。使用卡牌目标。")]
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
    }
}
