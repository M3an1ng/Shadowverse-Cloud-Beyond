using System;
using System.Collections.Generic; // Added for List
using ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors;
using ShadowverseBeyond.Server.Domain.Query; // Added for CardQueryConfig, SelectionOperation etc.
using ShadowverseBeyond.Server.Domain.Abilities.Common; // Added for ValueConfig

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// 目标选择器工厂类
    /// 根据配置创建相应的目标选择器实例
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public static class TargetSelectorFactory
    {
        /// <summary>
        /// 根据配置创建目标选择器
        /// </summary>
        /// <param name="config">目标选择器配置</param>
        /// <param name="queryExecutor">卡牌查询执行器依赖</param>
        /// <param name="logger">日志记录器依赖</param>
        /// <returns>目标选择器实例</returns>
        public static ITargetSelector CreateSelector(TargetConfig config, ICardQueryExecutor queryExecutor, ILogger<FilteredTargetSelector> logger)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (queryExecutor == null)
                throw new ArgumentNullException(nameof(queryExecutor));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            // Use fully qualified name for Common.SelectorType to avoid ambiguity
            // Note: We assume the TargetConfig passed in already has the correct SelectorType set.
            // The factory methods below now correctly use Common.SelectorType.Filtered.
            return config.SelectorType switch
            {
                // Assuming the SelectorType enum values in Common and Targets are compatible where names match
                Common.SelectorType.Self => new SelfTargetSelector(),
                // Pass dependencies to FilteredTargetSelector constructor
                Common.SelectorType.Filtered => new FilteredTargetSelector(queryExecutor, logger),
                Common.SelectorType.FromVariable => new FromVariableTargetSelector(config), // Assuming these don't need dependencies yet
                Common.SelectorType.TriggeringEventData => new TriggeringEventDataTargetSelector(config), // Assuming these don't need dependencies yet
                // Pass dependencies to CompositeTargetSelector constructor
                Common.SelectorType.Composite => new CompositeTargetSelector(config, queryExecutor, logger),
                Common.SelectorType.PlayerChoice => new PlayerChoiceTargetSelector(config),
                _ => throw new ArgumentException($"Unsupported selector type: {config.SelectorType}", nameof(config))
            };
        }

        /// <summary>
        /// 根据JSON配置字符串创建目标选择器
        /// </summary>
        /// <param name="jsonConfig">JSON配置字符串</param>
        /// <param name="queryExecutor">卡牌查询执行器依赖</param>
        /// <param name="logger">日志记录器依赖</param>
        /// <returns>目标选择器实例</returns>
        public static ITargetSelector CreateSelectorFromJson(string jsonConfig, ICardQueryExecutor queryExecutor, ILogger<FilteredTargetSelector> logger)
        {
            if (string.IsNullOrEmpty(jsonConfig))
                throw new ArgumentException("JSON config cannot be null or empty", nameof(jsonConfig));
            if (queryExecutor == null)
                throw new ArgumentNullException(nameof(queryExecutor));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            // TODO: 实现JSON反序列化
            // var config = JsonSerializer.Deserialize<TargetConfig>(jsonConfig);
            // return CreateSelector(config, queryExecutor, logger); // Pass dependencies

            throw new NotImplementedException("JSON deserialization not implemented yet");
        }

        /// <summary>
        /// 创建简单的自身目标选择器
        /// </summary>
        /// <returns>自身目标选择器</returns>
        public static ITargetSelector CreateSelfSelector()
        {
            // SelfTargetSelector likely doesn't need complex config, but check its constructor
            var targetConfig = new TargetConfig
            {
                SelectorType = Common.SelectorType.Self, // Use Common enum
                Query = new CardQueryConfig { SourcePoolExpression = "Context.SourceCard" } // Basic query for self
            };
            return new SelfTargetSelector(); // Assuming constructor is parameterless or takes basic config
        }

        /// <summary>
        /// 创建简单的敌方场上目标选择器 (已重构)
        /// </summary>
        /// <param name="count">选择数量</param>
        /// <param name="requirePlayerChoice">是否需要玩家选择</param>
        /// <param name="queryExecutor">卡牌查询执行器依赖</param>
        /// <param name="logger">日志记录器依赖</param>
        /// <returns>过滤目标选择器</returns>
        public static ITargetSelector CreateEnemyFieldSelector(ICardQueryExecutor queryExecutor, ILogger<FilteredTargetSelector> logger, int count = 1, bool requirePlayerChoice = true)
        {
            if (queryExecutor == null) throw new ArgumentNullException(nameof(queryExecutor));
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            // Refactored to use CardQueryConfig within TargetConfig
            var queryConfig = new CardQueryConfig
            {
                SourcePoolExpression = "Opponent.Field", // Directly set the source pool expression
                Operations = new List<IQueryOperation>()
            };

            // Add SelectionOperation for count limit
            queryConfig.Operations.Add(new SelectionOperation
            {
                // Assuming SelectionStrategy.First is appropriate when a specific count is given
                Strategy = SelectionStrategy.First,
                CountSource = ValueConfig.Static(count)
            });

            var targetConfig = new TargetConfig
            {
                // Use fully qualified name for Common.SelectorType
                SelectorType = Common.SelectorType.Filtered,
                Query = queryConfig, // Assign the configured query
                PlayerChoiceOptions = requirePlayerChoice ? new PlayerChoiceConfig
                {
                    IsRequired = true,
                    Prompt = "选择一个敌方目标"
                } : null // Assign to PlayerChoiceOptions
            };

            // Pass dependencies to FilteredTargetSelector constructor
            return new FilteredTargetSelector(queryExecutor, logger);
        }

        /// <summary>
        /// 创建简单的友方场上目标选择器 (已重构)
        /// </summary>
        /// <param name="count">选择数量</param>
        /// <param name="requirePlayerChoice">是否需要玩家选择</param>
        /// <param name="queryExecutor">卡牌查询执行器依赖</param>
        /// <param name="logger">日志记录器依赖</param>
        /// <returns>过滤目标选择器</returns>
        public static ITargetSelector CreateAllyFieldSelector(ICardQueryExecutor queryExecutor, ILogger<FilteredTargetSelector> logger, int count = 1, bool requirePlayerChoice = true)
        {
            if (queryExecutor == null) throw new ArgumentNullException(nameof(queryExecutor));
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            var queryConfig = new CardQueryConfig
            {
                SourcePoolExpression = "Own.Field",
                Operations = new List<IQueryOperation>()
            };

            queryConfig.Operations.Add(new SelectionOperation
            {
                Strategy = SelectionStrategy.First,
                CountSource = ValueConfig.Static(count)
            });

            var targetConfig = new TargetConfig
            {
                SelectorType = Common.SelectorType.Filtered,
                Query = queryConfig,
                PlayerChoiceOptions = requirePlayerChoice ? new PlayerChoiceConfig
                {
                    IsRequired = true,
                    Prompt = "选择一个友方目标"
                } : null
            };

            // Pass dependencies to FilteredTargetSelector constructor
            return new FilteredTargetSelector(queryExecutor, logger);
        }

        /// <summary>
        /// 创建所有敌方场上目标选择器 (已重构)
        /// </summary>
        /// <param name="queryExecutor">卡牌查询执行器依赖</param>
        /// <param name="logger">日志记录器依赖</param>
        /// <returns>过滤目标选择器</returns>
        public static ITargetSelector CreateAllEnemyFieldSelector(ICardQueryExecutor queryExecutor, ILogger<FilteredTargetSelector> logger)
        {
            if (queryExecutor == null) throw new ArgumentNullException(nameof(queryExecutor));
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            var queryConfig = new CardQueryConfig
            {
                SourcePoolExpression = "Opponent.Field",
                // No SelectionOperation needed for "All" by default with CardQueryConfig
                Operations = new List<IQueryOperation>()
            };

            var targetConfig = new TargetConfig
            {
                SelectorType = Common.SelectorType.Filtered,
                Query = queryConfig
                // No PlayerChoice needed for "All"
            };

            // Pass dependencies to FilteredTargetSelector constructor
            return new FilteredTargetSelector(queryExecutor, logger);
        }

        /// <summary>
        /// 创建所有友方场上目标选择器 (已重构)
        /// </summary>
        /// <param name="queryExecutor">卡牌查询执行器依赖</param>
        /// <param name="logger">日志记录器依赖</param>
        /// <returns>过滤目标选择器</returns>
        public static ITargetSelector CreateAllAllyFieldSelector(ICardQueryExecutor queryExecutor, ILogger<FilteredTargetSelector> logger)
        {
            if (queryExecutor == null) throw new ArgumentNullException(nameof(queryExecutor));
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            var queryConfig = new CardQueryConfig
            {
                SourcePoolExpression = "Own.Field",
                Operations = new List<IQueryOperation>()
            };

            var targetConfig = new TargetConfig
            {
                SelectorType = Common.SelectorType.Filtered,
                Query = queryConfig
            };

            // Pass dependencies to FilteredTargetSelector constructor
            return new FilteredTargetSelector(queryExecutor, logger);
        }
    }
}
