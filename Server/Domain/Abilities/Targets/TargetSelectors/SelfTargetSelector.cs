using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors
{
    /// <summary>
    /// 自身目标选择器
    /// 选择能力来源卡牌自身作为目标
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public class SelfTargetSelector : ITargetSelector
    {
        private readonly ILogger<SelfTargetSelector>? _logger;

        /// <summary>
        /// 初始化自身目标选择器
        /// </summary>
        /// <param name="logger">可选的日志记录器</param>
        public SelfTargetSelector(ILogger<SelfTargetSelector>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 选择目标（返回源卡牌自身）
        /// </summary>
        /// <param name="config">目标配置</param>
        /// <param name="context">游戏上下文</param>
        /// <returns>包含源卡牌的目标列表</returns>
        public Task<List<TargetId>> SelectTargetsAsync(TargetConfig config, GameContext context)
        {
            try
            {
                // 获取源卡牌的实例ID
                var sourceInstanceId = context.SourceCard?.InstanceId ??
                                       (context.CurrentItem as CardInstance)?.InstanceId ??
                                       Guid.Empty;

                if (sourceInstanceId == Guid.Empty)
                {
                    _logger?.LogWarning("[SelfTargetSelector] No source card found in context, returning empty target");
                    return Task.FromResult(new List<TargetId>());
                }

                var result = new List<TargetId> { TargetId.Card(sourceInstanceId) };

                _logger?.LogTrace("[SelfTargetSelector] Selected self target: {SourceCardId}", sourceInstanceId);

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[SelfTargetSelector] Error selecting self target");

                // 根据配置决定是否抛出异常
                if (config?.OnFailure == FailureHandling.AbortAbility)
                {
                    throw;
                }

                // 返回空列表作为降级处理
                return Task.FromResult(new List<TargetId>());
            }
        }
    }
}
