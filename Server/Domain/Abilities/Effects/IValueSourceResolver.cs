using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    /// <summary>
    /// 值源解析器接口
    /// 根据M12设计文档实现，负责解析效果配置中的动态值
    /// </summary>
    public interface IValueSourceResolver
    {
        /// <summary>
        /// 解析值源
        /// </summary>
        /// <param name="valueConfig">值配置</param>
        /// <param name="context">统一游戏上下文</param>
        /// <returns>解析后的值</returns>
        object? Resolve(ValueConfig valueConfig, GameContext context);

        object? ResolveFormulaValue(string formula, GameContext context);

        object? ResolveFormulaValueWithTrace(string formula, GameContext context, IDslTraceCollector traceCollector);

        /// <summary>
        /// 解析为整数
        /// </summary>
        /// <param name="valueConfig">值配置</param>
        /// <param name="context">统一游戏上下文</param>
        /// <returns>整数值</returns>
    }
}
