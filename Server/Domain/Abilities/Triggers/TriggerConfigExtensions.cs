using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Abilities.Triggers
{
    /// <summary>
    /// TriggerConfig的扩展方法，提供便捷的区域操作
    /// </summary>
    public static class TriggerConfigExtensions
    {
        /// <summary>
        /// 检查触发器是否应该在指定卡牌的当前区域中激活
        /// </summary>
        /// <param name="config">触发器配置</param>
        /// <param name="card">要检查的卡牌</param>
        /// <returns>如果应该激活返回true</returns>
        public static bool ShouldActivateForCard(this TriggerConfig config, CardInstance card)
        {
            if (card == null) return false;
            return config.IsValidZone(card.CurrentZone);
        }

        /// <summary>
        /// 获取触发器的有效区域数量
        /// </summary>
        /// <param name="config">触发器配置</param>
        /// <returns>有效区域数量，0表示所有区域都有效</returns>
        public static int GetValidZoneCount(this TriggerConfig config)
        {
            return config.ValidZones?.Count ?? 0;
        }

        /// <summary>
        /// 检查触发器是否限制在单一区域
        /// </summary>
        /// <param name="config">触发器配置</param>
        /// <returns>如果限制在单一区域返回true</returns>
        public static bool IsSingleZoneRestricted(this TriggerConfig config)
        {
            return config.ValidZones?.Count == 1;
        }

        /// <summary>
        /// 检查触发器是否允许所有区域
        /// </summary>
        /// <param name="config">触发器配置</param>
        /// <returns>如果允许所有区域返回true</returns>
        public static bool AllowsAllZones(this TriggerConfig config)
        {
            return config.ValidZones == null || config.ValidZones.Count == 0;
        }

        /// <summary>
        /// 获取触发器的主要区域（第一个有效区域）
        /// </summary>
        /// <param name="config">触发器配置</param>
        /// <returns>主要区域，如果没有限制则返回Field</returns>
        public static CardZone GetPrimaryZone(this TriggerConfig config)
        {
            return config.ValidZones?.FirstOrDefault() ?? CardZone.Field;
        }

        /// <summary>
        /// 检查触发器是否包含指定区域
        /// </summary>
        /// <param name="config">触发器配置</param>
        /// <param name="zone">要检查的区域</param>
        /// <returns>如果包含指定区域返回true</returns>
        public static bool ContainsZone(this TriggerConfig config, CardZone zone)
        {
            return config.IsValidZone(zone);
        }

        /// <summary>
        /// 检查触发器是否只针对活跃区域
        /// </summary>
        /// <param name="config">触发器配置</param>
        /// <returns>如果只针对活跃区域返回true</returns>
        public static bool IsActiveZoneOnly(this TriggerConfig config)
        {
            if (config.AllowsAllZones()) return false;

            return config.ValidZones!.All(zone => ZoneFilteringHelper.IsActiveZone(zone));
        }

        /// <summary>
        /// 检查触发器是否包含非活跃区域
        /// </summary>
        /// <param name="config">触发器配置</param>
        /// <returns>如果包含非活跃区域返回true</returns>
        public static bool IncludesInactiveZones(this TriggerConfig config)
        {
            if (config.AllowsAllZones()) return true;

            return config.ValidZones!.Any(zone => ZoneFilteringHelper.IsInactiveZone(zone));
        }

        /// <summary>
        /// 获取触发器的区域优先级（基于最高优先级的区域）
        /// </summary>
        /// <param name="config">触发器配置</param>
        /// <returns>优先级数值，越小优先级越高</returns>
        public static int GetZonePriority(this TriggerConfig config)
        {
            if (config.AllowsAllZones()) return 1; // 所有区域都有效，给予最高优先级

            return config.ValidZones!.Min(zone => ZoneFilteringHelper.GetZonePriority(zone));
        }

        /// <summary>
        /// 创建针对活跃区域优化的触发器配置副本
        /// </summary>
        /// <param name="config">原始触发器配置</param>
        /// <returns>优化后的触发器配置</returns>
        public static TriggerConfig CreateActiveZoneOptimized(this TriggerConfig config)
        {
            var optimizedConfig = new TriggerConfig
            {
                Id = config.Id,
                EventTypes = new List<EventType>(config.EventTypes),
                Timing = config.Timing,
                IsEnabled = config.IsEnabled
            };

            // 如果原配置允许所有区域，则限制为活跃区域
            if (config.AllowsAllZones())
            {
                optimizedConfig.ValidZones = ZoneFilteringHelper.ActiveZones.ToList();
            }
            else
            {
                // 只保留活跃区域
                optimizedConfig.ValidZones = config.ValidZones!
                    .Where(zone => ZoneFilteringHelper.IsActiveZone(zone))
                    .ToList();

                // 如果没有活跃区域，保持原有配置
                if (optimizedConfig.ValidZones.Count == 0)
                {
                    optimizedConfig.ValidZones = new List<CardZone>(config.ValidZones);
                }
            }

            return optimizedConfig;
        }

        /// <summary>
        /// 获取触发器配置的性能分析信息
        /// </summary>
        /// <param name="config">触发器配置</param>
        /// <returns>性能分析信息</returns>
        public static TriggerPerformanceInfo GetPerformanceInfo(this TriggerConfig config)
        {
            return new TriggerPerformanceInfo
            {
                IsOptimized = ZoneFilteringHelper.IsOptimizedForActiveZones(config),
                ZoneCount = config.GetValidZoneCount(),
                PrimaryZone = config.GetPrimaryZone(),
                Priority = config.GetZonePriority(),
                IncludesInactiveZones = config.IncludesInactiveZones(),
                SuggestedZones = ZoneFilteringHelper.SuggestOptimalZones(config)
            };
        }
    }

    /// <summary>
    /// 触发器性能分析信息
    /// </summary>
    public class TriggerPerformanceInfo
    {
        /// <summary>
        /// 是否已针对性能进行优化
        /// </summary>
        public bool IsOptimized { get; set; }

        /// <summary>
        /// 有效区域数量
        /// </summary>
        public int ZoneCount { get; set; }

        /// <summary>
        /// 主要区域
        /// </summary>
        public CardZone PrimaryZone { get; set; }

        /// <summary>
        /// 优先级
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// 是否包含非活跃区域
        /// </summary>
        public bool IncludesInactiveZones { get; set; }

        /// <summary>
        /// 建议的最佳区域列表
        /// </summary>
        public List<CardZone> SuggestedZones { get; set; } = new();

        /// <summary>
        /// 获取性能评分（0-100，越高越好）
        /// </summary>
        public int GetPerformanceScore()
        {
            int score = 100;

            // 如果未优化，扣分
            if (!IsOptimized) score -= 30;

            // 如果包含非活跃区域，扣分
            if (IncludesInactiveZones) score -= 20;

            // 根据区域数量调整分数
            if (ZoneCount == 0) score -= 25; // 所有区域都有效
            else if (ZoneCount > 3) score -= 15; // 区域过多

            // 根据优先级调整分数
            if (Priority > 5) score -= 10;

            return Math.Max(0, score);
        }
    }
}
