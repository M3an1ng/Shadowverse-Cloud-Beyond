using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Common; // For ValueConfig if needed

namespace ShadowverseBeyond.Server.Domain.Query
{
    /// <summary>
    /// 代表一个完整的卡牌查询配置。
    /// </summary>
    public record QueryConfig
    {
        /// <summary>
        /// 定义查询初始来源的 DSL 表达式。
        /// </summary>
        public string SourcePoolExpression { get; init; } = string.Empty;

        /// <summary>
        /// 应用于来源的一系列查询操作（筛选、排序、限制等）。
        /// 操作按列表顺序依次执行。
        /// </summary>
        public List<IQueryOperation> Operations { get; init; } = new();
    }

}