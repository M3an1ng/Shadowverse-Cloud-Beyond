using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext

namespace ShadowverseBeyond.Server.Domain.Abilities.Conditions
{
    /// <summary>
    /// DSL条件评估器实现
    /// 封装DSL表达式解析和评估的通用逻辑
    /// </summary>
    // Implement the general IConditionEvaluator interface as well
    public class DslConditionEvaluator : IConditionEvaluator
    {
        private readonly IDslCompiler _dslCompiler;
        private readonly ILogger<DslConditionEvaluator> _logger;

        public DslConditionEvaluator(
            IDslCompiler dslCompiler,
            ILogger<DslConditionEvaluator> logger)
        {
            _dslCompiler = dslCompiler ?? throw new ArgumentNullException(nameof(dslCompiler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        /// <summary>
        /// Implements IConditionEvaluator.Evaluate.
        /// Uses the DslCompiler to get a cached, high-performance delegate for the condition.
        /// </summary>
        public bool Evaluate(ConditionConfig config, GameContext context)
        {
            if (string.IsNullOrWhiteSpace(config.Expression))
            {
                _logger.LogDebug("ConditionConfig has empty expression, returning true");
                return true; // Default behavior for empty conditions
            }
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                // Compile the expression to get a cached, high-performance delegate.
                // The 'item' context is null because conditions are generally context-wide, not item-specific.
                var predicate = _dslCompiler.CompilePredicate<object>(config.Expression);

                // Execute the compiled delegate
                var result = predicate(null!, context);
                _logger.LogDebug("Compiled DSL condition '{Expression}' evaluated to {Result}", config.Expression, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compiling or evaluating DSL condition: {Expression}", config.Expression);
                return false;
            }
        }
    }
}
