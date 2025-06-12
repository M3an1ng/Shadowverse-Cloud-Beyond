using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// 游戏数据预加载服务，在应用启动时初始化GameDataProvider
    /// 实现IHostedService接口，确保在应用启动时自动执行预加载
    /// </summary>
    public class GameDataPreloadService : IHostedService
    {
        private readonly IGameDataProvider _gameDataProvider;
        private readonly ILogger<GameDataPreloadService> _logger;

        public GameDataPreloadService(
            IGameDataProvider gameDataProvider,
            ILogger<GameDataPreloadService> logger)
        {
            _gameDataProvider = gameDataProvider;
            _logger = logger;
        }

        /// <summary>
        /// 启动服务时执行数据预加载
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动任务</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("GameDataPreloadService 开始启动...");

            try
            {
                var startTime = DateTime.UtcNow;
                
                // 执行数据预加载
                await _gameDataProvider.InitializeAsync();
                
                var duration = DateTime.UtcNow - startTime;
                var statistics = _gameDataProvider.GetCacheStatistics();
                
                _logger.LogInformation(
                    "GameDataPreloadService 启动完成，耗时: {Duration}ms\n" +
                    "预加载统计:\n" +
                    "  - 卡牌定义: {CardCount} 个\n" +
                    "  - 能力配置: {AbilityCount} 个\n" +
                    "  - Buff定义: {BuffCount} 个\n" +
                    "  - 内存使用: {MemoryMB:F2} MB",
                    duration.TotalMilliseconds,
                    statistics.CardDefinitionCount,
                    statistics.AbilityConfigCount,
                    statistics.BuffDefinitionCount,
                    statistics.MemoryUsageBytes / 1024.0 / 1024.0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GameDataPreloadService 启动失败");
                throw; // 重新抛出异常，阻止应用启动
            }
        }

        /// <summary>
        /// 停止服务时的清理操作
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>停止任务</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("GameDataPreloadService 正在停止...");
            
            // 可以在这里执行清理操作，如保存统计信息等
            var statistics = _gameDataProvider.GetCacheStatistics();
            
            _logger.LogInformation(
                "GameDataPreloadService 停止完成\n" +
                "运行期间统计:\n" +
                "  - 缓存命中: {HitCount} 次\n" +
                "  - 缓存未命中: {MissCount} 次\n" +
                "  - 命中率: {HitRatio:P2}",
                statistics.CacheHitCount,
                statistics.CacheMissCount,
                statistics.CacheHitRatio);
            
            return Task.CompletedTask;
        }
    }
}
