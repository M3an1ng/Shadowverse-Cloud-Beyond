using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors
{
    /// <summary>
    /// 自身目标选择器
    /// 选择能力来源卡牌自身作为目标
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public class SelfTargetSelector : ITargetSelector
    {
        // Match interface signature
        public Task<List<TargetId>> SelectTargetsAsync(TargetConfig config, AbilityContext context)
        {
            // Return a list containing only the source card's TargetId
            var result = new List<TargetId> { TargetId.Card(context.SourceCard.InstanceId) };
            return Task.FromResult(result);
        }
    }
}
