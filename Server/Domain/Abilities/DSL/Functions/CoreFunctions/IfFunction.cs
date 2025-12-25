//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// If函数实现 - 条件选择
    /// 支持用法：
    /// - if(condition, thenValue, elseValue) - 如果条件为真返回thenValue，否则返回elseValue
    /// - if(condition, thenValue) - 如果条件为真返回thenValue，否则返回null
    /// </summary>
    public class IfFunction : BaseDslFunction
    {
        public override string Name => "if";
        public override string Description => "根据条件返回两个值中的一个。";

        public override IReadOnlyList<DslParameterInfo> Parameters => new List<DslParameterInfo>
        {
            new DslParameterInfo("condition", typeof(bool), "判断条件。"),
            new DslParameterInfo("thenValue", typeof(object), "如果条件为真，则返回此值。"),
            new DslParameterInfo("elseValue", typeof(object), "如果条件为假，则返回此值。", isOptional: true, defaultValue: null)
        };

        protected override int GetMinimumArgumentCount() => 2;
        protected override int? GetMaximumArgumentCount() => 3;

        public override object? Execute(List<object?> args, GameContext context)
        {
            if (args.Count < 2)
            {
                return null;
            }

            var condition = args[0];
            var thenValue = args[1];
            var elseValue = args.Count > 2 ? args[2] : null;

            var conditionResult = ConvertToBool(condition);

            return conditionResult ? thenValue : elseValue;
        }
    }
}
