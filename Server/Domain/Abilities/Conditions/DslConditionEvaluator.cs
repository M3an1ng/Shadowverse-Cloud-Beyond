using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Processing;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;

namespace ShadowverseBeyond.Server.Domain.Abilities.Conditions
{
    /// <summary>
    /// DSL条件评估器实现
    /// 封装DSL表达式解析和评估的通用逻辑
    /// </summary>
    public class DslConditionEvaluator : IDslConditionEvaluator
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
        /// <param name="context">条件评估上下文</param>
        /// <returns>评估结果</returns>
        public async Task<bool> EvaluateDslExpressionAsync(string expression, ConditionEvaluationContext context)
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

                // 创建DSL评估上下文
                var evaluationContext = CreateEvaluationContext(context);

                // 使用DSL求值器评估表达式
                var result = await _dslEvaluator.EvaluateAsync(expressionAst, evaluationContext);

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
        /// 从ConditionEvaluationContext创建EvaluationContext
        /// </summary>
        /// <param name="context">条件评估上下文</param>
        /// <returns>DSL评估上下文</returns>
        private EvaluationContext CreateEvaluationContext(ConditionEvaluationContext context)
        {
            var requestingPlayerIdStr = context.RequestingPlayerId.ToString();
            var requestingPlayer = context.GameState?.GetPlayerState(requestingPlayerIdStr);
            var opponentPlayer = context.GameState?.GetOpponentPlayerState(requestingPlayerIdStr);

            return new EvaluationContext(
                cardRepository: null!, // TODO: 需要从依赖注入中获取
                gameState: context.GameState?.ToDto(),
                player: requestingPlayer?.ToDto(),
                opponent: opponentPlayer?.ToDto(),
                currentItem: context.SourceCard,
                abilityContext: null // TODO: 可能需要从context中获取
            );
        }
    }
}
