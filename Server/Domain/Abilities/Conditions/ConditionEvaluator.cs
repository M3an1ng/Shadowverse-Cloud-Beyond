using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext

namespace ShadowverseBeyond.Server.Domain.Abilities.Conditions
{
    /// <summary>
    /// 条件评估器实现
    /// 负责评估条件配置并返回布尔结果
    /// </summary>
    public class ConditionEvaluator : IConditionEvaluator
    {
        private readonly IDslConditionEvaluator _dslConditionEvaluator;
        private readonly ILogger<ConditionEvaluator> _logger;

        public ConditionEvaluator(IDslConditionEvaluator dslConditionEvaluator, ILogger<ConditionEvaluator> logger)
        {
            _dslConditionEvaluator = dslConditionEvaluator ?? throw new ArgumentNullException(nameof(dslConditionEvaluator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 异步评估条件是否满足
        /// </summary>
        /// <param name="config">条件的静态配置</param>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>如果条件满足，则返回 true；否则返回 false</returns>
        public async Task<bool> EvaluateAsync(ConditionConfig config, GameContext context)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                // 检查条件是否启用
                if (!config.IsEnabled)
                {
                    _logger.LogDebug("Condition {ConditionId} is disabled, returning false", config.Id);
                    return false;
                }

                // 直接使用统一的GameContext，不再需要创建单独的条件评估上下文
                // 使用DSL条件评估器评估表达式
                var result = await _dslConditionEvaluator.EvaluateDslExpressionAsync(config.Expression, context);

                _logger.LogDebug("Condition {ConditionId} evaluated to {Result}", config.Id, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating condition {ConditionId} with expression: {Expression}",
                    config.Id, config.Expression);
                return false;
            }
        }

        // CreateConditionEvaluationContext方法已删除，因为现在直接使用统一的GameContext


    }
}
