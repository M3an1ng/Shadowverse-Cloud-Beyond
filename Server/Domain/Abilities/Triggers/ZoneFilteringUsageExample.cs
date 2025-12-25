using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Abilities.Triggers
{
    /// <summary>
    /// 区域过滤功能的使用示例
    /// 展示如何使用新的区域过滤功能来优化卡牌能力系统的性能
    /// </summary>
    public static class ZoneFilteringUsageExample
    {
        /// <summary>
        /// 示例1: 创建针对场上单位的攻击触发器
        /// 这种触发器只在卡牌在场上时才会响应攻击事件，避免手牌中的卡牌无意义地检查攻击事件
        /// </summary>
        public static TriggerConfig CreateFieldOnlyAttackTrigger()
        {
            return TriggerConfig.OnEventInZone(EventType.PlayCard, CardZone.Field)
                .WithZone(CardZone.Field); // 链式调用设置区域
        }

        /// <summary>
        /// 示例2: 创建手牌和场上都生效的抽牌触发器
        /// 某些能力可能需要在多个区域中响应事件
        /// </summary>
        public static TriggerConfig CreateMultiZoneDrawTrigger()
        {
            return TriggerConfig.OnEventInZones(EventType.Draw, CardZone.Hand, CardZone.Field);
        }

        /// <summary>
        /// 示例3: 创建所有区域都生效的回合开始触发器
        /// 某些全局效果需要在所有区域中响应
        /// </summary>
        public static TriggerConfig CreateGlobalTurnStartTrigger()
        {
            return TriggerConfig.OnEventInAllZones(EventType.TurnStart);
        }

        /// <summary>
        /// 示例4: 优化现有的触发器配置
        /// 将一个允许所有区域的触发器优化为只在活跃区域生效
        /// </summary>
        public static TriggerConfig OptimizeExistingTrigger(TriggerConfig originalConfig)
        {
            // 检查是否需要优化
            if (ZoneFilteringHelper.IsOptimizedForActiveZones(originalConfig))
            {
                return originalConfig; // 已经优化，无需修改
            }

            // 创建优化版本
            return originalConfig.CreateActiveZoneOptimized();
        }

        /// <summary>
        /// 示例5: 分析触发器性能并提供改进建议
        /// </summary>
        public static void AnalyzeTriggerPerformance(TriggerConfig config)
        {
            var performanceInfo = config.GetPerformanceInfo();

            // 输出性能分析结果
            Console.WriteLine($"触发器性能分析:");
            Console.WriteLine($"- 是否已优化: {performanceInfo.IsOptimized}");
            Console.WriteLine($"- 性能评分: {performanceInfo.GetPerformanceScore()}/100");
            Console.WriteLine($"- 有效区域数量: {performanceInfo.ZoneCount}");
            Console.WriteLine($"- 主要区域: {performanceInfo.PrimaryZone}");
            Console.WriteLine($"- 包含非活跃区域: {performanceInfo.IncludesInactiveZones}");

            if (performanceInfo.SuggestedZones.Count > 0)
            {
                Console.WriteLine($"- 建议区域: {string.Join(", ", performanceInfo.SuggestedZones)}");
            }
        }

        /// <summary>
        /// 示例6: 根据卡牌类型创建最优触发器
        /// </summary>
        public static TriggerConfig CreateOptimalTriggerForCardType(string cardType, EventType eventType)
        {
            var config = new TriggerConfig
            {
                EventTypes = new List<EventType> { eventType },
                Timing = TriggerTiming.On
            };

            // 根据卡牌类型设置最优区域
            switch (cardType.ToLower())
            {
                case "follower":
                case "amulet":
                    // 随从和护符主要在场上生效
                    config.WithZone(CardZone.Field);
                    break;

                case "spell":
                    // 法术可能在手牌中就有效果（如显示可用目标）
                    config.WithZones(CardZone.Hand, CardZone.Playing);
                    break;

                default:
                    // 默认使用活跃区域
                    config.ValidZones = ZoneFilteringHelper.ActiveZones.ToList();
                    break;
            }

            return config;
        }

        /// <summary>
        /// 示例7: 批量优化触发器配置列表
        /// </summary>
        public static List<TriggerConfig> OptimizeTriggerList(List<TriggerConfig> originalConfigs)
        {
            var optimizedConfigs = new List<TriggerConfig>();

            foreach (var config in originalConfigs)
            {
                var performanceInfo = config.GetPerformanceInfo();

                if (performanceInfo.GetPerformanceScore() < 70) // 性能评分低于70分需要优化
                {
                    var optimized = config.CreateActiveZoneOptimized();
                    optimizedConfigs.Add(optimized);
                }
                else
                {
                    optimizedConfigs.Add(config); // 性能已足够好，保持原配置
                }
            }

            return optimizedConfigs;
        }

        /// <summary>
        /// 示例8: 创建基于事件类型的智能触发器
        /// 自动根据事件类型选择最合适的区域配置
        /// </summary>
        public static TriggerConfig CreateSmartTrigger(EventType eventType)
        {
            var config = TriggerConfig.OnEvent(eventType);

            // 使用助手类的建议功能
            var suggestedZones = ZoneFilteringHelper.SuggestOptimalZones(config);

            if (suggestedZones.Count > 0)
            {
                config.ValidZones = suggestedZones;
            }
            else
            {
                // 如果没有特定建议，默认使用活跃区域
                config.ValidZones = ZoneFilteringHelper.ActiveZones.ToList();
            }

            return config;
        }

        /// <summary>
        /// 示例9: 性能监控和统计
        /// </summary>
        public static void MonitorZoneFilteringEffectiveness(List<TriggerConfig> configs)
        {
            int totalConfigs = configs.Count;
            int optimizedConfigs = 0;
            int activeZoneOnlyConfigs = 0;
            var zoneUsageStats = new Dictionary<CardZone, int>();

            foreach (var config in configs)
            {
                if (ZoneFilteringHelper.IsOptimizedForActiveZones(config))
                    optimizedConfigs++;

                if (config.IsActiveZoneOnly())
                    activeZoneOnlyConfigs++;

                // 统计区域使用情况
                if (config.ValidZones != null)
                {
                    foreach (var zone in config.ValidZones)
                    {
                        zoneUsageStats[zone] = zoneUsageStats.GetValueOrDefault(zone, 0) + 1;
                    }
                }
            }

            // 输出统计结果
            Console.WriteLine($"区域过滤效果统计:");
            Console.WriteLine($"- 总触发器数量: {totalConfigs}");
            Console.WriteLine($"- 已优化触发器: {optimizedConfigs} ({(double)optimizedConfigs / totalConfigs * 100:F1}%)");
            Console.WriteLine($"- 仅活跃区域触发器: {activeZoneOnlyConfigs} ({(double)activeZoneOnlyConfigs / totalConfigs * 100:F1}%)");

            Console.WriteLine("- 区域使用统计:");
            foreach (var kvp in zoneUsageStats.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value} 次使用");
            }
        }
    }
}
