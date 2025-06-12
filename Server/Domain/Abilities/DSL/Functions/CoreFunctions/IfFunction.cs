//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// If函数实现 - 条件选择
    /// 支持用法：
    /// - if(condition, thenValue, elseValue) - 如果条件为真返回thenValue，否则返回elseValue
    /// - if(condition, thenValue) - 如果条件为真返回thenValue，否则返回null
    /// </summary>
    public class IfFunction : BaseDslFunction
    {
        public override string Name => "if";

        protected override int GetMinimumArgumentCount() => 2;
        protected override int? GetMaximumArgumentCount() => 3;

        public override async Task<object?> ExecuteAsync(List<object?> args, EvaluationContext context)
        {
            await Task.CompletedTask; // 标记为异步但实际上是同步操作

            if (args.Count < 2)
            {
                return null;
            }

            var condition = args[0];
            var thenValue = args[1];
            var elseValue = args.Count > 2 ? args[2] : null;

            var conditionResult = ConvertToBool(condition);

            return conditionResult ? thenValue : elseValue;
        }
    }
}
