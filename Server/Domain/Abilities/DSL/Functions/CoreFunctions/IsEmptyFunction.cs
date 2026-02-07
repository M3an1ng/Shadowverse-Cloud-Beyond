//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// IsEmpty函数实现 - 检查集合或字符串是否为空
    /// 支持用法：
    /// - isEmpty(collection) - 检查集合是否为空
    /// - isEmpty(string) - 检查字符串是否为空或null
    /// </summary>
    public class IsEmptyFunction : BaseDslFunction
    {
        public override string Name => "isEmpty";
        public override Type ReturnType => typeof(bool);
        public override string Description => "检查一个集合是否为空。";

        public override IReadOnlyList<DslParameterInfo> Parameters => new List<DslParameterInfo>
        {
            new DslParameterInfo("collection", typeof(IEnumerable), "要进行检查的集合。")
        };

        protected override int? GetExpectedArgumentCount() => 1;

        protected override bool ValidateArgumentTypes(List<object?> args)
        {
            if (args.Count != 1)
            {
                return false;
            }

            return true;
        }

        public override object? Execute(List<object?> args, GameContext context)
        {
            if (args.Count != 1)
            {
                return true; // 无效参数视为空
            }

            var value = args[0];

            // null值视为空
            if (value == null)
            {
                return true;
            }

            // 字符串检查
            if (value is string stringValue)
            {
                return string.IsNullOrEmpty(stringValue);
            }

            var enumerable = ConvertToEnumerable(value);
            return !enumerable.Any();
        }
    }
}
