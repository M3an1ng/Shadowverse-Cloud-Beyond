using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;

namespace ShadowverseBeyond.Server.Domain.Abilities.Conditions
{
    /// <summary>
    /// DSL条件评估器接口
    /// 提供DSL表达式评估的抽象，便于测试和依赖注入
    /// </summary>
    public interface IDslConditionEvaluator
    {
        /// <summary>
        /// 异步评估DSL条件表达式
        /// </summary>
        /// <param name="expression">DSL表达式字符串</param>
        /// <param name="context">条件评估上下文</param>
        /// <returns>评估结果</returns>
        Task<bool> EvaluateDslExpressionAsync(string expression, ConditionEvaluationContext context);
    }
}
