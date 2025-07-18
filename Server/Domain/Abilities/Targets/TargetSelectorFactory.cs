using System;
using System.Collections.Concurrent;
using System.Collections.Generic; // Added for List
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors;
using ShadowverseBeyond.Server.Domain.Query; // Added for CardQueryConfig, SelectionOperation etc.
using ShadowverseBeyond.Server.Domain.Abilities.Common; // Added for ValueConfig

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// 目标选择器工厂类
    /// 根据配置创建相应的目标选择器实例
    /// 包含缓存机制以提升性能
    /// </summary>
    public static class TargetSelectorFactory
    {
        /// <summary>
        /// 常用选择器的缓存，提升性能
        /// 键为选择器配置的哈希值，值为选择器实例
        /// </summary>
        private static readonly ConcurrentDictionary<string, ITargetSelector> _selectorCache = new();

        /// <summary>
        /// 缓存统计信息
        /// </summary>
        public static class CacheStatistics
        {
            public static int CacheHits { get; private set; }
            public static int CacheMisses { get; private set; }
            public static int CacheSize => _selectorCache.Count;

            internal static void RecordHit() => CacheHits++;
            internal static void RecordMiss() => CacheMisses++;

            public static void Reset()
            {
                CacheHits = 0;
                CacheMisses = 0;
            }
        }

        /// <summary>
        /// 清空选择器缓存
        /// </summary>
        public static void ClearCache()
        {
            _selectorCache.Clear();
            CacheStatistics.Reset();
        }
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

            return CreateSelectorInternal(config, queryExecutor, logger);
        }

        /// <summary>
        /// 根据配置创建目标选择器（带缓存）
        /// 对于常用的选择器配置，使用缓存提升性能
        /// </summary>
        /// <param name="config">目标选择器配置</param>
        /// <param name="queryExecutor">卡牌查询执行器依赖</param>
        /// <param name="logger">日志记录器依赖</param>
        /// <param name="useCache">是否使用缓存</param>
        /// <returns>目标选择器实例</returns>
        public static ITargetSelector CreateSelector(TargetConfig config, ICardQueryExecutor queryExecutor, ILogger<FilteredTargetSelector> logger, bool useCache)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (queryExecutor == null)
                throw new ArgumentNullException(nameof(queryExecutor));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            if (!useCache)
            {
                return CreateSelectorInternal(config, queryExecutor, logger);
            }

            // 生成缓存键
            var cacheKey = GenerateCacheKey(config);

            if (_selectorCache.TryGetValue(cacheKey, out var cachedSelector))
            {
                CacheStatistics.RecordHit();
                return cachedSelector;
            }

            CacheStatistics.RecordMiss();
            var newSelector = CreateSelectorInternal(config, queryExecutor, logger);

            // 只缓存无状态的选择器类型
            if (IsCacheableSelector(config.SelectorType))
            {
                _selectorCache.TryAdd(cacheKey, newSelector);
            }

            return newSelector;
        }

        /// <summary>
        /// 内部选择器创建方法
        /// </summary>
        private static ITargetSelector CreateSelectorInternal(TargetConfig config, ICardQueryExecutor queryExecutor, ILogger<FilteredTargetSelector> logger)
        {
            // Use fully qualified name for Common.SelectorType to avoid ambiguity
            // Note: We assume the TargetConfig passed in already has the correct SelectorType set.
            // The factory methods below now correctly use Common.SelectorType.Filtered.
            return config.SelectorType switch
            {
                // Assuming the SelectorType enum values in Common and Targets are compatible where names match
                Common.SelectorType.Self => new SelfTargetSelector(
                    logger as ILogger<SelfTargetSelector> ??
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<SelfTargetSelector>.Instance),
                // Pass dependencies to FilteredTargetSelector constructor
                Common.SelectorType.Filtered => new FilteredTargetSelector(queryExecutor, logger),
                Common.SelectorType.TriggeringEventData => new TriggeringEventDataTargetSelector(config), // Assuming these don't need dependencies yet
                // Pass dependencies to CompositeTargetSelector constructor
                Common.SelectorType.Composite => new CompositeTargetSelector(config, queryExecutor, logger),
                Common.SelectorType.PlayerChoice => new PlayerChoiceTargetSelector(config, queryExecutor,
                    logger as ILogger<PlayerChoiceTargetSelector> ??
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<PlayerChoiceTargetSelector>.Instance),
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

            try
            {
                // Configure JSON serializer options for proper deserialization
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    AllowTrailingCommas = true
                };

                // Deserialize the JSON configuration
                var config = JsonSerializer.Deserialize<TargetConfig>(jsonConfig, options);

                if (config == null)
                {
                    throw new InvalidOperationException("Failed to deserialize TargetConfig from JSON - result was null");
                }

                // Validate the deserialized configuration
                ValidateTargetConfig(config);

                // Use the main factory method to create the selector with proper dependencies
                return CreateSelector(config, queryExecutor, logger);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Invalid JSON configuration: {ex.Message}", nameof(jsonConfig), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create target selector from JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建简单的自身目标选择器
        /// </summary>
        /// <param name="queryExecutor">卡牌查询执行器依赖（可选）</param>
        /// <param name="logger">日志记录器依赖（可选）</param>
        /// <returns>自身目标选择器</returns>
        public static ITargetSelector CreateSelfSelector(ICardQueryExecutor? queryExecutor = null, ILogger<FilteredTargetSelector>? logger = null)
        {
            var targetConfig = new TargetConfig
            {
                SelectorType = Common.SelectorType.Self,
                Query = new CardQueryConfig { SourcePoolExpression = "Context.SourceCard" }
            };

            // 如果提供了依赖，使用主工厂方法；否则直接创建
            if (queryExecutor != null && logger != null)
            {
                return CreateSelector(targetConfig, queryExecutor, logger);
            }
            else
            {
                // 为了向后兼容，直接创建SelfTargetSelector
                return new SelfTargetSelector(
                    logger as ILogger<SelfTargetSelector> ??
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<SelfTargetSelector>.Instance);
            }
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

            // Use the main factory method for consistency
            return CreateSelector(targetConfig, queryExecutor, logger);
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

            // Use the main factory method for consistency
            return CreateSelector(targetConfig, queryExecutor, logger);
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

            // Use the main factory method for consistency
            return CreateSelector(targetConfig, queryExecutor, logger);
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

            // Use the main factory method for consistency
            return CreateSelector(targetConfig, queryExecutor, logger);
        }

        /// <summary>
        /// 验证目标配置的有效性
        /// </summary>
        /// <param name="config">要验证的目标配置</param>
        /// <exception cref="ArgumentException">当配置无效时抛出</exception>
        private static void ValidateTargetConfig(TargetConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // 验证选择器类型是否有效
            if (!Enum.IsDefined(typeof(Common.SelectorType), config.SelectorType))
            {
                throw new ArgumentException($"Invalid SelectorType: {config.SelectorType}", nameof(config));
            }

            // 验证特定选择器类型的配置
            switch (config.SelectorType)
            {
                case Common.SelectorType.PlayerChoice:
                    if (config.PlayerChoiceOptions == null)
                    {
                        throw new ArgumentException("PlayerChoiceOptions is required for PlayerChoice selector type", nameof(config));
                    }
                    ValidatePlayerChoiceConfig(config.PlayerChoiceOptions);
                    break;

                case Common.SelectorType.Composite:
                    if (config.SubSelectors == null || config.SubSelectors.Count == 0)
                    {
                        throw new ArgumentException("SubSelectors is required for Composite selector type", nameof(config));
                    }
                    // 递归验证子选择器
                    foreach (var subSelector in config.SubSelectors)
                    {
                        ValidateTargetConfig(subSelector);
                    }
                    break;

                case Common.SelectorType.Filtered:
                    if (config.Query == null)
                    {
                        throw new ArgumentException("Query is required for Filtered selector type", nameof(config));
                    }
                    ValidateCardQueryConfig(config.Query);
                    break;
            }

            // 验证失败处理方式
            if (!Enum.IsDefined(typeof(FailureHandling), config.OnFailure))
            {
                throw new ArgumentException($"Invalid FailureHandling: {config.OnFailure}", nameof(config));
            }
        }

        /// <summary>
        /// 验证玩家选择配置的有效性
        /// </summary>
        /// <param name="config">玩家选择配置</param>
        private static void ValidatePlayerChoiceConfig(PlayerChoiceConfig config)
        {
            if (config.MinCount < 0)
            {
                throw new ArgumentException("MinCount cannot be negative", nameof(config));
            }

            if (config.MaxCount.HasValue && config.MaxCount.Value < config.MinCount)
            {
                throw new ArgumentException("MaxCount cannot be less than MinCount", nameof(config));
            }

            if (config.MaxCount.HasValue && config.MaxCount.Value <= 0)
            {
                throw new ArgumentException("MaxCount must be positive when specified", nameof(config));
            }
        }

        /// <summary>
        /// 验证卡牌查询配置的有效性
        /// </summary>
        /// <param name="config">卡牌查询配置</param>
        private static void ValidateCardQueryConfig(CardQueryConfig config)
        {
            if (string.IsNullOrEmpty(config.SourcePoolExpression))
            {
                throw new ArgumentException("SourcePoolExpression cannot be null or empty", nameof(config));
            }

            // 验证查询类型
            if (!Enum.IsDefined(typeof(QueryType), config.TargetType))
            {
                throw new ArgumentException($"Invalid QueryType: {config.TargetType}", nameof(config));
            }

            // 可以添加更多的查询配置验证逻辑
            // 例如验证 Operations 列表中的操作是否有效
        }

        /// <summary>
        /// 生成配置的缓存键
        /// </summary>
        /// <param name="config">目标配置</param>
        /// <returns>缓存键字符串</returns>
        private static string GenerateCacheKey(TargetConfig config)
        {
            // 使用JSON序列化生成一致的缓存键
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false // 紧凑格式用于缓存键
            };

            try
            {
                return JsonSerializer.Serialize(config, options);
            }
            catch
            {
                // 如果序列化失败，使用基本信息生成键
                return $"{config.SelectorType}_{config.Query?.SourcePoolExpression ?? "null"}_{config.VariableName ?? "null"}";
            }
        }

        /// <summary>
        /// 检查选择器类型是否可以缓存
        /// 只有无状态的选择器才适合缓存
        /// </summary>
        /// <param name="selectorType">选择器类型</param>
        /// <returns>如果可以缓存返回true</returns>
        private static bool IsCacheableSelector(Common.SelectorType selectorType)
        {
            return selectorType switch
            {
                Common.SelectorType.Self => true,           // 无状态，可缓存
                Common.SelectorType.Filtered => true,       // 查询逻辑无状态，可缓存
                Common.SelectorType.TriggeringEventData => false, // 依赖事件数据，不可缓存
                Common.SelectorType.Composite => false,     // 复合选择器可能有状态，不可缓存
                Common.SelectorType.PlayerChoice => false,  // 涉及用户交互，不可缓存
                _ => false // 未知类型默认不缓存
            };
        }

        /// <summary>
        /// 获取缓存使用情况报告
        /// </summary>
        /// <returns>缓存统计信息</returns>
        public static string GetCacheReport()
        {
            var hitRate = CacheStatistics.CacheHits + CacheStatistics.CacheMisses > 0
                ? (double)CacheStatistics.CacheHits / (CacheStatistics.CacheHits + CacheStatistics.CacheMisses) * 100
                : 0;

            return $"TargetSelectorFactory Cache Report:\n" +
                   $"- Cache Size: {CacheStatistics.CacheSize}\n" +
                   $"- Cache Hits: {CacheStatistics.CacheHits}\n" +
                   $"- Cache Misses: {CacheStatistics.CacheMisses}\n" +
                   $"- Hit Rate: {hitRate:F2}%";
        }
    }
}
