using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    public sealed class VarFunction : BaseDslFunction
    {
        public override string Name => "var";
        public override Type ReturnType => typeof(int);
        public override string Description => "读取源卡牌上的计数器数值。";

        public override IReadOnlyList<DslParameterInfo> Parameters => new List<DslParameterInfo>
        {
            new DslParameterInfo("name", typeof(string), "计数器名称（大小写不敏感）。"),
            new DslParameterInfo("defaultValue", typeof(int), "（可选）计数器缺失时的默认值。", isOptional: true, defaultValue: 0)
        };

        protected override int GetMinimumArgumentCount() => 1;
        protected override int? GetMaximumArgumentCount() => 2;

        protected override bool ValidateArgumentTypes(List<object?> args)
        {
            if (args.Count == 0)
            {
                return false;
            }

            if (args[0] != null && args[0] is not string)
            {
                return false;
            }

            return args.Count < 2 || args[1] == null || args[1] is int || args[1] is long || args[1] is double || args[1] is float || args[1] is decimal || args[1] is string;
        }

        public override object? Execute(List<object?> args, GameContext context)
        {
            if (args.Count == 0)
            {
                return 0;
            }

            var name = ConvertToString(args[0], string.Empty);
            var defaultValue = args.Count >= 2 ? ConvertToInt(args[1], 0) : 0;

            if (string.IsNullOrWhiteSpace(name))
            {
                return defaultValue;
            }

            var sourceCard = context.SourceCard;
            if (sourceCard == null)
            {
                return defaultValue;
            }

            return sourceCard.GetCounter(name);
        }
    }
}
