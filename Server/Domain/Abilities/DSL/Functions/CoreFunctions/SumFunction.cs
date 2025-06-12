//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// Sum函数实现 - 计算总和
    /// 支持用法：
    /// - sum(value1, value2, ...) - 返回多个值的总和
    /// - sum(collection) - 返回集合中所有数值的总和
    /// - sum(collection, property) - 返回集合中指定属性值的总和
    /// </summary>
    public class SumFunction : BaseDslFunction
    {
        public override string Name => "sum";

        protected override int GetMinimumArgumentCount() => 1;

        protected override bool ValidateArgumentTypes(List<object?> args)
        {
            if (args.Count == 0)
            {
                return false;
            }

            // 如果只有一个参数，检查是否为集合或数值
            if (args.Count == 1)
            {
                var arg = args[0];
                return arg == null || IsCollection(arg) || IsNumeric(arg);
            }

            // 如果有两个参数，第一个应该是集合，第二个应该是属性名
            if (args.Count == 2)
            {
                var firstArg = args[0];
                var secondArg = args[1];
                
                return (firstArg == null || IsCollection(firstArg)) && 
                       (secondArg == null || secondArg is string);
            }

            // 如果有多个参数，都应该是数值类型
            return args.All(arg => arg == null || IsNumeric(arg));
        }

        public override async Task<object?> ExecuteAsync(List<object?> args, EvaluationContext context)
        {
            await Task.CompletedTask; // 标记为异步但实际上是同步操作

            if (args.Count == 0)
            {
                return 0;
            }

            // 如果只有一个参数
            if (args.Count == 1)
            {
                var arg = args[0];
                if (arg == null)
                {
                    return 0;
                }

                // 如果是集合，返回集合中所有值的总和
                if (IsCollection(arg))
                {
                    return GetSumFromCollection(ConvertToEnumerable(arg));
                }

                // 如果是单个数值，直接返回
                return ConvertToDouble(arg);
            }

            // 如果有两个参数，第一个是集合，第二个是属性名
            if (args.Count == 2)
            {
                var collection = args[0];
                var propertyName = ConvertToString(args[1]);

                if (collection == null || !IsCollection(collection))
                {
                    return 0;
                }

                if (string.IsNullOrEmpty(propertyName))
                {
                    return GetSumFromCollection(ConvertToEnumerable(collection));
                }

                return GetSumFromCollectionByProperty(ConvertToEnumerable(collection), propertyName);
            }

            // 如果有多个参数，返回所有参数的总和
            return GetSumFromValues(args);
        }

        /// <summary>
        /// 检查对象是否为数值类型
        /// </summary>
        private static bool IsNumeric(object? value)
        {
            return value is int or double or float or decimal;
        }

        /// <summary>
        /// 从集合中计算总和
        /// </summary>
        private static double GetSumFromCollection(IEnumerable<object?> collection)
        {
            double sum = 0;

            foreach (var item in collection)
            {
                if (item == null)
                {
                    continue;
                }

                sum += ConvertToDouble(item);
            }

            return sum;
        }

        /// <summary>
        /// 从集合中根据属性计算总和
        /// </summary>
        private static double GetSumFromCollectionByProperty(IEnumerable<object?> collection, string propertyName)
        {
            double sum = 0;

            foreach (var item in collection)
            {
                if (item == null)
                {
                    continue;
                }

                var propertyValue = GetPropertyValue(item, propertyName);
                if (propertyValue != null)
                {
                    sum += ConvertToDouble(propertyValue);
                }
            }

            return sum;
        }

        /// <summary>
        /// 从多个值中计算总和
        /// </summary>
        private static double GetSumFromValues(List<object?> values)
        {
            double sum = 0;

            foreach (var value in values)
            {
                if (value != null)
                {
                    sum += ConvertToDouble(value);
                }
            }

            return sum;
        }
    }
}
