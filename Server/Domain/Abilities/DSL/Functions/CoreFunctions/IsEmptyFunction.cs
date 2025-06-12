//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// IsEmpty函数实现 - 检查集合或字符串是否为空
    /// 支持用法：
    /// - isEmpty(collection) - 检查集合是否为空
    /// - isEmpty(string) - 检查字符串是否为空或null
    /// </summary>
    public class IsEmptyFunction : BaseDslFunction
    {
        public override string Name => "isEmpty";

        protected override int? GetExpectedArgumentCount() => 1;

        protected override bool ValidateArgumentTypes(List<object?> args)
        {
            if (args.Count != 1)
            {
                return false;
            }

            var arg = args[0];
            // 参数可以是null、集合或字符串
            return arg == null || IsCollection(arg) || arg is string;
        }

        public override async Task<object?> ExecuteAsync(List<object?> args, EvaluationContext context)
        {
            await Task.CompletedTask; // 标记为异步但实际上是同步操作

            if (args.Count != 1)
            {
                return true; // 无效参数视为空
            }

            var value = args[0];

            // null值视为空
            if (value == null)
            {
                return true;
            }

            // 字符串检查
            if (value is string stringValue)
            {
                return string.IsNullOrEmpty(stringValue);
            }

            // 集合检查
            if (IsCollection(value))
            {
                var enumerable = ConvertToEnumerable(value);
                return !enumerable.Any();
            }

            // 其他类型视为非空
            return false;
        }
    }
}
