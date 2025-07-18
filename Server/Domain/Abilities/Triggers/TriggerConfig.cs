using ShadowverseBeyond.Server.Domain.Abilities.Common;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums; // <-- Added using

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
        public List<EventType> EventTypes { get; set; } = new();

        // Removed Scope property (Scope checks should be handled by ConditionConfig)

        /// <summary>
        /// 触发时序
        /// </summary>
        public TriggerTiming Timing { get; set; } = TriggerTiming.After;


        /// <summary>
        /// 触发器生效的卡牌区域列表
        /// 为空或null时代表不需要进行区域检测（所有区域都有效）
        /// 用于性能优化，避免在非活跃区域处理不必要的触发器
        /// </summary>
        public List<CardZone> ValidZones { get; set; } = new() { CardZone.Field };

        /// <summary>
        /// 向后兼容的单一区域属性
        /// 设置时会清空ValidZones并添加单个区域
        /// 获取时返回ValidZones的第一个区域，如果为空则返回Field
        /// </summary>
        [Obsolete("Use ValidZones property instead for multi-zone support")]
        public CardZone CardZone
        {
            get => ValidZones?.FirstOrDefault() ?? CardZone.Field;
            set
            {
                ValidZones = new List<CardZone> { value };
            }
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 检查指定区域是否在有效区域列表中
        /// </summary>
        /// <param name="zone">要检查的区域</param>
        /// <returns>如果区域有效或不需要区域检测则返回true</returns>
        public bool IsValidZone(CardZone zone)
        {
            // 如果ValidZones为空或null，表示不需要区域检测，所有区域都有效
            if (ValidZones == null || ValidZones.Count == 0)
                return true;

            return ValidZones.Contains(zone);
        }

        /// <summary>
        /// 设置单个有效区域
        /// </summary>
        /// <param name="zone">要设置的区域</param>
        /// <returns>当前TriggerConfig实例，支持链式调用</returns>
        public TriggerConfig WithZone(CardZone zone)
        {
            ValidZones = new List<CardZone> { zone };
            return this;
        }

        /// <summary>
        /// 设置多个有效区域
        /// </summary>
        /// <param name="zones">要设置的区域列表</param>
        /// <returns>当前TriggerConfig实例，支持链式调用</returns>
        public TriggerConfig WithZones(params CardZone[] zones)
        {
            ValidZones = zones?.ToList() ?? new List<CardZone>();
            return this;
        }

        /// <summary>
        /// 设置为所有区域都有效（不进行区域检测）
        /// </summary>
        /// <returns>当前TriggerConfig实例，支持链式调用</returns>
        public TriggerConfig WithAllZones()
        {
            ValidZones = new List<CardZone>();
            return this;
        }

        // Removed Parameters property (Use specific properties or dedicated config objects if needed)

        /// <summary>
        /// 创建简单事件触发器
        /// </summary>
        /// <param name="eventType">游戏事件类型</param>
        /// <returns>触发器配置</returns>
        public static TriggerConfig OnEvent(EventType eventType) // <-- Removed scope parameter
        {
            return new TriggerConfig
            {
                EventTypes = new List<EventType> { eventType },
                // Scope removed, handle via Conditions if needed
                Timing = TriggerTiming.After,
                ValidZones = new List<CardZone> { CardZone.Field } // Default to Field zone
            };
        }

        /// <summary>
        /// 创建多事件触发器
        /// </summary>
        /// <param name="eventTypes">游戏事件类型列表</param>
        /// <returns>触发器配置</returns>
        public static TriggerConfig OnEvents(List<EventType> eventTypes) // <-- Removed scope parameter
        {
            return new TriggerConfig
            {
                EventTypes = eventTypes,
                // Scope removed, handle via Conditions if needed
                Timing = TriggerTiming.After,
                ValidZones = new List<CardZone> { CardZone.Field } // Default to Field zone
            };
        }

        /// <summary>
        /// 创建指定区域的事件触发器
        /// </summary>
        /// <param name="eventType">游戏事件类型</param>
        /// <param name="zone">有效区域</param>
        /// <returns>触发器配置</returns>
        public static TriggerConfig OnEventInZone(EventType eventType, CardZone zone)
        {
            return new TriggerConfig
            {
                EventTypes = new List<EventType> { eventType },
                Timing = TriggerTiming.After,
                ValidZones = new List<CardZone> { zone }
            };
        }

        /// <summary>
        /// 创建多区域的事件触发器
        /// </summary>
        /// <param name="eventType">游戏事件类型</param>
        /// <param name="zones">有效区域列表</param>
        /// <returns>触发器配置</returns>
        public static TriggerConfig OnEventInZones(EventType eventType, params CardZone[] zones)
        {
            return new TriggerConfig
            {
                EventTypes = new List<EventType> { eventType },
                Timing = TriggerTiming.After,
                ValidZones = zones?.ToList() ?? new List<CardZone>()
            };
        }

        /// <summary>
        /// 创建所有区域都有效的事件触发器（不进行区域检测）
        /// </summary>
        /// <param name="eventType">游戏事件类型</param>
        /// <returns>触发器配置</returns>
        public static TriggerConfig OnEventInAllZones(EventType eventType)
        {
            return new TriggerConfig
            {
                EventTypes = new List<EventType> { eventType },
                Timing = TriggerTiming.After,
                ValidZones = new List<CardZone>() // Empty list means all zones are valid
            };
        }

        // Removed OnCondition static helper method as Conditions are no longer part of TriggerConfig
    }

    // Removed TriggerLimitConfig class and LimitScope enum (Limits handled by AbilityConfig.Conditions)

    // Removed TriggerDelayConfig class and DelayType enum.
}
