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
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 事件类型列表（从配置中获取）
        /// </summary>
        public List<EventType> EventTypes => Config.EventTypes;

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
            if (!IsEnabled || TriggeredThisEvent)
                return false;

            // 1. 检查区域限制
            if (sourceCard != null && !Config.IsValidZone(sourceCard.CurrentZone))
            {
#if DEBUG
                // Console.WriteLine($"[TriggerDebug] Trigger for '{sourceCard.Card.Name}' filtered by zone. Current: {sourceCard.CurrentZone}, Valid: {string.Join(",", Config.ValidZones)}");
#endif
                return false;
            }

            // 2. 检查触发源限制
            if (!IsValidRelation(gameEvent, sourceCard))
            {
#if DEBUG
                // Console.WriteLine($"[TriggerDebug] Trigger for '{sourceCard?.Card.Name}' filtered by relation type. Required: {Config.RelationType}");
#endif
                return false;
            }

            // 3. 检查触发次数限制
            if (IsLimitReached(CurrentTurnTriggerCount, Config.CountLimit?.MaxPerTurn)
                || IsLimitReached(TotalTriggerCount, Config.CountLimit?.MaxPerGame))
            {
                return false;
            }

            // 3. 检查时序匹配 (由调用者保证)
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
        /// Checks if the relationship between the ability owner and the event participants
        /// matches the required relation type.
        /// </summary>
        private bool IsValidRelation(IGameEvent gameEvent, CardInstance? abilityOwnerCard)
        {
            if (Config.RelationType == TriggerRelationType.Any)
                return true;

            // If the ability has no owner, it cannot be "Self" in any context.
            if (abilityOwnerCard == null)
            {
                return Config.RelationType != TriggerRelationType.SourceSelf &&
                       Config.RelationType != TriggerRelationType.TargetSelf;
            }

            var eventSourceCard = gameEvent.Source?.Card;
            var eventTargetCard = gameEvent.Target.FirstOrDefault()?.Card;

            switch (Config.RelationType)
            {
                case TriggerRelationType.SourceSelf:
                    return eventSourceCard != null && eventSourceCard.InstanceId == abilityOwnerCard.InstanceId;

                case TriggerRelationType.SourceOther:
                    return eventSourceCard != null && eventSourceCard.InstanceId != abilityOwnerCard.InstanceId;

                case TriggerRelationType.TargetSelf:
                    return eventTargetCard != null && eventTargetCard.InstanceId == abilityOwnerCard.InstanceId;

                case TriggerRelationType.TargetOther:
                    return eventTargetCard != null && eventTargetCard.InstanceId != abilityOwnerCard.InstanceId;

                default:
                    return true; // Should be covered by the 'Any' check at the beginning
            }
        }

        private static bool IsLimitReached(int currentCount, int? limit)
        {
            return limit.HasValue && limit.Value > 0 && currentCount >= limit.Value;
        }
    }
}
