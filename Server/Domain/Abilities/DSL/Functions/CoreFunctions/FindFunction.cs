//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// find函数实现 - 按索引访问列表/数组/集合元素（索引从1开始）
    /// 用法:
    /// - list.find(1)
    /// - list.find(1, defaultValue)
    /// </summary>
    public class FindFunction : BaseDslFunction
    {
        public override string Name => "find";
        public override Type ReturnType => typeof(TargetInfo);
        public override string Description => "按索引获取集合中的目标（索引从1开始，越界返回默认值或null）。";

        public override IReadOnlyList<DslParameterInfo> Parameters => new List<DslParameterInfo>
        {
            new DslParameterInfo("source", typeof(object), "列表/数组/可枚举集合。"),
            new DslParameterInfo("index", typeof(int), "索引（从1开始）。"),
            new DslParameterInfo("defaultValue", typeof(object), "越界时返回的默认值（TargetInfo 或 CardInstance）。", isOptional: true)
        };

        protected override int GetMinimumArgumentCount() => 2;
        protected override int? GetMaximumArgumentCount() => 3;

        protected override bool ValidateArgumentTypes(List<object?> args)
        {
            if (args.Count < 2)
            {
                return false;
            }

            return args[0] == null || IsCollection(args[0]) || args[0] is Array || args[0] is IList;
        }

        public override object? Execute(List<object?> args, GameContext context)
        {
            if (args.Count < 2)
            {
                return null;
            }

            var source = args[0];
            var index = ConvertToInt(args[1], 0) - 1;
            var defaultValue = args.Count >= 3 ? ConvertToTargetInfo(args[2]) : null;

            if (source == null || index < 0)
            {
                return defaultValue;
            }

            object? item = null;
            if (source is IList list)
            {
                if (index < list.Count)
                {
                    item = list[index];
                }
            }
            else if (source is Array array)
            {
                if (index < array.Length)
                {
                    item = array.GetValue(index);
                }
            }
            else if (IsCollection(source))
            {
                var items = ConvertToEnumerable(source).ToList();
                if (index < items.Count)
                {
                    item = items[index];
                }
            }

            return ConvertToTargetInfo(item) ?? defaultValue;
        }

        private static TargetInfo? ConvertToTargetInfo(object? value)
        {
            return value switch
            {
                null => null,
                TargetInfo targetInfo => targetInfo,
                CardInstance card => TargetInfo.ForCard(card),
                PlayerState player => TargetInfo.ForPlayer(player),
                _ => null
            };
        }
    }
}
