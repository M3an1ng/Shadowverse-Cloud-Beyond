using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Dtos; // For CardInstanceDto, CardDefinitionDto if needed directly
using ShadowverseBeyond.Server.Domain.Abilities.Common; // Added for CardQueryConfig
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // Added for TargetSelectionContext

namespace ShadowverseBeyond.Server.Domain.Query
{
    /// <summary>
    /// 封装卡牌查询的结果。
    /// </summary>
    public record QueryResult
    {
        /// <summary>
        /// 查询到的卡牌列表。
        /// 使用 IReadOnlyList<object> 以存储两种可能的类型。
        /// </summary>
        public IReadOnlyList<object> Results { get; init; } = Array.Empty<object>();

        /// <summary>
        /// 指示查询是否成功执行。
        /// </summary>
        public bool Success { get; init; } = true;

        /// <summary>
        /// 如果查询失败，包含错误信息。
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// 辅助方法，尝试将结果列表转换为指定的类型 T。
        /// </summary>
        /// <typeparam name="T">目标类型 (应为 CardInstanceDto 或 CardDefinitionDto)。</typeparam>
        /// <returns>转换后的只读列表。</returns>
        public IReadOnlyList<T> GetResultsAs<T>() where T : class
        {
            // 使用 OfType<T> 安全地过滤和转换元素
            return Results.OfType<T>().ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// 定义执行卡牌查询配置的契约。
    /// </summary>
    public interface IQueryExecutor
    {
        /// <summary>
        /// 根据给定的DSL查询字符串和上下文同步执行卡牌查询。
        /// </summary>
        /// <param name="dsl">DSL查询字符串。</param>
        /// <param name="context">统一的游戏上下文。</param>
        /// <returns>包含查询结果的 CardQueryResult。</returns>
        QueryResult ExecuteQuery(string dsl, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null);

        /// <summary>
        /// 根据给定的查询配置和上下文同步执行卡牌查询。
        /// </summary>
        /// <param name="queryConfig">卡牌查询配置。</param>
        /// <param name="context">统一的游戏上下文。</param>
        /// <param name="resolvedTargets">一个可选的、包含先前已解析目标的字典，用于依赖性查询。</param>
        /// <returns>包含查询结果的 CardQueryResult。</returns>
        QueryResult ExecuteQuery(QueryConfig queryConfig, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null);
    }
}