using ShadowverseBeyond.Server.Domain.Abilities.Common;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // <-- Added using

namespace ShadowverseBeyond.Server.Domain.Abilities.Triggers
{
    /// <summary>
    /// 触发器配置类
    /// 定义能力的触发时机
    /// </summary>
    public class TriggerConfig
    {
        /// <summary>
        /// 触发器唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 触发器名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 监听的游戏事件类型列表
        /// </summary>
        public List<GameEventType> EventTypes { get; set; } = new();

        // Removed Scope property (Scope checks should be handled by ConditionConfig)

        /// <summary>
        /// 触发时序
        /// </summary>
        public TriggerTiming Timing { get; set; } = TriggerTiming.After;

        // Removed IsOptional property (Consider moving to AbilityConfig or EffectConfig if needed)

        // Removed RequiresTarget property (Should be handled by EffectConfig or AbilityConfig)

        // Removed Tags property (Can be reintroduced if complex matching is needed later)

        // Removed ConditionExpression property

        // Removed Conditions property (Conditions are now held directly by AbilityConfig)

        // Removed Limit property and TriggerLimitConfig (All limits should be handled by AbilityConfig.Conditions)

        // Removed Delay property as delay logic will be handled at the effect level.

        // Removed Priority property (Runtime system will handle order if needed, or reintroduce later)

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        // Removed Parameters property (Use specific properties or dedicated config objects if needed)

        /// <summary>
        /// 创建简单事件触发器
        /// </summary>
        /// <param name="eventType">游戏事件类型</param>
        /// <returns>触发器配置</returns>
        public static TriggerConfig OnEvent(GameEventType eventType) // <-- Removed scope parameter
        {
            return new TriggerConfig
            {
                EventTypes = new List<GameEventType> { eventType },
                // Scope removed, handle via Conditions if needed
                Timing = TriggerTiming.After
            };
        }

        /// <summary>
        /// 创建多事件触发器
        /// </summary>
        /// <param name="eventTypes">游戏事件类型列表</param>
        /// <returns>触发器配置</returns>
        public static TriggerConfig OnEvents(List<GameEventType> eventTypes) // <-- Removed scope parameter
        {
            return new TriggerConfig
            {
                EventTypes = eventTypes,
                // Scope removed, handle via Conditions if needed
                Timing = TriggerTiming.After
            };
        }

        // Removed OnCondition static helper method as Conditions are no longer part of TriggerConfig
    }

    // Removed TriggerLimitConfig class and LimitScope enum (Limits handled by AbilityConfig.Conditions)

    // Removed TriggerDelayConfig class and DelayType enum.
}
