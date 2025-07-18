using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    /// <summary>
    /// 换牌效果参数
    /// 包含执行换牌操作所需的所有参数
    /// 目标玩家通过 GameContext.SelectedTargets 传递，遵循统一的 EffectHandler 架构模式
    /// </summary>
    public class MulliganEffectParams : BaseEffectParams
    {
        /// <summary>
        /// 要替换的卡牌UID列表
        /// </summary>
        public List<Guid> CardUidsToReplace { get; set; } = new List<Guid>();

        /// <summary>
        /// 是否强制完成换牌（跳过某些验证）
        /// </summary>
        public bool ForceMulligan { get; set; } = false;

        /// <summary>
        /// 换牌原因描述
        /// </summary>
        public string MulliganReason { get; set; } = "PlayerChoice";
    }
}
