using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;

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
        Task<List<IAbility>> GetTriggeredAbilitiesAsync(IGameEvent gameEvent, GameContext context);

        /// <summary>
        /// Directly processes a single ability instance to generate its effects,
        /// including condition checks and target resolution. This centralizes the ability execution logic.
        /// </summary>
        /// <param name="ability">The ability instance to process.</param>
        /// <param name="context">The current game context.</param>
        /// <returns>A list of effects to be enqueued.</returns>
        Task<List<Effect>> ProcessSingleAbilityAsync(IAbility ability, GameContext context);
    }
}
