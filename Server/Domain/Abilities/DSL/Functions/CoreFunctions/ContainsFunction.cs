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
    /// Contains函数实现 - 检查集合是否包含指定元素
    /// 支持用法：
    /// - contains(collection, value) - 检查集合是否包含指定值
    /// - contains(string, substring) - 检查字符串是否包含子字符串
    /// </summary>
    public class ContainsFunction : BaseDslFunction
    {
        public override string Name => "contains";
        public override Type ReturnType => typeof(bool);
        public override string Description => "检查一个集合或字符串是否包含指定的元素或子字符串。";

        public override IReadOnlyList<DslParameterInfo> Parameters => new List<DslParameterInfo>
        {
            new DslParameterInfo("source", typeof(object), "要进行检查的集合或字符串。"),
            new DslParameterInfo("value", typeof(object), "要查找的元素或子字符串。")
        };

        protected override int? GetExpectedArgumentCount() => 2;

        protected override bool ValidateArgumentTypes(List<object?> args)
        {
            if (args.Count != 2)
            {
                return false;
            }

            var firstArg = args[0];
            // 第一个参数应该是集合或字符串
            return firstArg == null || IsCollection(firstArg) || firstArg is string;
        }

        public override object? Execute(List<object?> args, GameContext context)
        {
            if (args.Count != 2)
            {
                return false;
            }

            var collection = args[0];
            var searchValue = args[1];

            if (collection == null)
            {
                return false;
            }

            // 如果是字符串，检查是否包含子字符串
            if (collection is string stringValue)
            {
                var searchString = ConvertToString(searchValue);
                return stringValue.Contains(searchString, StringComparison.OrdinalIgnoreCase);
            }

            // 如果是集合，检查是否包含指定值
            if (IsCollection(collection))
            {
                var enumerable = ConvertToEnumerable(collection);
                return ContainsValue(enumerable, searchValue, context);
            }

            return false;
        }

        /// <summary>
        /// 检查集合是否包含指定值
        /// </summary>
        private static bool ContainsValue(IEnumerable<object?> collection, object? searchValue, GameContext context)
        {
            if (searchValue is string searchString && TryConvertToStringCollection(collection, out var stringCollection))
            {
                return DslTribeOperations.Contains(context, stringCollection, searchString);
            }

            foreach (var item in collection)
            {
                if (AreEqual(item, searchValue))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 比较两个值是否相等
        /// </summary>
        private static bool AreEqual(object? value1, object? value2)
        {
            // 处理null值
            if (value1 == null && value2 == null)
            {
                return true;
            }

            if (value1 == null || value2 == null)
            {
                return false;
            }

            // 如果类型相同，直接比较
            if (value1.GetType() == value2.GetType())
            {
                return value1.Equals(value2);
            }

            // 如果都是数值类型，转换后比较
            if (IsNumeric(value1) && IsNumeric(value2))
            {
                var num1 = ConvertToDouble(value1);
                var num2 = ConvertToDouble(value2);
                return Math.Abs(num1 - num2) < double.Epsilon;
            }

            // 如果都可以转换为字符串，比较字符串（不区分大小写）
            var str1 = ConvertToString(value1);
            var str2 = ConvertToString(value2);
            return string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 检查对象是否为数值类型
        /// </summary>
        private static bool IsNumeric(object? value)
        {
            return value is int or double or float or decimal;
        }

        private static bool TryConvertToStringCollection(IEnumerable<object?> collection, out List<string> stringCollection)
        {
            stringCollection = new List<string>();
            foreach (var item in collection)
            {
                if (item is not string text)
                {
                    stringCollection.Clear();
                    return false;
                }

                stringCollection.Add(text);
            }

            return true;
        }
    }
}
