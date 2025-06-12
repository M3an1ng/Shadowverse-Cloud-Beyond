//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // Add using for IConditionFactory
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // Add using for IEffectExecutor
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Dtos.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// 配置驱动的能力管理器，负责管理和协调所有卡片能力
    /// 基于最新Event驱动架构设计
    /// </summary>
    public class AbilityManager : IAbilityTriggerService
    {
        private readonly ConcurrentDictionary<string, List<Ability>> _abilitiesByEventType = new();
        private readonly ConcurrentDictionary<Guid, List<Ability>> _abilitiesByCardId = new();
        private readonly List<Ability> _allAbilities = new();
        private readonly ILogger<AbilityManager>? _logger;
        private readonly IConditionFactory _conditionFactory; // Inject Condition Factory
        private readonly IServiceScopeFactory _serviceScopeFactory; // Use IServiceScopeFactory to get scoped IEffectExecutor
        // Removed: _eventProcessor to break circular dependency
        // Removed: _buffManager, _cardRepository, _functionRegistry

        public AbilityManager(
            ILogger<AbilityManager>? logger,
            IConditionFactory conditionFactory, // Add IConditionFactory
            IServiceScopeFactory serviceScopeFactory) // Use IServiceScopeFactory instead of direct IEffectExecutor
                                                      // Removed: IEventProcessor eventProcessor to break circular dependency
        {
            _logger = logger; // Nullable logger is allowed
            _conditionFactory = conditionFactory ?? throw new ArgumentNullException(nameof(conditionFactory)); // Store factory
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory)); // Store scope factory
            // Removed: _eventProcessor assignment to break circular dependency
        }

        /// <summary>
        /// 从配置创建并注册卡片的所有能力
        /// </summary>
        /// <param name="cardInstance">卡片实例</param>
        /// <param name="abilityConfigs">能力配置列表</param>
        /// <returns>创建的能力列表</returns>
        public List<Ability> CreateAbilitiesForCard(CardInstance cardInstance, List<AbilityConfig> abilityConfigs)
        {
            var abilities = new List<Ability>();

            foreach (var config in abilityConfigs)
            {
                var ability = CreateAbilityFromConfig(config, cardInstance);
                if (ability != null)
                {
                    abilities.Add(ability);
                    RegisterAbility(ability);
                }
            }

            _logger?.LogDebug("[AbilityManager] Created {Count} abilities for card {CardId}",
                abilities.Count, cardInstance.InstanceId);

            return abilities;
        }

        /// <summary>
        /// 从配置创建单个能力
        /// </summary>
        /// <param name="config">能力配置</param>
        /// <param name="cardInstance">卡片实例</param>
        /// <returns>创建的能力</returns>
        public Ability? CreateAbilityFromConfig(AbilityConfig config, CardInstance cardInstance)
        {
            try
            {
                // Create a scope to get IEffectExecutor
                using var scope = _serviceScopeFactory.CreateScope();
                var effectExecutor = scope.ServiceProvider.GetRequiredService<IEffectExecutor>();

                // Pass all required dependencies to the Ability constructor
                var ability = new Ability(
                    config,
                    _conditionFactory, // Pass injected factory
                    effectExecutor,    // Pass scoped executor
                    cardInstance);

                // 激活能力
                ability.Activate();

                return ability;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[AbilityManager] Error creating ability from config for card {CardId}",
                    cardInstance.InstanceId);
                return null;
            }
        }

        /// <summary>
        /// 注册能力到管理器
        /// </summary>
        /// <param name="ability">要注册的能力</param>
        public void RegisterAbility(Ability ability)
        {
            if (ability?.SourceCard == null) return;

            _allAbilities.Add(ability);

            // 按事件类型索引能力
            foreach (var eventType in ability.Trigger.EventTypes)
            {
                var eventTypeString = eventType.ToString();
                _abilitiesByEventType.AddOrUpdate(
                    eventTypeString,
                    new List<Ability> { ability },
                    (key, existingList) =>
                    {
                        existingList.Add(ability);
                        return existingList;
                    });
            }

            // 按卡片ID索引能力
            _abilitiesByCardId.AddOrUpdate(
                ability.SourceCard.InstanceId,
                new List<Ability> { ability },
                (key, existingList) =>
                {
                    existingList.Add(ability);
                    return existingList;
                });

            // 新增：更新CardInstance的能力列表（双向绑定）
            UpdateCardInstanceAbilities(ability.SourceCard);

            _logger?.LogDebug("[AbilityManager] Registered ability: {AbilityTag} for card {CardId}",
                ability.AbilityTag, ability.SourceCard.InstanceId);
        }

        /// <summary>
        /// 注销能力
        /// </summary>
        /// <param name="ability">要注销的能力</param>
        public void UnregisterAbility(Ability ability)
        {
            if (ability?.SourceCard == null) return;

            _allAbilities.Remove(ability);

            // 从事件类型索引中移除
            foreach (var eventType in ability.Trigger.EventTypes)
            {
                var eventTypeString = eventType.ToString();
                if (_abilitiesByEventType.TryGetValue(eventTypeString, out var abilities))
                {
                    abilities.Remove(ability);
                    if (abilities.Count == 0)
                    {
                        _abilitiesByEventType.TryRemove(eventTypeString, out _);
                    }
                }
            }

            // 从卡片ID索引中移除
            if (_abilitiesByCardId.TryGetValue(ability.SourceCard.InstanceId, out var cardAbilities))
            {
                cardAbilities.Remove(ability);
                if (cardAbilities.Count == 0)
                {
                    _abilitiesByCardId.TryRemove(ability.SourceCard.InstanceId, out _);
                }
            }

            // 新增：更新CardInstance的能力列表（双向绑定）
            UpdateCardInstanceAbilities(ability.SourceCard);

            _logger?.LogDebug("[AbilityManager] Unregistered ability: {AbilityTag} for card {CardId}",
                ability.AbilityTag, ability.SourceCard.InstanceId);
        }

        /// <summary>
        /// 注销卡片的所有能力
        /// </summary>
        /// <param name="cardInstanceId">卡片实例ID</param>
        public void UnregisterCardAbilities(Guid cardInstanceId)
        {
            if (_abilitiesByCardId.TryGetValue(cardInstanceId, out var abilities))
            {
                var abilitiesToRemove = abilities.ToList();
                CardInstance? cardInstance = null;

                foreach (var ability in abilitiesToRemove)
                {
                    if (cardInstance == null)
                        cardInstance = ability.SourceCard; // 获取CardInstance引用

                    UnregisterAbility(ability);
                }

                // 注意：UnregisterAbility已经会调用UpdateCardInstanceAbilities，
                // 但为了确保最终状态正确，这里再次更新
                if (cardInstance != null)
                {
                    UpdateCardInstanceAbilities(cardInstance);
                }
            }
        }

        /// <summary>
        /// 注销卡片的所有能力（重载方法，接受CardInstance参数）
        /// </summary>
        /// <param name="cardInstance">卡片实例</param>
        public void UnregisterCardAbilities(CardInstance cardInstance)
        {
            if (cardInstance != null)
            {
                UnregisterCardAbilities(cardInstance.InstanceId);
            }
        }

        /// <summary>
        /// 获取指定事件触发的能力列表
        /// 注意：此方法不再处理事件，只返回相关能力。事件处理应通过EventProcessor进行。
        /// </summary>
        /// <param name="gameEvent">触发的游戏事件</param>
        /// <param name="context">游戏上下文</param>
        /// <returns>触发的能力列表</returns>
        public async Task<List<Ability>> GetTriggeredAbilitiesAsync(IGameEvent gameEvent, GameContext context)
        {
            var triggeredAbilities = new List<Ability>();
            var eventTypeString = gameEvent.EventType.ToString();

            // 获取监听此事件类型的能力
            var relevantAbilities = GetAbilitiesForEvent(eventTypeString);

            foreach (var ability in relevantAbilities)
            {
                try
                {
                    // 检查能力是否可以触发（基本检查）
                    if (ability.CanTrigger(gameEvent))
                    {
                        // 检查条件是否满足
                        if (await ability.CheckConditionsAsync(context))
                        {
                            triggeredAbilities.Add(ability);
                            _logger?.LogDebug("[AbilityManager] Ability {AbilityTag} triggered by event {EventType}",
                                ability.AbilityTag, gameEvent.EventType);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[AbilityManager] Error checking trigger for ability {AbilityTag}",
                        ability.AbilityTag);
                }
            }

            return triggeredAbilities;
        }

        /// <summary>
        /// 获取指定事件类型的相关能力
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <returns>相关能力列表</returns>
        public List<Ability> GetAbilitiesForEvent(string eventType)
        {
            return _abilitiesByEventType.TryGetValue(eventType, out var abilities)
                ? abilities.ToList()
                : new List<Ability>();
        }

        /// <summary>
        /// 获取指定卡片的所有能力
        /// </summary>
        /// <param name="cardInstanceId">卡片实例ID</param>
        /// <returns>卡片能力列表</returns>
        public List<Ability> GetAbilitiesForCard(Guid cardInstanceId)
        {
            return _abilitiesByCardId.TryGetValue(cardInstanceId, out var abilities)
                ? abilities.ToList()
                : new List<Ability>();
        }

        /// <summary>
        /// 获取所有激活的能力
        /// </summary>
        /// <returns>激活能力列表</returns>
        public List<Ability> GetActiveAbilities()
        {
            return _allAbilities.ToList(); // Removed .Where(a => a.IsActive)
        }

        /// <summary>
        /// 清除所有能力
        /// </summary>
        public void ClearAllAbilities()
        {
            var count = _allAbilities.Count;
            _allAbilities.Clear();
            _abilitiesByEventType.Clear();
            _abilitiesByCardId.Clear();

            _logger?.LogDebug("[AbilityManager] Cleared {Count} abilities", count);
        }

        /// <summary>
        /// 更新CardInstance的能力列表（双向绑定核心方法）
        /// </summary>
        /// <param name="cardInstance">要更新的卡牌实例</param>
        private void UpdateCardInstanceAbilities(CardInstance cardInstance)
        {
            if (cardInstance == null) return;

            var abilities = GetAbilitiesForCard(cardInstance.InstanceId);
            cardInstance.SetAbilities(abilities.AsReadOnly());

            _logger?.LogDebug("[AbilityManager] Updated abilities for card {CardId}: {Count} abilities",
                cardInstance.InstanceId, abilities.Count);
        }

        /// <summary>
        /// 批量更新多个CardInstance的能力列表
        /// </summary>
        /// <param name="cardInstances">要更新的卡牌实例列表</param>
        public void RefreshCardAbilities(IEnumerable<CardInstance> cardInstances)
        {
            foreach (var cardInstance in cardInstances)
            {
                UpdateCardInstanceAbilities(cardInstance);
            }
        }

        /// <summary>
        /// 转换事件为DTO（兼容性方法）
        /// </summary>
        /// <param name="events">事件列表</param>
        /// <returns>事件DTO列表</returns>
        public List<GameEventDto> ConvertToEventDtos(List<IGameEvent> events)
        {
            var eventDtos = new List<GameEventDto>();

            foreach (var gameEvent in events)
            {
                try
                {
                    var dto = ConvertEventToDto(gameEvent);
                    if (dto != null)
                    {
                        eventDtos.Add(dto);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[AbilityManager] Error converting event {EventType} to DTO",
                        gameEvent.EventType);
                }
            }

            return eventDtos;
        }

        /// <summary>
        /// 将单个事件转换为DTO
        /// </summary>
        private GameEventDto? ConvertEventToDto(IGameEvent gameEvent)
        {
            return gameEvent.EventType switch
            {
                "ability_triggered" => new AbilityTriggeredEventDto(
                    gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                    gameEvent.SourceCard?.InstanceId ?? Guid.Empty,
                    (string)(gameEvent.Parameters.GetValueOrDefault("ability_tag") ?? string.Empty),
                    (string)(gameEvent.Parameters.GetValueOrDefault("trigger_event_type") ?? string.Empty),
                    gameEvent.Parameters
                ),
                _ => null
            };
        }
    }
} // Closing brace for namespace ShadowverseBeyond.Server.Domain.Abilities