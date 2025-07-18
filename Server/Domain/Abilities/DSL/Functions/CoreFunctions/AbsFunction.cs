//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// Abs函数实现 - 计算绝对值
    /// 支持用法：
    /// - abs(value) - 返回数值的绝对值
    /// </summary>
    public class AbsFunction : BaseDslFunction
    {
        public override string Name => "abs";

        protected override int? GetExpectedArgumentCount() => 1;

        protected override bool ValidateArgumentTypes(List<object?> args)
        {
            if (args.Count != 1)
            {
                return false;
            }

            var arg = args[0];
            return arg == null || IsNumeric(arg);
        }

        public override async Task<object?> ExecuteAsync(List<object?> args, GameContext context)
        {
            await Task.CompletedTask; // 标记为异步但实际上是同步操作

            if (args.Count != 1)
            {
                return null;
            }

            var value = args[0];
            if (value == null)
            {
                return 0;
            }

            var numericValue = ConvertToDouble(value);
            return Math.Abs(numericValue);
        }

        /// <summary>
        /// 检查对象是否为数值类型
        /// </summary>
        private static bool IsNumeric(object? value)
        {
            return value is int or double or float or decimal;
        }
    }
}
