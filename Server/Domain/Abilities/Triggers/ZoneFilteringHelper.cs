using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Events;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Abilities.Triggers
{
    /// <summary>
    /// 区域过滤助手类，提供区域相关的性能优化功能
    /// </summary>
    public static class ZoneFilteringHelper
    {
        /// <summary>
        /// 活跃区域列表 - 这些区域中的卡牌通常需要处理更多事件
        /// </summary>
        public static readonly HashSet<CardZone> ActiveZones = new()
        {
            CardZone.Field,
            CardZone.Playing
        };

        /// <summary>
        /// 低活跃区域列表 - 这些区域中的卡牌通常处理较少事件
        /// </summary>
        public static readonly HashSet<CardZone> InactiveZones = new()
        {
            CardZone.Deck,
            CardZone.Hand,
            CardZone.Graveyard,
            CardZone.Extra,
            CardZone.Emblem
        };

        /// <summary>
        /// 检查区域是否为活跃区域
        /// </summary>
        /// <param name="zone">要检查的区域</param>
        /// <returns>如果是活跃区域返回true</returns>
        public static bool IsActiveZone(CardZone zone)
        {
            return ActiveZones.Contains(zone);
        }

        /// <summary>
        /// 检查区域是否为非活跃区域
        /// </summary>
        /// <param name="zone">要检查的区域</param>
        /// <returns>如果是非活跃区域返回true</returns>
        public static bool IsInactiveZone(CardZone zone)
        {
            return InactiveZones.Contains(zone);
        }

        /// <summary>
        /// 获取区域的活跃度优先级（数值越小优先级越高）
        /// </summary>
        /// <param name="zone">要检查的区域</param>
        /// <returns>优先级数值</returns>
        public static int GetZonePriority(CardZone zone)
        {
            return zone switch
            {
                CardZone.Field => 1,      // 最高优先级
                CardZone.Playing => 3,    // 中高优先级
                CardZone.Hand => 4,       // 中优先级
                CardZone.Graveyard => 5,  // 低优先级
                CardZone.Deck => 6,       // 最低优先级
                CardZone.Extra => 7,      // 特殊区域
                CardZone.Emblem => 7,     // 纹章区域（特殊区域）
                _ => 8                    // 未知区域
            };
        }

        /// <summary>
        /// 根据区域优先级对卡牌列表进行排序
        /// </summary>
        /// <param name="cards">要排序的卡牌列表</param>
        /// <returns>按区域优先级排序的卡牌列表</returns>
        public static List<CardInstance> SortByZonePriority(IEnumerable<CardInstance> cards)
        {
            return cards.OrderBy(card => GetZonePriority(card.CurrentZone)).ToList();
        }

        /// <summary>
        /// 过滤出指定区域的卡牌
        /// </summary>
        /// <param name="cards">要过滤的卡牌列表</param>
        /// <param name="zones">允许的区域列表</param>
        /// <returns>过滤后的卡牌列表</returns>
        public static List<CardInstance> FilterByZones(IEnumerable<CardInstance> cards, params CardZone[] zones)
        {
            var zoneSet = new HashSet<CardZone>(zones);
            return cards.Where(card => zoneSet.Contains(card.CurrentZone)).ToList();
        }

        /// <summary>
        /// 过滤出活跃区域的卡牌
        /// </summary>
        /// <param name="cards">要过滤的卡牌列表</param>
        /// <returns>活跃区域的卡牌列表</returns>
        public static List<CardInstance> FilterActiveZones(IEnumerable<CardInstance> cards)
        {
            return cards.Where(card => IsActiveZone(card.CurrentZone)).ToList();
        }

        /// <summary>
        /// 过滤出非活跃区域的卡牌
        /// </summary>
        /// <param name="cards">要过滤的卡牌列表</param>
        /// <returns>非活跃区域的卡牌列表</returns>
        public static List<CardInstance> FilterInactiveZones(IEnumerable<CardInstance> cards)
        {
            return cards.Where(card => IsInactiveZone(card.CurrentZone)).ToList();
        }

        /// <summary>
        /// 获取区域统计信息
        /// </summary>
        /// <param name="cards">要统计的卡牌列表</param>
        /// <returns>区域统计字典</returns>
        public static Dictionary<CardZone, int> GetZoneStatistics(IEnumerable<CardInstance> cards)
        {
            return cards.GroupBy(card => card.CurrentZone)
                       .ToDictionary(group => group.Key, group => group.Count());
        }

        /// <summary>
        /// 检查触发器配置是否针对活跃区域进行了优化
        /// </summary>
        /// <param name="config">触发器配置</param>
        /// <returns>如果配置已优化返回true</returns>
        public static bool IsOptimizedForActiveZones(TriggerConfig config)
        {
            // 如果ValidZones为空，表示所有区域都有效，未优化
            if (config.ValidZones == null || config.ValidZones.Count == 0)
                return false;

            // 如果只包含活跃区域，则认为已优化
            return config.ValidZones.All(zone => IsActiveZone(zone));
        }

        /// <summary>
        /// 为触发器配置建议最佳区域设置
        /// </summary>
        /// <param name="config">触发器配置</param>
        /// <returns>建议的区域列表</returns>
        public static List<CardZone> SuggestOptimalZones(TriggerConfig config)
        {
            // 基于事件类型建议最佳区域
            var suggestedZones = new List<CardZone>();

            foreach (var eventType in config.EventTypes)
            {
                switch (eventType)
                {
                    case EventType.Summon:
                    case EventType.Attack:
                        // 这些事件主要在场上发生
                        suggestedZones.Add(CardZone.Field);
                        break;

                    case EventType.TurnStart:
                    case EventType.TurnEnd:
                        // 回合事件可能影响场上和手牌
                        suggestedZones.AddRange(new[] { CardZone.Field, CardZone.Hand });
                        break;

                    default:
                        // 默认建议活跃区域
                        suggestedZones.AddRange(ActiveZones);
                        break;
                }
            }

            return suggestedZones.Distinct().ToList();
        }
    }
}
