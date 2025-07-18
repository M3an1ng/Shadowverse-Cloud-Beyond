using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue
{
    /// <summary>
    /// 效果队列的具体实现。
    /// 【架构重构】职责简化为一个线程安全的、支持优先级的队列封装器。
    /// 它不再负责执行效果。
    /// </summary>
    public class EffectQueue : IEffectQueue
    {
        private readonly PriorityQueue<Effect, int> _effectQueue;
        private readonly ILogger<EffectQueue> _logger;
        private readonly object _queueLock = new object();

        public EffectQueue(ILogger<EffectQueue> logger)
        {
            _effectQueue = new PriorityQueue<Effect, int>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 检查是否有待处理的效果
        /// </summary>
        public bool HasPendingEffects
        {
            get
            {
                lock (_queueLock)
                {
                    return _effectQueue.Count > 0;
                }
            }
        }

        /// <summary>
        /// 获取队列中待处理效果的数量
        /// </summary>
        public int PendingEffectCount
        {
            get
            {
                lock (_queueLock)
                {
                    return _effectQueue.Count;
                }
            }
        }

        /// <summary>
        /// 将效果请求加入队列
        /// </summary>
        public Task EnqueueEffectAsync(Effect request)
        {
            if (request == null)
            {
                _logger.LogWarning("Attempted to enqueue null EffectRequest");
                return Task.CompletedTask;
            }

            lock (_queueLock)
            {
                _effectQueue.Enqueue(request, request.Priority);
                _logger.LogDebug("Enqueued effect request: {EffectType} for player {PlayerId} with Priority {Priority} and ID {RequestId}",
                    request.EffectConfig?.Type ?? "unknown", request.RequestingPlayerId, request.Priority, request.Id);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 清空队列中的所有效果
        /// </summary>
        public void ClearQueue()
        {
            var clearedCount = 0;
            lock (_queueLock)
            {
                clearedCount = _effectQueue.Count;
                _effectQueue.Clear();
            }

            if (clearedCount > 0)
            {
                _logger.LogInformation("Cleared {ClearedCount} effects from the effect queue", clearedCount);
            }
        }

        /// <summary>
        /// 【架构重构】获取并移除队列中的下一个效果请求
        /// </summary>
        public Effect? GetNextRequest()
        {
            lock (_queueLock)
            {
                if (_effectQueue.TryDequeue(out var effectRequest, out var priority))
                {
                    _logger.LogDebug("Dequeued effect request: {EffectType} with Priority {Priority} for player {PlayerId}",
                        effectRequest.EffectConfig?.Type ?? "unknown", priority, effectRequest.RequestingPlayerId);
                    return effectRequest;
                }
            }
            return null;
        }

        /// <summary>
        /// 【新增】查看队列中的下一个效果请求，但不移除它
        /// </summary>
        public Effect? Peek()
        {
            lock (_queueLock)
            {
                if (_effectQueue.TryPeek(out var effectRequest, out _))
                {
                    return effectRequest;
                }
            }
            return null;
        }
    }
}
