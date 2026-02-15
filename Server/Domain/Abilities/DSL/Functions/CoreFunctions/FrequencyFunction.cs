using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// frequency(source, target): count how many elements in source equal target.
    /// </summary>
    public sealed class FrequencyFunction : BaseDslFunction
    {
        public override string Name => "frequency";
        public override Type ReturnType => typeof(int);
        public override string Description => "统计集合中与目标值相等的元素个数。";

        public override IReadOnlyList<DslParameterInfo> Parameters => new List<DslParameterInfo>
        {
            new DslParameterInfo("source", typeof(object), "要统计的集合来源。"),
            new DslParameterInfo("target", typeof(object), "要匹配的目标值。")
        };

        protected override int? GetExpectedArgumentCount() => 2;

        protected override bool ValidateArgumentTypes(List<object?> args)
        {
            if (args.Count != 2)
            {
                return false;
            }

            return args[0] == null || IsCollection(args[0]);
        }

        public override object? Execute(List<object?> args, GameContext context)
        {
            if (args.Count != 2)
            {
                return 0;
            }

            var source = args[0];
            var target = args[1];
            if (source == null || !IsCollection(source))
            {
                return 0;
            }

            var count = 0;
            foreach (var item in ConvertToEnumerable(source))
            {
                if (AreEqual(item, target))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool AreEqual(object? left, object? right)
        {
            if (left == null && right == null)
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left.GetType() == right.GetType())
            {
                return left.Equals(right);
            }

            if (IsNumeric(left) && IsNumeric(right))
            {
                var l = ConvertToDouble(left);
                var r = ConvertToDouble(right);
                return Math.Abs(l - r) < double.Epsilon;
            }

            return string.Equals(
                ConvertToString(left),
                ConvertToString(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNumeric(object? value)
        {
            return value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
        }
    }
}
