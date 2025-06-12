using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// 游戏数据提供者实现，负责预加载和缓存核心静态游戏数据
    /// 采用单例模式，在应用启动时预加载所有数据到内存中
    /// </summary>
    public class GameDataProvider : IGameDataProvider
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GameDataProvider> _logger;

        // 缓存字典
        private readonly ConcurrentDictionary<int, CardDefinition> _cardDefinitions = new();
        private readonly ConcurrentDictionary<int, List<AbilityConfig>> _abilityConfigsByCard = new();
        private readonly ConcurrentDictionary<long, AbilityConfig> _abilityConfigsById = new();
        private readonly ConcurrentDictionary<string, BuffDefinition> _buffDefinitions = new();

        // 缓存状态
        private volatile bool _isInitialized = false;
        private readonly SemaphoreSlim _initializationLock = new(1, 1);
        private DateTime _lastInitialized = DateTime.MinValue;
        private TimeSpan _initializationDuration = TimeSpan.Zero;

        // 统计信息
        private int _cacheHitCount = 0;
        private int _cacheMissCount = 0;

        public bool IsInitialized => _isInitialized;

        public GameDataProvider(
            IServiceScopeFactory scopeFactory,
            ILogger<GameDataProvider> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _initializationLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                _logger.LogInformation("开始初始化GameDataProvider缓存...");
                var startTime = DateTime.UtcNow;

                await LoadCardDefinitionsAsync();
                await LoadAbilityConfigsAsync();
                await LoadBuffDefinitionsAsync();

                _isInitialized = true;
                _lastInitialized = DateTime.UtcNow;
                _initializationDuration = DateTime.UtcNow - startTime;

                _logger.LogInformation("GameDataProvider缓存初始化完成，耗时: {Duration}ms，" +
                    "卡牌定义: {CardCount}，能力配置: {AbilityCount}，Buff定义: {BuffCount}",
                    _initializationDuration.TotalMilliseconds,
                    _cardDefinitions.Count,
                    _abilityConfigsById.Count,
                    _buffDefinitions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GameDataProvider初始化失败");
                throw;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        /// <inheritdoc />
        public CardDefinition? GetCardDefinition(int cardId)
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("GameDataProvider未初始化，无法获取卡牌定义 {CardId}", cardId);
                Interlocked.Increment(ref _cacheMissCount);
                return null;
            }

            if (_cardDefinitions.TryGetValue(cardId, out var cardDefinition))
            {
                Interlocked.Increment(ref _cacheHitCount);
                return cardDefinition;
            }

            Interlocked.Increment(ref _cacheMissCount);
            _logger.LogDebug("未找到卡牌定义 {CardId}", cardId);
            return null;
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<int, CardDefinition> GetAllCardDefinitions()
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("GameDataProvider未初始化，返回空字典");
                return new Dictionary<int, CardDefinition>();
            }

            return _cardDefinitions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <inheritdoc />
        public List<AbilityConfig> GetAbilityConfigs(int cardId)
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("GameDataProvider未初始化，无法获取能力配置 {CardId}", cardId);
                Interlocked.Increment(ref _cacheMissCount);
                return new List<AbilityConfig>();
            }

            if (_abilityConfigsByCard.TryGetValue(cardId, out var configs))
            {
                Interlocked.Increment(ref _cacheHitCount);
                return new List<AbilityConfig>(configs); // 返回副本
            }

            Interlocked.Increment(ref _cacheMissCount);
            _logger.LogDebug("未找到卡牌 {CardId} 的能力配置", cardId);
            return new List<AbilityConfig>();
        }

        /// <inheritdoc />
        public AbilityConfig? GetAbilityConfig(long abilityId)
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("GameDataProvider未初始化，无法获取能力配置 {AbilityId}", abilityId);
                Interlocked.Increment(ref _cacheMissCount);
                return null;
            }

            if (_abilityConfigsById.TryGetValue(abilityId, out var config))
            {
                Interlocked.Increment(ref _cacheHitCount);
                return config;
            }

            Interlocked.Increment(ref _cacheMissCount);
            _logger.LogDebug("未找到能力配置 {AbilityId}", abilityId);
            return null;
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<int, List<AbilityConfig>> GetAllAbilityConfigs()
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("GameDataProvider未初始化，返回空字典");
                return new Dictionary<int, List<AbilityConfig>>();
            }

            return _abilityConfigsByCard.ToDictionary(
                kvp => kvp.Key,
                kvp => new List<AbilityConfig>(kvp.Value));
        }

        /// <inheritdoc />
        public BuffDefinition? GetBuffDefinition(string buffId)
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("GameDataProvider未初始化，无法获取Buff定义 {BuffId}", buffId);
                Interlocked.Increment(ref _cacheMissCount);
                return null;
            }

            if (_buffDefinitions.TryGetValue(buffId, out var buffDefinition))
            {
                Interlocked.Increment(ref _cacheHitCount);
                return buffDefinition;
            }

            Interlocked.Increment(ref _cacheMissCount);
            _logger.LogDebug("未找到Buff定义 {BuffId}", buffId);
            return null;
        }

        /// <inheritdoc />
        public async Task RefreshCacheAsync()
        {
            _logger.LogInformation("开始刷新GameDataProvider缓存...");

            _isInitialized = false;
            ClearCache();

            await InitializeAsync();

            _logger.LogInformation("GameDataProvider缓存刷新完成");
        }

        /// <inheritdoc />
        public void ClearCache()
        {
            _cardDefinitions.Clear();
            _abilityConfigsByCard.Clear();
            _abilityConfigsById.Clear();
            _buffDefinitions.Clear();

            _cacheHitCount = 0;
            _cacheMissCount = 0;

            _logger.LogDebug("GameDataProvider缓存已清除");
        }

        /// <inheritdoc />
        public CacheStatistics GetCacheStatistics()
        {
            return new CacheStatistics
            {
                CardDefinitionCount = _cardDefinitions.Count,
                AbilityConfigCount = _abilityConfigsById.Count,
                BuffDefinitionCount = _buffDefinitions.Count,
                LastInitialized = _lastInitialized,
                InitializationDuration = _initializationDuration,
                MemoryUsageBytes = EstimateMemoryUsage(),
                CacheHitCount = _cacheHitCount,
                CacheMissCount = _cacheMissCount
            };
        }

        private async Task LoadCardDefinitionsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cardRepository = scope.ServiceProvider.GetRequiredService<ICardRepository>();

                _logger.LogInformation("开始加载卡牌定义...");
                var allCards = await cardRepository.GetAllCardDefinitionsAsync();

                _cardDefinitions.Clear();
                foreach (var card in allCards)
                {
                    _cardDefinitions[card.Id] = card;
                }

                _logger.LogInformation("加载了 {Count} 个卡牌定义", allCards.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载卡牌定义时发生错误");
                throw;
            }
        }

        private async Task LoadAbilityConfigsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                // Use the registered AbilityLoader service instead of creating new instance
                var abilityLoader = scope.ServiceProvider.GetRequiredService<AbilityLoader>();

                _logger.LogInformation("开始加载能力配置...");
                var allAbilities = await abilityLoader.LoadAllAbilitiesAsync();

                _abilityConfigsByCard.Clear();
                _abilityConfigsById.Clear();

                foreach (var ability in allAbilities)
                {
                    // 按卡牌ID索引
                    _abilityConfigsByCard.AddOrUpdate(
                        ability.CardId,
                        new List<AbilityConfig> { ability },
                        (key, existing) => { existing.Add(ability); return existing; });

                    // 按能力ID索引 - 修复类型转换问题
                    if (long.TryParse(ability.Id, out var abilityIdLong))
                    {
                        _abilityConfigsById[abilityIdLong] = ability;
                    }
                }

                _logger.LogInformation("加载了 {Count} 个能力配置", allAbilities.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载能力配置时发生错误");
                throw;
            }
        }

        private async Task LoadBuffDefinitionsAsync()
        {
            // TODO: 实现BuffDefinition的数据库加载
            // 当前为占位符实现
            _logger.LogDebug("BuffDefinition加载暂未实现，跳过");
            await Task.CompletedTask;
        }

        private long EstimateMemoryUsage()
        {
            // 简单的内存使用估算
            long totalBytes = 0;

            // 估算卡牌定义内存使用
            totalBytes += _cardDefinitions.Count * 1024; // 假设每个卡牌定义约1KB

            // 估算能力配置内存使用
            totalBytes += _abilityConfigsById.Count * 2048; // 假设每个能力配置约2KB

            // 估算Buff定义内存使用
            totalBytes += _buffDefinitions.Count * 512; // 假设每个Buff定义约512B

            return totalBytes;
        }
    }
}
