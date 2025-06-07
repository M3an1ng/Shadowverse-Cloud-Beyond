using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
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
        /// <param name="context">解析上下文</param>
        /// <returns>解析后的值</returns>
        Task<object> ResolveAsync(ValueConfig valueConfig, ValueResolutionContext context);

        /// <summary>
        /// 解析为整数
        /// </summary>
        /// <param name="valueConfig">值配置</param>
        /// <param name="context">解析上下文</param>
        /// <returns>整数值</returns>
        Task<int> ResolveAsIntAsync(ValueConfig valueConfig, ValueResolutionContext context);

        /// <summary>
        /// 解析为字符串
        /// </summary>
        /// <param name="valueConfig">值配置</param>
        /// <param name="context">解析上下文</param>
        /// <returns>字符串值</returns>
        Task<string> ResolveAsStringAsync(ValueConfig valueConfig, ValueResolutionContext context);

        /// <summary>
        /// 解析为布尔值
        /// </summary>
        /// <param name="valueConfig">值配置</param>
        /// <param name="context">解析上下文</param>
        /// <returns>布尔值</returns>
        Task<bool> ResolveAsBoolAsync(ValueConfig valueConfig, ValueResolutionContext context);
    }

    /// <summary>
    /// 值解析上下文
    /// 提供值解析时所需的上下文信息
    /// </summary>
    public class ValueResolutionContext
    {
        /// <summary>
        /// 当前游戏状态
        /// </summary>
        public GameState CurrentGameState { get; set; } = null!;

        /// <summary>
        /// 来源卡牌
        /// </summary>
        public CardInstance SourceCard { get; set; } = null!;

        /// <summary>
        /// 来源玩家ID
        /// </summary>
        public string SourcePlayerId { get; set; } = string.Empty;

        /// <summary>
        /// 目标卡牌（如果有）
        /// </summary>
        public CardInstance? TargetCard { get; set; }

        /// <summary>
        /// 能力变量
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>(); // Use Dictionary instead of removed AbilityVariables

        /// <summary>
        /// 触发事件（如果有）
        /// </summary>
        public object? TriggeringEvent { get; set; }

        /// <summary>
        /// 额外参数
        /// </summary>
        public Dictionary<string, object> AdditionalParams { get; set; } = new();
    }

    // 注意：值源类型使用现有的ValueType枚举
    // 扩展的值源类型可以通过DynamicSource字符串来区分，如：
    // - "source.attack" - 来源卡牌属性
    // - "target.health" - 目标卡牌属性
    // - "player.pp" - 玩家属性
    // - "game.turn" - 游戏状态属性
    // - "event.damage" - 事件属性
}
