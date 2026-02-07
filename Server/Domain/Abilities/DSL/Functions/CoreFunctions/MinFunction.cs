//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System;
using System.Collections;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// Min函数实现 - 获取最小值
    /// 支持用法：
    /// - min(collection) - 返回数值集合中的最小值
    /// </summary>
    public class MinFunction : BaseDslFunction
    {
        public override string Name => "min";
        public override string Description => "找出数值集合中的最小值。";

        public override IReadOnlyList<DslParameterInfo> Parameters => new List<DslParameterInfo>
        {
            new DslParameterInfo("collection", typeof(IEnumerable<double>), "要查找最小值的数值集合。")
        };

        public override Type ReturnType => typeof(double);

        protected override int? GetExpectedArgumentCount() => 1;

        protected override bool ValidateArgumentTypes(List<object?> args)
        {
            if (args.Count != 1)
            {
                return false;
            }

            var collection = args[0];
            if (collection == null)
            {
                return true;
            }

            if (!IsCollection(collection))
            {
                return false;
            }

            var enumerable = ConvertToEnumerable(collection);
            foreach (var item in enumerable)
            {
                if (item == null)
                {
                    continue;
                }

                if (!IsNumeric(item))
                {
                    return false;
                }
            }

            return true;
        }

        public override object? Execute(List<object?> args, GameContext context)
        {
            if (args.Count != 1)
            {
                return null;
            }

            var collection = args[0];
            if (collection == null)
            {
                return null;
            }

            if (!IsCollection(collection))
            {
                return null;
            }

            return GetMinFromCollection(ConvertToEnumerable(collection));
        }

        /// <summary>
        /// 检查对象是否为数值类型
        /// </summary>
        private static bool IsNumeric(object? value)
        {
            return value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
        }

        /// <summary>
        /// 从集合中获取最小值
        /// </summary>
        private static object? GetMinFromCollection(IEnumerable<object?> collection)
        {
            double? minValue = null;

            foreach (var item in collection)
            {
                if (item == null)
                {
                    continue;
                }

                if (!IsNumeric(item))
                {
                    continue;
                }

                var numericValue = ConvertToDouble(item);
                if (!minValue.HasValue || numericValue < minValue.Value)
                {
                    minValue = numericValue;
                }
            }

            return minValue;
        }
    }
}
