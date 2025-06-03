// Path: Server/Services/Abstractions/IClientMethodMapper.cs
using ShadowverseBeyond.Server.Dtos.SignalR; // 修正此 using 指令以识别 GameEventDto
using ShadowverseBeyond.Server.Hubs;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public interface IClientMethodMapper
    {
        /// <summary>
        /// 根据事件类型将事件广播给指定的连接ID。
        /// </summary>
        /// <param name="connectionId">目标客户端连接ID。</param>
        /// <param name="eventDto">要广播的事件数据传输对象 (基类)。</param>
        /// <returns>表示异步操作的任务。</returns>
        Task BroadcastEventToClientAsync(string connectionId, GameEventDto eventDto);

        /// <summary>
        /// 根据事件类型将事件广播给指定的组。
        /// </summary>
        /// <param name="groupName">目标组名。</param>
        /// <param name="eventDto">要广播的事件数据传输对象 (基类)。</param>
        /// <returns>表示异步操作的任务。</returns>
        Task BroadcastEventToGroupAsync(string groupName, GameEventDto eventDto);

        /// <summary>
        /// 根据事件类型将事件广播给除指定连接ID之外的所有组成员。
        /// </summary>
        /// <param name="groupName">目标组名。</param>
        /// <param name="connectionIdToExclude">要排除的连接ID。</param>
        /// <param name="eventDto">要广播的事件数据传输对象 (基类)。</param>
        /// <returns>表示异步操作的任务。</returns>
        Task BroadcastEventToGroupExceptAsync(string groupName, string connectionIdToExclude, GameEventDto eventDto);

        /// <summary>
        /// 根据事件类型将事件广播给组内的所有连接。
        /// </summary>
        /// <param name="groupName">目标组名。</param>
        /// <param name="eventDto">要广播的事件数据传输对象 (基类)。</param>
        /// <returns>表示异步操作的任务。</returns>
        Task BroadcastEventToGroupAllAsync(string groupName, GameEventDto eventDto);
    }
}