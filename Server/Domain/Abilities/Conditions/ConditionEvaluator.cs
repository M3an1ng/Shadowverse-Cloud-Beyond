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
        // This class now acts as a wrapper or composite evaluator.
        // The actual DSL evaluation is handled by DslConditionEvaluator, which is injected via the IConditionEvaluator interface.
        private readonly IConditionEvaluator _dslConditionEvaluator;
        private readonly ILogger<ConditionEvaluator> _logger;

        public ConditionEvaluator(IConditionEvaluator dslConditionEvaluator, ILogger<ConditionEvaluator> logger)
        {
            // The injected evaluator is expected to be the DslConditionEvaluator instance configured in DI.
            _dslConditionEvaluator = dslConditionEvaluator ?? throw new ArgumentNullException(nameof(dslConditionEvaluator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 同步评估条件是否满足
        /// </summary>
        /// <param name="config">条件的静态配置</param>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>如果条件满足，则返回 true；否则返回 false</returns>
        public bool Evaluate(ConditionConfig config, GameContext context)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                if (!config.IsEnabled)
                {
                    _logger.LogDebug("Condition {ConditionId} is disabled, returning false", config.Id);
                    return false;
                }

                // Delegate the evaluation to the injected evaluator (which should be the DslConditionEvaluator).
                // This class no longer needs to know about the specifics of DSL.
                var result = _dslConditionEvaluator.Evaluate(config, context);

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
