using System;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue
{
    /// <summary>
    /// 效果队列接口，用于管理游戏中的效果执行顺序
    /// </summary>
    public interface IEffectQueue
    {
        /// <summary>
        /// 将效果请求加入队列
        /// </summary>
        /// <param name="request">效果请求</param>
        /// <returns>异步任务</returns>
        Task EnqueueEffectAsync(Effect request);

        /// <summary>
        /// 检查是否有待处理的效果
        /// </summary>
        bool HasPendingEffects { get; }

        /// <summary>
        /// 获取队列中待处理效果的数量
        /// </summary>
        int PendingEffectCount { get; }

        /// <summary>
        /// 清空队列中的所有效果
        /// </summary>
        void ClearQueue();

        /// <summary>
        /// 【架构重构】获取并移除队列中的下一个效果请求
        /// </summary>
        /// <returns>下一个效果请求，如果队列为空则返回null</returns>
        Effect? GetNextRequest();

        /// <summary>
        /// 【新增】查看队列中的下一个效果请求，但不移除它
        /// </summary>
        /// <returns>下一个效果请求，如果队列为空则返回null</returns>
        Effect? Peek();
    }
}
