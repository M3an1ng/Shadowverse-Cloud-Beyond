using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// 能力触发服务接口，用于解耦EventProcessor和AbilityManager之间的循环依赖
    /// </summary>
    public interface IAbilityTriggerService
    {
        /// <summary>
        /// 获取指定事件触发的能力列表
        /// </summary>
        /// <param name="gameEvent">触发的游戏事件</param>
        /// <param name="context">游戏上下文</param>
        /// <returns>触发的能力列表</returns>
        Task<List<Ability>> GetTriggeredAbilitiesAsync(IGameEvent gameEvent, GameContext context);
    }
}
