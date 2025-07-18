using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    /// <summary>
    /// 效果队列服务接口
    /// 提供游戏级别的效果队列管理功能
    /// </summary>
    public interface IEffectQueueService
    {
        /// <summary>
        /// 【架构重构】获取并移除指定游戏的下一个效果请求
        /// </summary>
        /// <param name="gameId">游戏ID</param>
        /// <returns>下一个效果请求，如果队列为空则返回null</returns>
        Effect? GetNextEffectRequest(string gameId);

        /// <summary>
        /// 检查指定游戏是否有待处理的效果
        /// </summary>
        /// <param name="gameId">游戏ID</param>
        /// <returns>是否有待处理的效果</returns>
        bool HasPendingEffects(string gameId);

        /// <summary>
        /// 获取指定游戏的待处理效果数量
        /// </summary>
        /// <param name="gameId">游戏ID</param>
        /// <returns>待处理效果数量</returns>
        int GetPendingEffectCount(string gameId);

        /// <summary>
        /// 清空指定游戏的效果队列
        /// </summary>
        /// <param name="gameId">游戏ID</param>
        void ClearGameEffectQueue(string gameId);

        /// <summary>
        /// 【架构重构】将任意效果请求加入指定游戏的队列
        /// 用于支持EffectHandler返回的EffectRequests入队
        /// </summary>
        /// <param name="gameId">游戏ID</param>
        /// <param name="effectRequest">效果请求</param>
        /// <returns>异步任务</returns>
        Task EnqueueEffectRequestAsync(string gameId, Effect effectRequest);
    }
}
