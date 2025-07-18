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
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// Count函数实现 - 计算集合中元素的数量
    /// 支持用法：
    /// - count(collection) - 返回集合中元素的总数
    /// - count(collection, property) - 返回集合中指定属性非空的元素数量
    /// - count(collection, condition) - 返回满足条件的元素数量（未来扩展）
    /// </summary>
    public class CountFunction : BaseDslFunction
    {
        public override string Name => "count";

        protected override int GetMinimumArgumentCount() => 1;
        protected override int? GetMaximumArgumentCount() => 2;

        protected override bool ValidateArgumentTypes(List<object?> args)
        {
            if (args.Count == 0)
            {
                return false;
            }

            // 第一个参数应该是集合或可枚举对象
            var firstArg = args[0];
            if (firstArg == null)
            {
                return true; // null集合返回0
            }

            // 检查是否为集合类型（排除字符串）
            if (!IsCollection(firstArg))
            {
                return false;
            }

            // 如果有第二个参数，应该是字符串（属性名）
            if (args.Count == 2 && args[1] != null && !(args[1] is string))
            {
                return false;
            }

            return true;
        }

        public override async Task<object?> ExecuteAsync(List<object?> args, GameContext context)
        {
            await Task.CompletedTask; // 标记为异步但实际上是同步操作

            if (args.Count == 0)
            {
                return 0;
            }

            var collection = args[0];
            if (collection == null)
            {
                return 0;
            }

            var enumerable = ConvertToEnumerable(collection);

            // 如果只有一个参数，返回集合总数
            if (args.Count == 1)
            {
                return enumerable.Count();
            }

            // 如果有第二个参数，按属性过滤计数
            var propertyName = ConvertToString(args[1]);
            if (string.IsNullOrEmpty(propertyName))
            {
                return enumerable.Count();
            }

            return CountByProperty(enumerable, propertyName);
        }

        /// <summary>
        /// 根据属性计数
        /// </summary>
        private static int CountByProperty(IEnumerable<object?> collection, string propertyName)
        {
            var count = 0;

            foreach (var item in collection)
            {
                if (item == null)
                {
                    continue;
                }

                var propertyValue = GetPropertyValue(item, propertyName);

                // 计算非空属性值的数量
                if (propertyValue != null)
                {
                    // 对于布尔属性，只计算true值
                    if (propertyValue is bool boolValue)
                    {
                        if (boolValue)
                        {
                            count++;
                        }
                    }
                    // 对于数值属性，只计算非零值
                    else if (propertyValue is int intValue)
                    {
                        if (intValue != 0)
                        {
                            count++;
                        }
                    }
                    else if (propertyValue is double doubleValue)
                    {
                        if (doubleValue != 0.0)
                        {
                            count++;
                        }
                    }
                    // 对于字符串属性，只计算非空字符串
                    else if (propertyValue is string stringValue)
                    {
                        if (!string.IsNullOrEmpty(stringValue))
                        {
                            count++;
                        }
                    }
                    // 对于其他类型，只要不为null就计算
                    else
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
