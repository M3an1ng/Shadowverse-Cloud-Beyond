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
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// find函数实现 - 按索引访问列表/数组/集合元素（索引从1开始）
    /// 用法:
    /// - list.find(1)
    /// - list.find(1, defaultValue)
    /// </summary>
    public class FindFunction : BaseDslFunction, IFunctionReturnTypeResolver
    {
        public override string Name => "find";
        public override Type ReturnType => typeof(object);
        public override string Description => "按索引获取集合中的第X个目标（X从1开始，越界返回默认值或null）。";

        public override IReadOnlyList<DslParameterInfo> Parameters => new List<DslParameterInfo>
        {
            new DslParameterInfo("source", typeof(object), "列表/数组/可枚举集合。"),
            new DslParameterInfo("index", typeof(int), "索引（从1开始）。"),
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

        public Type? ResolveReturnType(IReadOnlyList<Type> argumentTypes)
        {
            if (argumentTypes == null || argumentTypes.Count == 0)
            {
                return ReturnType;
            }

            var sourceType = argumentTypes[0];
            if (sourceType == null || sourceType == typeof(object) || sourceType == typeof(string))
            {
                return ReturnType;
            }

            return DslCollectionCoercion.TryGetEnumerableElementType(sourceType, out var elementType)
                ? elementType
                : sourceType;
        }

        public override object? Execute(List<object?> args, GameContext context)
        {
            if (args.Count < 2)
            {
                return null;
            }

            var source = args[0];
            var index = ConvertToInt(args[1], 0) - 1;
            var defaultValue = args.Count >= 3 ? args[2] : null;

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

            return item ?? defaultValue;
        }
    }
}
