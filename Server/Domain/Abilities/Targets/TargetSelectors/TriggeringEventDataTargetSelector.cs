using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors
{
    /// <summary>
    /// 触发事件数据目标选择器
    /// 从触发事件的数据中提取目标
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public class TriggeringEventDataTargetSelector : ITargetSelector
    {
        private readonly TargetConfig _config;

        public TriggeringEventDataTargetSelector(TargetConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// 从触发事件中提取目标
        /// </summary>
        /// <param name="config">目标配置</param>
        /// <param name="context">统一游戏上下文</param>
        /// <returns>目标ID列表</returns>
        public Task<List<TargetId>> SelectTargetsAsync(TargetConfig config, GameContext context)
        {
            if (context.TriggeringEvent == null)
            {
                return Task.FromResult(new List<TargetId>());
            }

            var targets = ExtractTargetsFromEvent(context.TriggeringEvent);
            return Task.FromResult(targets);
        }

        /// <summary>
        /// 从具体事件中提取目标
        /// </summary>
        /// <param name="gameEvent">游戏事件</param>
        /// <returns>目标ID列表</returns>
        private List<TargetId> ExtractTargetsFromEvent(IGameEvent gameEvent)
        {
            var targets = new List<TargetId>();

            switch (gameEvent)
            {
                case CardPlayedEventDto cardPlayedEvent:
                    // 从卡牌打出事件中提取被打出的卡牌
                    if (cardPlayedEvent.PlayedCardInstance?.InstanceId != Guid.Empty)
                    {
                        targets.Add(TargetId.Card(cardPlayedEvent.PlayedCardInstance!.InstanceId));
                    }
                    break;

                case DamageDealtEventDto damageEvent:
                    // 从伤害事件中提取受伤害的目标
                    if (damageEvent.TargetUid != Guid.Empty)
                    {
                        targets.Add(TargetId.Card(damageEvent.TargetUid));
                    }
                    break;

                case UnitDestroyedEventDto destroyedEvent:
                    // 从单位被破坏事件中提取被破坏的单位
                    if (destroyedEvent.UnitUid != Guid.Empty)
                    {
                        targets.Add(TargetId.Card(destroyedEvent.UnitUid));
                    }
                    break;

                case UnitEvolvedEventDto evolutionEvent:
                    // 从进化事件中提取进化的卡牌
                    if (evolutionEvent.EvolvedUnitState?.InstanceId != Guid.Empty)
                    {
                        targets.Add(TargetId.Card(evolutionEvent.EvolvedUnitState!.InstanceId));
                    }
                    break;

                case AttackDeclaredEventDto attackEvent:
                    // 从攻击事件中提取攻击者和被攻击者
                    if (!string.IsNullOrEmpty(attackEvent.AttackerUid) && Guid.TryParse(attackEvent.AttackerUid, out var attackerId))
                    {
                        targets.Add(TargetId.Card(attackerId));
                    }
                    if (!string.IsNullOrEmpty(attackEvent.TargetUid) && Guid.TryParse(attackEvent.TargetUid, out var targetId))
                    {
                        targets.Add(TargetId.Card(targetId));
                    }
                    break;

                case CardSummonedEventDto summonEvent:
                    // 从召唤事件中提取被召唤的卡牌
                    if (summonEvent.SummonedCard?.InstanceId != Guid.Empty)
                    {
                        targets.Add(TargetId.Card(summonEvent.SummonedCard!.InstanceId));
                    }
                    break;

                case CardDrawnEventDto drawnEvent:
                    // 从抽牌事件中提取被抽取的卡牌
                    if (!string.IsNullOrEmpty(drawnEvent.DrawnCardUid) && Guid.TryParse(drawnEvent.DrawnCardUid, out var drawnCardId))
                    {
                        targets.Add(TargetId.Card(drawnCardId));
                    }
                    break;

                default:
                    // 对于未知事件类型，尝试通过反射获取可能的目标ID
                    targets.AddRange(ExtractTargetsFromUnknownEvent(gameEvent));
                    break;
            }

            return targets;
        }

        /// <summary>
        /// 从未知事件类型中尝试提取目标
        /// </summary>
        /// <param name="gameEvent">游戏事件</param>
        /// <returns>目标ID列表</returns>
        private List<TargetId> ExtractTargetsFromUnknownEvent(IGameEvent gameEvent)
        {
            var targets = new List<TargetId>();

            // 使用反射查找可能的目标ID属性
            var eventType = gameEvent.GetType();
            var properties = eventType.GetProperties();

            foreach (var property in properties)
            {
                // 查找名称包含"InstanceId"或"TargetId"的Guid属性
                if ((property.Name.Contains("InstanceId") || property.Name.Contains("TargetId")) &&
                    property.PropertyType == typeof(Guid))
                {
                    var value = property.GetValue(gameEvent);
                    if (value is Guid guidValue && guidValue != Guid.Empty)
                    {
                        targets.Add(TargetId.Card(guidValue));
                    }
                }
                // 查找名称包含"InstanceId"或"TargetId"的Guid?属性
                else if ((property.Name.Contains("InstanceId") || property.Name.Contains("TargetId")) &&
                         property.PropertyType == typeof(Guid?))
                {
                    var value = property.GetValue(gameEvent);
                    if (value is Guid guidValue && guidValue != Guid.Empty)
                    {
                        targets.Add(TargetId.Card(guidValue));
                    }
                }
            }

            return targets;
        }
    }
}
