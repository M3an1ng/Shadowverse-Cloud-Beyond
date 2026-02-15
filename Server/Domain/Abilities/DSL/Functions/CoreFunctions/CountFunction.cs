//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System;
using System.Collections;
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
    /// - count("counterName") - 返回当前卡牌上的计数器数值
    /// </summary>
    public class CountFunction : BaseDslFunction
    {
        public override string Name => "count";
        public override string Description => "计算集合中元素的数量。";

        public override IReadOnlyList<DslParameterInfo> Parameters => new List<DslParameterInfo>
        {
            new DslParameterInfo("collectionOrCounter", typeof(object), "要计算数量的目标集合，或计数器名称。")
        };

        public override Type ReturnType => typeof(int);

        protected override int GetMinimumArgumentCount() => 1;
        protected override int? GetMaximumArgumentCount() => 1;

        protected override bool ValidateArgumentTypes(List<object?> args)
        {
            if (args.Count != 1)
            {
                return false;
            }

            // 第一个参数应该是集合或可枚举对象
            var firstArg = args[0];
            if (firstArg == null)
            {
                return true; // null集合返回0
            }

            if (firstArg is string)
            {
                return true;
            }

            // 检查是否为集合类型（排除字符串）
            if (!IsCollection(firstArg))
            {
                return false;
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

            if (collection is string counterName)
            {
                var sourceCard = context.SourceCard;
                if (sourceCard == null)
                {
                    return 0;
                }

                return sourceCard.GetCounter(counterName);
            }

            var enumerable = ConvertToEnumerable(collection);

            return enumerable.Count();
        }
    }
}
