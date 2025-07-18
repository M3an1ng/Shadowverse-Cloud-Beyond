using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// 效果队列服务的具体实现
    /// 管理每个游戏的效果队列
    /// </summary>
    public class EffectQueueService : IEffectQueueService
    {
        private readonly ConcurrentDictionary<string, IEffectQueue> _gameEffectQueues;
        private readonly ILogger<EffectQueueService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public EffectQueueService(ILogger<EffectQueueService> logger, IServiceProvider serviceProvider)
        {
            _gameEffectQueues = new ConcurrentDictionary<string, IEffectQueue>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// 获取或创建指定游戏的效果队列
        /// </summary>
        private IEffectQueue GetOrCreateEffectQueue(string gameId)
        {
            return _gameEffectQueues.GetOrAdd(gameId, _ =>
            {
                _logger.LogDebug("Creating new effect queue for game {GameId}", gameId);

                // 从DI容器创建EffectQueue实例
                // 注意：EffectQueue本身不再需要依赖，因为它只是一个简单的队列包装器
                var logger = _serviceProvider.GetRequiredService<ILogger<ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue.EffectQueue>>();
                return new ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue.EffectQueue(logger);
            });
        }

        /// <summary>
        /// 【架构重构】将任意效果请求加入指定游戏的队列
        /// </summary>
        public async Task EnqueueEffectRequestAsync(string gameId, Effect effectRequest)
        {
            if (effectRequest == null)
            {
                _logger.LogWarning("Attempted to enqueue null EffectRequest for game {GameId}", gameId);
                return;
            }

            _logger.LogDebug("Enqueuing effect request {EffectType} for player {PlayerId} in game {GameId}",
                effectRequest.EffectConfig?.Type ?? "unknown", effectRequest.RequestingPlayerId, gameId);

            var effectQueue = GetOrCreateEffectQueue(gameId);
            await effectQueue.EnqueueEffectAsync(effectRequest);
        }

        /// <summary>
        /// 【架构重构】获取并移除指定游戏的下一个效果请求
        /// </summary>
        public Effect? GetNextEffectRequest(string gameId)
        {
            if (_gameEffectQueues.TryGetValue(gameId, out var effectQueue))
            {
                var request = effectQueue.GetNextRequest();
                if (request != null)
                {
                    _logger.LogDebug("Retrieved effect request {EffectType} for player {PlayerId} from game {GameId}",
                        request.EffectConfig?.Type ?? "unknown", request.RequestingPlayerId, gameId);
                }
                return request;
            }
            return null;
        }

        /// <summary>
        /// 检查指定游戏是否有待处理的效果
        /// </summary>
        public bool HasPendingEffects(string gameId)
        {
            if (_gameEffectQueues.TryGetValue(gameId, out var effectQueue))
            {
                return effectQueue.HasPendingEffects;
            }
            return false;
        }

        /// <summary>
        /// 获取指定游戏的待处理效果数量
        /// </summary>
        public int GetPendingEffectCount(string gameId)
        {
            if (_gameEffectQueues.TryGetValue(gameId, out var effectQueue))
            {
                return effectQueue.PendingEffectCount;
            }
            return 0;
        }

        /// <summary>
        /// 清空指定游戏的效果队列
        /// </summary>
        public void ClearGameEffectQueue(string gameId)
        {
            if (_gameEffectQueues.TryGetValue(gameId, out var effectQueue))
            {
                _logger.LogInformation("Clearing effect queue for game {GameId}", gameId);
                effectQueue.ClearQueue();
            }
        }
    }
}
