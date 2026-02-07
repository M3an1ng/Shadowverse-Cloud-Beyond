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
    /// Sum函数实现 - 计算总和
    /// 支持用法：
    /// - sum(collection) - 返回数值集合中所有数值的总和
    /// </summary>
    public class SumFunction : BaseDslFunction
    {
        public override string Name => "sum";

        public override string Description => "计算数值集合中所有数值的总和。";

        public override IReadOnlyList<DslParameterInfo> Parameters => new List<DslParameterInfo>
        {
            new DslParameterInfo("collection", typeof(IEnumerable<double>), "要求和的数值集合。")
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
                return 0;
            }

            var collection = args[0];
            if (collection == null)
            {
                return 0;
            }

            if (!IsCollection(collection))
            {
                return 0;
            }

            return GetSumFromCollection(ConvertToEnumerable(collection));
        }

        /// <summary>
        /// 检查对象是否为数值类型
        /// </summary>
        private static bool IsNumeric(object? value)
        {
            return value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
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

                if (!IsNumeric(item))
                {
                    continue;
                }

                sum += ConvertToDouble(item);
            }

            return sum;
        }
    }
}
