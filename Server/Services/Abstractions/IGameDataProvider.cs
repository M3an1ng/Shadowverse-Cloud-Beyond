using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    /// <summary>
    /// 游戏数据提供者接口，负责预加载和缓存核心静态游戏数据
    /// 提供高性能的数据访问，消除运行时数据库查询延迟
    /// </summary>
    public interface IGameDataProvider
    {
        /// <summary>
        /// 初始化数据提供者，预加载所有静态游戏数据
        /// </summary>
        /// <returns>初始化任务</returns>
        Task InitializeAsync();

        /// <summary>
        /// 获取指定ID的卡牌定义
        /// </summary>
        /// <param name="cardId">卡牌ID</param>
        /// <returns>卡牌定义，如果不存在则返回null</returns>
        CardDefinition? GetCardDefinition(int cardId);

        /// <summary>
        /// 获取所有卡牌定义的只读字典
        /// </summary>
        /// <returns>卡牌定义字典</returns>
        IReadOnlyDictionary<int, CardDefinition> GetAllCardDefinitions();

        /// <summary>
        /// Gets a read-only collection of all card definitions.
        /// This is the preferred way for DSL to access all cards.
        /// </summary>
        IReadOnlyCollection<CardDefinition> Cards { get; }

        /// <summary>
        /// 获取指定卡牌的所有能力配置
        /// </summary>
        /// <param name="cardId">卡牌ID</param>
        /// <returns>能力配置列表</returns>
        List<AbilityConfig> GetAbilityConfigs(int cardId);

        /// <summary>
        /// 获取指定ID的能力配置
        /// </summary>
        /// <param name="abilityId">能力ID</param>
        /// <returns>能力配置，如果不存在则返回null</returns>
        AbilityConfig? GetAbilityConfig(long abilityId);

        /// <summary>
        /// 获取所有能力配置的只读字典（按卡牌ID分组）
        /// </summary>
        /// <returns>能力配置字典</returns>
        IReadOnlyDictionary<int, List<AbilityConfig>> GetAllAbilityConfigs();

        /// <summary>
        /// 获取指定ID的Buff定义（待实现）
        /// </summary>
        /// <param name="buffId">Buff ID</param>
        /// <returns>Buff定义，如果不存在则返回null</returns>
        BuffDefinition? GetBuffDefinition(string buffId);

        /// <summary>
        /// 刷新缓存，重新从数据库加载所有数据
        /// </summary>
        /// <returns>刷新任务</returns>
        Task RefreshCacheAsync();

        /// <summary>
        /// 清除所有缓存数据
        /// </summary>
        void ClearCache();

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        /// <returns>缓存统计信息</returns>
        CacheStatistics GetCacheStatistics();

        /// <summary>
        /// 检查数据提供者是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 数据缓存版本标识，用于客户端缓存失效。
        /// </summary>
        Guid DataRevision { get; }
    }

    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// 卡牌定义数量
        /// </summary>
        public int CardDefinitionCount { get; set; }

        /// <summary>
        /// 能力配置数量
        /// </summary>
        public int AbilityConfigCount { get; set; }

        /// <summary>
        /// Buff定义数量
        /// </summary>
        public int BuffDefinitionCount { get; set; }

        /// <summary>
        /// 最后初始化时间
        /// </summary>
        public DateTime LastInitialized { get; set; }

        /// <summary>
        /// 初始化耗时
        /// </summary>
        public TimeSpan InitializationDuration { get; set; }

        /// <summary>
        /// 内存使用量（字节）
        /// </summary>
        public long MemoryUsageBytes { get; set; }

        /// <summary>
        /// 缓存命中次数
        /// </summary>
        public int CacheHitCount { get; set; }

        /// <summary>
        /// 缓存未命中次数
        /// </summary>
        public int CacheMissCount { get; set; }

        /// <summary>
        /// 缓存命中率
        /// </summary>
        public double CacheHitRatio => CacheHitCount + CacheMissCount > 0
            ? (double)CacheHitCount / (CacheHitCount + CacheMissCount)
            : 0.0;
    }

}
