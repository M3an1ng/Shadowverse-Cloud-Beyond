using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// 游戏数据提供者实现，负责预加载和缓存核心静态游戏数据
    /// 采用单例模式，在应用启动时预加载所有数据到内存中
    /// </summary>
    public class GameDataProvider : IGameDataProvider, IEnvironmentScopedGameDataProvider
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GameDataProvider> _logger;

        private sealed class CacheSnapshot
        {
            public CacheSnapshot(
                Dictionary<int, CardDefinition> cardDefinitions,
                IReadOnlyList<CardDefinition> cards,
                IReadOnlyDictionary<DeckEnvironment, IReadOnlyList<CardDefinition>> cardDefinitionsByEnvironment,
                Dictionary<int, List<AbilityConfig>> abilityConfigsByCard,
                Dictionary<long, AbilityConfig> abilityConfigsById,
                Dictionary<string, BuffDefinition> buffDefinitions)
            {
                CardDefinitions = cardDefinitions;
                Cards = cards;
                CardDefinitionsByEnvironment = cardDefinitionsByEnvironment;
                AbilityConfigsByCard = abilityConfigsByCard;
                AbilityConfigsById = abilityConfigsById;
                BuffDefinitions = buffDefinitions;
            }

            public static CacheSnapshot Empty { get; } = new(
                new Dictionary<int, CardDefinition>(),
                Array.Empty<CardDefinition>(),
                new Dictionary<DeckEnvironment, IReadOnlyList<CardDefinition>>(),
                new Dictionary<int, List<AbilityConfig>>(),
                new Dictionary<long, AbilityConfig>(),
                new Dictionary<string, BuffDefinition>());

            public Dictionary<int, CardDefinition> CardDefinitions { get; }
            public IReadOnlyList<CardDefinition> Cards { get; }
            public IReadOnlyDictionary<DeckEnvironment, IReadOnlyList<CardDefinition>> CardDefinitionsByEnvironment { get; }
            public Dictionary<int, List<AbilityConfig>> AbilityConfigsByCard { get; }
            public Dictionary<long, AbilityConfig> AbilityConfigsById { get; }
            public Dictionary<string, BuffDefinition> BuffDefinitions { get; }
        }

        private CacheSnapshot _snapshot = CacheSnapshot.Empty;
        private Guid _dataRevision = Guid.Empty;

        // 静态实例，用于全局访问
        public static IGameDataProvider Instance { get; private set; } = null!;

        // 缓存状态
        private volatile bool _isInitialized = false;
        private readonly SemaphoreSlim _initializationLock = new(1, 1);
        private DateTime _lastInitialized = DateTime.MinValue;
        private TimeSpan _initializationDuration = TimeSpan.Zero;

        // 统计信息
        private int _cacheHitCount = 0;
        private int _cacheMissCount = 0;

        public bool IsInitialized => _isInitialized;
        public Guid DataRevision => _dataRevision;

        public GameDataProvider(
            IServiceScopeFactory scopeFactory,
            ILogger<GameDataProvider> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _logger.LogInformation("[GameDataProvider] Instance created. HashCode: {HashCode}", this.GetHashCode());
            // 设置静态实例
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                _logger.LogWarning("GameDataProvider singleton instance is being recreated. This might indicate an issue with the dependency injection container configuration. New instance hash: {NewHash}, Old instance hash: {OldHash}", this.GetHashCode(), Instance.GetHashCode());
                Instance = this;
            }
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _initializationLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                var startTime = DateTime.UtcNow;

                var snapshot = await BuildSnapshotAsync();
                Interlocked.Exchange(ref _snapshot, snapshot);

                _isInitialized = true;
                _dataRevision = Guid.NewGuid();
                _lastInitialized = DateTime.UtcNow;
                _initializationDuration = DateTime.UtcNow - startTime;

                _logger.LogInformation("GameDataProvider缓存初始化完成，耗时: {Duration}ms，" +
                    "卡牌定义: {CardCount}，能力配置: {AbilityCount}，Buff定义: {BuffCount}",
                    _initializationDuration.TotalMilliseconds,
                    snapshot.CardDefinitions.Count,
                    snapshot.AbilityConfigsById.Count,
                    snapshot.BuffDefinitions.Count);
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

        /// <summary>
        /// [TEST ONLY] Sets the singleton instance to a mock or specific implementation for testing purposes.
        /// This method should only be used in test projects.
        /// </summary>
        /// <param name="instance">The IGameDataProvider instance to set.</param>
        public static void SetInstanceForTesting(IGameDataProvider instance)
        {
            Instance = instance;
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

            var snapshot = Volatile.Read(ref _snapshot);
            if (snapshot.CardDefinitions.TryGetValue(cardId, out var cardDefinition))
            {
                Interlocked.Increment(ref _cacheHitCount);
                return cardDefinition;
            }

            Interlocked.Increment(ref _cacheMissCount);
            _logger.LogWarning("Cache miss for CardDefinition {CardId}. Current provider instance is {InstanceType}, Hash: {HashCode}",
                cardId,
                Instance?.GetType().FullName ?? "null",
                Instance?.GetHashCode());
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

            var snapshot = Volatile.Read(ref _snapshot);
            return snapshot.CardDefinitions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<CardDefinition> Cards => Volatile.Read(ref _snapshot).Cards;

        public IReadOnlyCollection<CardDefinition> GetCardDefinitionsForEnvironment(DeckEnvironment environment)
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("GameDataProvider未初始化，无法获取环境卡池 {Environment}", environment);
                return Array.Empty<CardDefinition>();
            }

            var snapshot = Volatile.Read(ref _snapshot);
            if (snapshot.CardDefinitionsByEnvironment.TryGetValue(environment, out var scopedCards))
            {
                return scopedCards;
            }

            return Array.Empty<CardDefinition>();
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

            var snapshot = Volatile.Read(ref _snapshot);
            if (snapshot.AbilityConfigsByCard.TryGetValue(cardId, out var configs))
            {
                Interlocked.Increment(ref _cacheHitCount);
                return new List<AbilityConfig>(configs); // 返回副本
            }

            Interlocked.Increment(ref _cacheMissCount);
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

            var snapshot = Volatile.Read(ref _snapshot);
            if (snapshot.AbilityConfigsById.TryGetValue(abilityId, out var config))
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

            var snapshot = Volatile.Read(ref _snapshot);
            return snapshot.AbilityConfigsByCard.ToDictionary(
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

            var snapshot = Volatile.Read(ref _snapshot);
            if (snapshot.BuffDefinitions.TryGetValue(buffId, out var buffDefinition))
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

            var startTime = DateTime.UtcNow;
            var snapshot = await BuildSnapshotAsync();
            Interlocked.Exchange(ref _snapshot, snapshot);
            _isInitialized = true;
            _dataRevision = Guid.NewGuid();
            _lastInitialized = DateTime.UtcNow;
            _initializationDuration = DateTime.UtcNow - startTime;

            _logger.LogInformation("GameDataProvider缓存刷新完成");
        }

        /// <inheritdoc />
        public void ClearCache()
        {
            Interlocked.Exchange(ref _snapshot, CacheSnapshot.Empty);
            _isInitialized = false;
            _dataRevision = Guid.Empty;

            _cacheHitCount = 0;
            _cacheMissCount = 0;

            _logger.LogDebug("GameDataProvider缓存已清除");
        }

        /// <inheritdoc />
        public CacheStatistics GetCacheStatistics()
        {
            var snapshot = Volatile.Read(ref _snapshot);
            return new CacheStatistics
            {
                CardDefinitionCount = snapshot.CardDefinitions.Count,
                AbilityConfigCount = snapshot.AbilityConfigsById.Count,
                BuffDefinitionCount = snapshot.BuffDefinitions.Count,
                LastInitialized = _lastInitialized,
                InitializationDuration = _initializationDuration,
                MemoryUsageBytes = EstimateMemoryUsage(),
                CacheHitCount = _cacheHitCount,
                CacheMissCount = _cacheMissCount
            };
        }

        private async Task<CacheSnapshot> BuildSnapshotAsync()
        {
            var cardDefinitions = await LoadCardDefinitionsAsync();
            var cards = cardDefinitions.Values.ToList();
            var cardsByEnvironment = BuildCardDefinitionsByEnvironment(cards);
            var abilityConfigs = await LoadAbilityConfigsAsync();
            var buffDefinitions = await LoadBuffDefinitionsAsync();

            return new CacheSnapshot(
                cardDefinitions,
                cards,
                cardsByEnvironment,
                abilityConfigs.ByCard,
                abilityConfigs.ById,
                buffDefinitions);
        }

        private static IReadOnlyDictionary<DeckEnvironment, IReadOnlyList<CardDefinition>> BuildCardDefinitionsByEnvironment(
            IReadOnlyCollection<CardDefinition> cardDefinitions)
        {
            var cardsByEnvironment = new Dictionary<DeckEnvironment, List<CardDefinition>>();

            foreach (var card in cardDefinitions)
            {
                var environments = card.CardSetDefinition?.Environments;
                if (environments == null || environments.Count == 0)
                {
                    continue;
                }

                foreach (var environment in environments.Distinct())
                {
                    if (!cardsByEnvironment.TryGetValue(environment, out var list))
                    {
                        list = new List<CardDefinition>();
                        cardsByEnvironment[environment] = list;
                    }

                    list.Add(card);
                }
            }

            return cardsByEnvironment.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<CardDefinition>)pair.Value);
        }

        private async Task<Dictionary<int, CardDefinition>> LoadCardDefinitionsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cardRepository = scope.ServiceProvider.GetRequiredService<ICardRepository>();

                _logger.LogInformation("开始加载卡牌定义...");
                var allCards = await cardRepository.GetAllCardDefinitionsAsync();

                var cardDefinitions = new Dictionary<int, CardDefinition>(allCards.Count);
                foreach (var card in allCards)
                {
                    cardDefinitions[card.Id] = card;
                }

                _logger.LogInformation("加载了 {Count} 个卡牌定义", allCards.Count);
                return cardDefinitions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载卡牌定义时发生错误");
                throw;
            }
        }

        private sealed record AbilityConfigSnapshot(
            Dictionary<int, List<AbilityConfig>> ByCard,
            Dictionary<long, AbilityConfig> ById);

        private async Task<AbilityConfigSnapshot> LoadAbilityConfigsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<AbilityLoader>>();
                // Resolve AbilityLoader from the scope to ensure its dependencies (like IAbilityFactory) are injected.
                // This is the correct way to use services within a singleton that depend on scoped services.
                var abilityLoader = scope.ServiceProvider.GetRequiredService<AbilityLoader>();

                var allAbilities = await abilityLoader.LoadAllAbilitiesAsync();

                var abilityConfigsByCard = new Dictionary<int, List<AbilityConfig>>();
                var abilityConfigsById = new Dictionary<long, AbilityConfig>();

                foreach (var ability in allAbilities)
                {
                    // 按卡牌ID索引
                    if (!abilityConfigsByCard.TryGetValue(ability.CardId, out var list))
                    {
                        list = new List<AbilityConfig>();
                        abilityConfigsByCard[ability.CardId] = list;
                    }
                    list.Add(ability);

                    // 按能力ID索引 - 修复类型转换问题
                    // ability.Id is already a long, no need to parse.
                    abilityConfigsById[ability.Id] = ability;
                }

                _logger.LogInformation("加载了 {Count} 个能力配置", allAbilities.Count);
                return new AbilityConfigSnapshot(abilityConfigsByCard, abilityConfigsById);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载能力配置时发生错误");
                throw;
            }
        }

        private async Task<Dictionary<string, BuffDefinition>> LoadBuffDefinitionsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                _logger.LogInformation("开始加载Buff定义...");
                var allBuffs = await dbContext.BuffDefinitions.AsNoTracking().ToListAsync();

                var buffDefinitions = new Dictionary<string, BuffDefinition>();
                foreach (var buff in allBuffs)
                {
                    buffDefinitions[buff.Id] = buff;
                }

                _logger.LogInformation("加载了 {Count} 个Buff定义", allBuffs.Count);
                return buffDefinitions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载Buff定义时发生错误");
                throw;
            }
        }

        private long EstimateMemoryUsage()
        {
            var snapshot = Volatile.Read(ref _snapshot);
            // 简单的内存使用估算
            long totalBytes = 0;

            // 估算卡牌定义内存使用
            totalBytes += snapshot.CardDefinitions.Count * 1024; // 假设每个卡牌定义约1KB

            // 估算能力配置内存使用
            totalBytes += snapshot.AbilityConfigsById.Count * 2048; // 假设每个能力配置约2KB

            // 估算Buff定义内存使用
            totalBytes += snapshot.BuffDefinitions.Count * 512; // 假设每个Buff定义约512B

            return totalBytes;
        }
    }
}
