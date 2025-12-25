using Microsoft.AspNetCore.Mvc;
using ShadowverseBeyond.Server.Services;
using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities;

namespace ShadowverseBeyond.Server.Controllers
{
    /// <summary>
    /// 诊断和性能监控控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly ILogger<DiagnosticsController> _logger;
        public DiagnosticsController(ILogger<DiagnosticsController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 获取所有运行时能力配置
        /// </summary>
        /// <returns>所有运行时能力配置的字典</returns>
        [HttpGet("abilities")]
        public ActionResult<IReadOnlyDictionary<int, List<AbilityConfig>>> GetAllAbilities()
        {
            try
            {
                var abilities = GameDataProvider.Instance.GetAllAbilityConfigs();
                return Ok(abilities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all ability configs");
                return StatusCode(500, new { error = "Failed to retrieve ability configs" });
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        /// <returns>缓存统计信息</returns>
        [HttpGet("cache-stats")]
        public ActionResult<object> GetCacheStatistics()
        {
            try
            {
                var stats = GameDataProvider.Instance.GetCacheStatistics();

                var result = new
                {
                    IsInitialized = GameDataProvider.Instance.IsInitialized,
                    CardDefinitionCount = stats.CardDefinitionCount,
                    AbilityConfigCount = stats.AbilityConfigCount,
                    BuffDefinitionCount = stats.BuffDefinitionCount,
                    LastInitialized = stats.LastInitialized,
                    InitializationDuration = stats.InitializationDuration,
                    MemoryUsageBytes = stats.MemoryUsageBytes,
                    MemoryUsageMB = Math.Round(stats.MemoryUsageBytes / 1024.0 / 1024.0, 2),
                    CacheHitCount = stats.CacheHitCount,
                    CacheMissCount = stats.CacheMissCount,
                    CacheHitRatio = Math.Round(stats.CacheHitRatio * 100, 2),
                    Performance = new
                    {
                        Status = GameDataProvider.Instance.IsInitialized ? "Optimized" : "Not Initialized",
                        DatabaseQueriesEliminated = "All runtime card definition and ability config queries",
                        ExpectedPerformanceGain = "Significant reduction in I/O latency"
                    }
                };

                _logger.LogInformation("Cache statistics requested. Hit ratio: {HitRatio}%, Total requests: {Total}",
                    result.CacheHitRatio, stats.CacheHitCount + stats.CacheMissCount);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cache statistics");
                return StatusCode(500, new { error = "Failed to retrieve cache statistics" });
            }
        }

        /// <summary>
        /// 刷新缓存
        /// </summary>
        /// <returns>刷新结果</returns>
        [HttpPost("refresh-cache")]
        public async Task<ActionResult<object>> RefreshCache()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                await GameDataProvider.Instance.RefreshCacheAsync();
                var duration = DateTime.UtcNow - startTime;

                var stats = GameDataProvider.Instance.GetCacheStatistics();

                var result = new
                {
                    Success = true,
                    RefreshDuration = duration,
                    NewStats = new
                    {
                        CardDefinitionCount = stats.CardDefinitionCount,
                        AbilityConfigCount = stats.AbilityConfigCount,
                        BuffDefinitionCount = stats.BuffDefinitionCount,
                        LastInitialized = stats.LastInitialized
                    }
                };

                _logger.LogInformation("Cache refreshed successfully in {Duration}ms", duration.TotalMilliseconds);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing cache");
                return StatusCode(500, new { error = "Failed to refresh cache" });
            }
        }

        /// <summary>
        /// 获取数据加载优化状态
        /// </summary>
        /// <returns>优化状态信息</returns>
        [HttpGet("optimization-status")]
        public ActionResult<object> GetOptimizationStatus()
        {
            try
            {
                var stats = GameDataProvider.Instance.GetCacheStatistics();

                var result = new
                {
                    OptimizationEnabled = GameDataProvider.Instance.IsInitialized,
                    OptimizationDetails = new
                    {
                        DatabaseQueriesEliminated = new[]
                        {
                            "CardDefinition loading in GameService.CreateGameAsync",
                            "CardDefinition loading in GameService.CheckPlayabilityAsync",
                            "CardDefinition loading in GameService.FetchCardDataAsync",
                            "AbilityConfig loading in GameService.ProcessCardPlayAbilitiesAsync",
                            "AbilityConfig loading in GameService.CheckPlayabilityAsync",
                            "AbilityLoader database access replaced with cache access"
                        },
                        PerformanceImprovements = new[]
                        {
                            "All static game data access is now synchronous memory operations",
                            "Eliminated database I/O latency during gameplay",
                            "Reduced complexity of scoped dependency management",
                            "Unified data access through GameDataProvider"
                        },
                        CacheStatistics = new
                        {
                            TotalDataCached = stats.CardDefinitionCount + stats.AbilityConfigCount,
                            CacheHitRatio = $"{Math.Round(stats.CacheHitRatio * 100, 2)}%",
                            MemoryUsage = $"{Math.Round(stats.MemoryUsageBytes / 1024.0 / 1024.0, 2)} MB"
                        }
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving optimization status");
                return StatusCode(500, new { error = "Failed to retrieve optimization status" });
            }
        }
    }
}
