using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities;
using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.Runtime
{
    /// <summary>
    /// 运行时触发器包装类
    /// 包装TriggerConfig并添加运行时状态和行为
    /// </summary>
    public class Trigger
    {
        /// <summary>
        /// 静态触发器配置
        /// </summary>
        public TriggerConfig Config { get; }

        /// <summary>
        /// 上次触发时间
        /// </summary>
        public DateTime? LastTriggeredTime { get; private set; }

        /// <summary>
        /// 总触发次数
        /// </summary>
        public int TotalTriggerCount { get; private set; }

        /// <summary>
        /// 当前回合触发次数
        /// </summary>
        public int CurrentTurnTriggerCount { get; private set; }

        /// <summary>
        /// 是否在当前事件处理中已触发
        /// </summary>
        public bool TriggeredThisEvent { get; private set; }

        /// <summary>
        /// 触发器是否处于冷却状态
        /// </summary>
        public bool IsOnCooldown { get; private set; }

        /// <summary>
        /// 冷却结束时间
        /// </summary>
        public DateTime? CooldownEndTime { get; private set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 事件类型列表（从配置中获取）
        /// </summary>
        public List<GameEventType> EventTypes => Config.EventTypes;

        public Trigger(TriggerConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// 检查是否可以触发（不进行区域检查）
        /// </summary>
        /// <param name="gameEvent">游戏事件</param>
        /// <returns>是否可以触发</returns>
        public bool CanTrigger(IGameEvent gameEvent)
        {
            return CanTrigger(gameEvent, null);
        }

        /// <summary>
        /// 检查是否可以触发（包含区域检查）
        /// </summary>
        /// <param name="gameEvent">游戏事件</param>
        /// <param name="sourceCard">触发器所属的卡牌实例，用于区域检查</param>
        /// <returns>是否可以触发</returns>
        public bool CanTrigger(IGameEvent gameEvent, CardInstance? sourceCard)
        {
            if (!IsEnabled)
                return false;

            if (IsOnCooldown && DateTime.UtcNow < CooldownEndTime)
                return false;

            if (TriggeredThisEvent)
                return false;

            // 检查事件类型匹配 - Convert string to enum for comparison
            if (!Enum.TryParse<GameEventType>(gameEvent.EventType, true, out var eventTypeEnum) ||
                !Config.EventTypes.Contains(eventTypeEnum))
                return false;

            // 检查区域限制（性能优化）
            if (sourceCard != null && !Config.IsValidZone(sourceCard.CurrentZone))
            {
                // 记录区域过滤的日志（仅在调试模式下）
#if DEBUG
                // Note: We don't have access to logger here, but this could be added if needed
                // _logger?.LogTrace("[RuntimeTrigger] Trigger filtered out due to zone restriction. Card {CardId} in zone {Zone}, valid zones: {ValidZones}",
                //     sourceCard.InstanceId, sourceCard.CurrentZone, string.Join(", ", Config.ValidZones));
#endif
                return false;
            }

            // 检查时序匹配
            // 时序检查由事件处理器在适当的时机调用触发器
            // 这里假设调用时机已经正确
            return true;
        }

        /// <summary>
        /// 记录触发
        /// </summary>
        public void RecordTrigger()
        {
            LastTriggeredTime = DateTime.UtcNow;
            TotalTriggerCount++;
            CurrentTurnTriggerCount++;
            TriggeredThisEvent = true;
        }

        /// <summary>
        /// 重置回合计数器
        /// </summary>
        public void ResetTurnCounters()
        {
            CurrentTurnTriggerCount = 0;
        }

        /// <summary>
        /// 重置事件标志
        /// </summary>
        public void ResetEventFlags()
        {
            TriggeredThisEvent = false;
        }

        /// <summary>
        /// 设置冷却
        /// </summary>
        /// <param name="cooldownDuration">冷却时长</param>
        public void SetCooldown(TimeSpan cooldownDuration)
        {
            IsOnCooldown = true;
            CooldownEndTime = DateTime.UtcNow.Add(cooldownDuration);
        }

        /// <summary>
        /// 清除冷却
        /// </summary>
        public void ClearCooldown()
        {
            IsOnCooldown = false;
            CooldownEndTime = null;
        }

        /// <summary>
        /// 检查并更新冷却状态
        /// </summary>
        public void UpdateCooldownStatus()
        {
            if (IsOnCooldown && DateTime.UtcNow >= CooldownEndTime)
            {
                ClearCooldown();
            }
        }
    }
}
