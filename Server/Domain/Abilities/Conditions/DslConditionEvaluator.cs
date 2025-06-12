using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Processing;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext

namespace ShadowverseBeyond.Server.Domain.Abilities.Conditions
{
    /// <summary>
    /// DSL条件评估器实现
    /// 封装DSL表达式解析和评估的通用逻辑
    /// </summary>
    // Implement the general IConditionEvaluator interface as well
    public class DslConditionEvaluator : IDslConditionEvaluator, IConditionEvaluator
    {
        private readonly DslEvaluator _dslEvaluator;
        private readonly DslExpressionParser _dslParser;
        private readonly ILogger<DslConditionEvaluator> _logger;
        public DslConditionEvaluator(
            DslEvaluator dslEvaluator,
            DslExpressionParser dslParser,
            ILogger<DslConditionEvaluator> logger)
        {
            _dslEvaluator = dslEvaluator ?? throw new ArgumentNullException(nameof(dslEvaluator));
            _dslParser = dslParser ?? throw new ArgumentNullException(nameof(dslParser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 异步评估DSL条件表达式
        /// </summary>
        /// <param name="expression">DSL表达式字符串</param>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>评估结果</returns>
        public async Task<bool> EvaluateDslExpressionAsync(string expression, GameContext context)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                _logger.LogDebug("Empty expression provided, returning true");
                return true;
            }

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                // 解析表达式为AST
                var expressionAst = _dslParser.ParseExpression(expression);
                if (expressionAst == null)
                {
                    _logger.LogWarning("Failed to parse DSL expression: {Expression}", expression);
                    return false;
                }

                // 直接使用DslEvaluator的GameContext重载方法
                var result = await _dslEvaluator.EvaluateAsync(expressionAst, context);

                // 处理评估结果
                if (result.IsSuccess)
                {
                    var boolResult = TypeConverter.ConvertToBool(result.Value);
                    _logger.LogDebug("DSL expression '{Expression}' evaluated to {Result} (original value: {Value})",
                        expression, boolResult, result.Value);
                    return boolResult;
                }
                else
                {
                    _logger.LogWarning("DSL expression '{Expression}' evaluation failed: {Errors}",
                        expression, string.Join(", ", result.Errors));
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating DSL expression: {Expression}", expression);
                return false;
            }
        }



        /// <summary>
        /// Implements IConditionEvaluator.EvaluateAsync.
        /// 直接使用统一的GameContext构建EvaluationContext用于DslEvaluator。
        /// </summary>
        public async Task<bool> EvaluateAsync(ConditionConfig config, GameContext context)
        {
            if (string.IsNullOrWhiteSpace(config.Expression))
            {
                _logger.LogDebug("ConditionConfig has empty expression, returning true");
                return true; // Or false, depending on desired default behavior
            }
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                // Parse the expression from ConditionConfig
                var expressionAst = _dslParser.ParseExpression(config.Expression);
                if (expressionAst == null)
                {
                    _logger.LogWarning("Failed to parse DSL expression from ConditionConfig: {Expression}", config.Expression);
                    return false;
                }

                // 直接使用DslEvaluator的GameContext重载方法
                var result = await _dslEvaluator.EvaluateAsync(expressionAst, context);

                // Process result
                if (result.IsSuccess)
                {
                    var boolResult = TypeConverter.ConvertToBool(result.Value);
                    _logger.LogDebug("DSL condition '{Expression}' evaluated to {Result} (original value: {Value})",
                        config.Expression, boolResult, result.Value);
                    return boolResult;
                }
                else
                {
                    _logger.LogWarning("DSL condition '{Expression}' evaluation failed: {Errors}",
                        config.Expression, string.Join(", ", result.Errors));
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating DSL condition: {Expression}", config.Expression);
                return false;
            }
        }
    }
}
