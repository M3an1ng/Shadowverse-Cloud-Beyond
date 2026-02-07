using ShadowverseBeyond.Server.Domain.Events; // For IGameEvent
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Events.Services
{
    public interface IEventHistoryService
    {
        /// <summary>
        /// 异步存储一个游戏事件。
        /// </summary>
        /// <param name="gameId">游戏ID。</param>
        /// <param name="gameEvent">要存储的游戏事件。</param>
        Task StoreEventAsync(string gameId, IGameEvent gameEvent);

        /// <summary>
        /// 同步获取指定游戏的事件历史列表（按存储顺序）。
        /// </summary>
        /// <param name="gameId">游戏ID。</param>
        /// <returns>该局游戏的事件列表。</returns>
        IReadOnlyList<IGameEvent> GetEvents(string gameId);

        /// <summary>
        /// 根据事件ID异步检索一个游戏事件。
        /// </summary>
        /// <param name="gameId">游戏ID。</param>
        /// <param name="eventId">要检索的事件ID (Guid)。</param>
        /// <returns>找到的IGameEvent，如果未找到则返回null。</returns>
        Task<IGameEvent?> GetEventByIdAsync(string gameId, Guid eventId);

        /// <summary>
        /// (可选，但推荐) 清除指定游戏的所有事件历史。
        /// 应在游戏结束并清理GameState时调用。
        /// </summary>
        /// <param name="gameId">要清除历史的游戏ID。</param>
        Task ClearHistoryForGameAsync(string gameId);
    }
}
