using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    /// <summary>
    /// 值源解析器接口
    /// 根据M12设计文档实现，负责解析效果配置中的动态值
    /// </summary>
    public interface IValueSourceResolver
    {
        /// <summary>
        /// 解析值源
        /// </summary>
        /// <param name="valueConfig">值配置</param>
        /// <param name="context">统一游戏上下文</param>
        /// <returns>解析后的值</returns>
        Task<object> ResolveAsync(ValueConfig valueConfig, GameContext context);

        /// <summary>
        /// 解析为整数
        /// </summary>
        /// <param name="valueConfig">值配置</param>
        /// <param name="context">统一游戏上下文</param>
        /// <returns>整数值</returns>
        Task<int> ResolveAsIntAsync(ValueConfig valueConfig, GameContext context);

        /// <summary>
        /// 解析为字符串
        /// </summary>
        /// <param name="valueConfig">值配置</param>
        /// <param name="context">统一游戏上下文</param>
        /// <returns>字符串值</returns>
        Task<string> ResolveAsStringAsync(ValueConfig valueConfig, GameContext context);

        /// <summary>
        /// 解析为布尔值
        /// </summary>
        /// <param name="valueConfig">值配置</param>
        /// <param name="context">统一游戏上下文</param>
        /// <returns>布尔值</returns>
        Task<bool> ResolveAsBoolAsync(ValueConfig valueConfig, GameContext context);
    }



    // 注意：值源类型使用现有的ValueType枚举
    // 扩展的值源类型可以通过DynamicSource字符串来区分，如：
    // - "source.attack" - 来源卡牌属性
    // - "target.health" - 目标卡牌属性
    // - "player.pp" - 玩家属性
    // - "game.turn" - 游戏状态属性
    // - "event.damage" - 事件属性
}
