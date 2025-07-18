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
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue; // Added for IAbility

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// 配置驱动的能力管理器，负责管理和协调所有卡片能力
    /// 基于最新Event驱动架构设计
    /// </summary>
    public class AbilityManager : IAbilityTriggerService
    {
        private readonly ConcurrentDictionary<string, List<IAbility>> _abilitiesByEventType = new();
        private readonly ConcurrentDictionary<Guid, List<IAbility>> _abilitiesByCardId = new();
        private readonly List<IAbility> _allAbilities = new();
        private readonly ConcurrentDictionary<Guid, IAbility> _abilitiesByRuntimeId = new(); // 新增：按RuntimeId索引能力
        private readonly ILogger<AbilityManager>? _logger;
        private readonly IConditionFactory _conditionFactory; // Inject Condition Factory
        private readonly IServiceScopeFactory _serviceScopeFactory; // Use IServiceScopeFactory to get scoped IEffectExecutor
        // Removed: _eventProcessor to break circular dependency
        // Removed: _buffManager, _cardRepository, _functionRegistry

        public AbilityManager(
            ILogger<AbilityManager>? logger,
            IConditionFactory conditionFactory,
            IServiceScopeFactory serviceScopeFactory,
            IEnumerable<IAbility> systemAbilities) // Added systemAbilities
        {
            _logger = logger;
            _conditionFactory = conditionFactory ?? throw new ArgumentNullException(nameof(conditionFactory));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));

            if (systemAbilities != null)
            {
                foreach (var systemAbility in systemAbilities)
                {
                    RegisterAbility(systemAbility);
                }
                _logger?.LogInformation("[AbilityManager] Registered {Count} system abilities.", systemAbilities.Count());
            }
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
                // Create a factory function that creates a scope and gets IEffectExecutor
                Func<IEffectExecutor> effectExecutorFactory = () =>
                {
                    var scope = _serviceScopeFactory.CreateScope();
                    return scope.ServiceProvider.GetRequiredService<IEffectExecutor>();
                };

                // Pass all required dependencies to the Ability constructor
                var ability = new Ability(
                    config,
                    _conditionFactory, // Pass injected factory
                    cardInstance);

                // ability.Activate(); // Activate() 方法已移除

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
        public void RegisterAbility(IAbility ability)
        {
            if (ability == null) return;
            // If it's a card ability (i.e., an instance of Ability class), it must have a SourceCard.
            // System abilities (other IAbility implementers) can have SourceCard == null.
            if (ability is Ability && ability.SourceCard == null)
            {
                _logger?.LogWarning("[AbilityManager] Attempted to register a card ability (Type: Ability) without a SourceCard. AbilityTag: {AbilityTag}. Registration skipped.", ability.AbilityTag);
                return;
            }


            _allAbilities.Add(ability);

            // 按事件类型索引能力
            // Ensure TriggerConfig is used for event types
            if (ability.TriggerConfig != null && ability.TriggerConfig.EventTypes != null)
            {
                foreach (var eventType in ability.TriggerConfig.EventTypes)
                {
                    var eventTypeString = eventType.ToString();
                    _abilitiesByEventType.AddOrUpdate(
                        eventTypeString,
                        new List<IAbility> { ability },
                        (key, existingList) =>
                        {
                            existingList.Add(ability);
                            return existingList;
                        });
                }
            }


            // 按卡片ID索引能力 (Only if SourceCard is not null)
            if (ability.SourceCard != null)
            {
                _abilitiesByCardId.AddOrUpdate(
                    ability.SourceCard.InstanceId,
                    new List<IAbility> { ability },
                    (key, existingList) =>
                    {
                        existingList.Add(ability);
                        return existingList;
                    });

                // 新增：更新CardInstance的能力列表（双向绑定）
                UpdateCardInstanceAbilities(ability.SourceCard);
            }

            // 新增：填充 _abilitiesByRuntimeId 字典
            // IAbility 保证了 RuntimeId 的存在 (Ability.cs 中初始化)
            _abilitiesByRuntimeId.TryAdd(ability.RuntimeId, ability);


            _logger?.LogDebug("[AbilityManager] Registered ability: {AbilityTag} (Type: {AbilityType}, RuntimeId: {RuntimeId}) for card {CardId}",
                ability.AbilityTag, ability.Type, ability.RuntimeId, ability.SourceCard?.InstanceId);
        }

        /// <summary>
        /// 注销能力
        /// </summary>
        /// <param name="ability">要注销的能力</param>
        public void UnregisterAbility(IAbility ability)
        {
            if (ability == null) return;
            // Allow system abilities without SourceCard to be unregistered from _allAbilities
            // but SourceCard specific logic should only run if SourceCard exists.

            _allAbilities.Remove(ability);

            // 从事件类型索引中移除
            // Ensure TriggerConfig is used for event types
            if (ability.TriggerConfig != null && ability.TriggerConfig.EventTypes != null)
            {
                foreach (var eventType in ability.TriggerConfig.EventTypes)
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
            }

            // 从卡片ID索引中移除 (Only if SourceCard is not null)
            if (ability.SourceCard != null)
            {
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
            }

            // 新增：从 _abilitiesByRuntimeId 字典中移除
            _abilitiesByRuntimeId.TryRemove(ability.RuntimeId, out _);

            _logger?.LogDebug("[AbilityManager] Unregistered ability: {AbilityTag} (Type: {AbilityType}, RuntimeId: {RuntimeId}) for card {CardId}",
                ability.AbilityTag, ability.Type, ability.RuntimeId, ability.SourceCard?.InstanceId);
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
        public async Task<List<IAbility>> GetTriggeredAbilitiesAsync(IGameEvent gameEvent, GameContext context)
        {
            var triggeredAbilities = new List<IAbility>();
            var eventTypeString = gameEvent.EventType.ToString();

            // 获取监听此事件类型的能力
            var relevantAbilities = GetAbilitiesForEvent(eventTypeString);

            foreach (var ability in relevantAbilities)
            {
                try
                {
                    // Use the CanTrigger method from IAbility
                    // IsActive 检查已移除，激活状态由外部管理
                    if (await ability.CanTrigger(gameEvent, context)) // 传递 context
                    {
                        // 检查条件是否满足
                        if (await ability.CheckConditionsAsync(context))
                        {
                            triggeredAbilities.Add(ability);
                            _logger?.LogDebug("[AbilityManager] Ability {AbilityTag} (Type: {AbilityType}) triggered by event {EventType}",
                                ability.AbilityTag, ability.Type, gameEvent.EventType);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[AbilityManager] Error checking trigger for ability {AbilityTag} (Type: {AbilityType})",
                        ability.AbilityTag, ability.Type);
                }
            }

            return triggeredAbilities;
        }

        /// <summary>
        /// 获取指定事件类型的相关能力
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <returns>相关能力列表</returns>
        public List<IAbility> GetAbilitiesForEvent(string eventType)
        {
            return _abilitiesByEventType.TryGetValue(eventType, out var abilities)
                ? abilities.ToList() // ToList creates a copy, which is good practice here
                : new List<IAbility>();
        }

        /// <summary>
        /// 获取指定卡片的所有能力
        /// </summary>
        /// <param name="cardInstanceId">卡片实例ID</param>
        /// <returns>卡片能力列表</returns>
        public List<IAbility> GetAbilitiesForCard(Guid cardInstanceId)
        {
            return _abilitiesByCardId.TryGetValue(cardInstanceId, out var abilities)
                ? abilities.ToList() // ToList creates a copy
                : new List<IAbility>();
        }

        /// <summary>
        /// 获取所有激活的能力
        /// </summary>
        /// <returns>激活能力列表</returns>
        public List<IAbility> GetActiveAbilities()
        {
            // IsActive 属性已移除，此方法现在返回所有能力
            // 如果需要过滤激活的能力，调用者需要自行实现
            return _allAbilities.ToList();
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
            _abilitiesByRuntimeId.Clear(); // 新增：清空RuntimeId字典

            _logger?.LogDebug("[AbilityManager] Cleared {Count} abilities from all collections.", count);
        }

        /// <summary>
        /// 根据运行时ID获取能力实例。
        /// </summary>
        /// <param name="runtimeId">能力的运行时唯一ID。</param>
        /// <returns>找到的IAbility实例，如果未找到则返回null。</returns>
        public IAbility? GetAbilityByRuntimeId(Guid runtimeId)
        {
            if (runtimeId == Guid.Empty)
            {
                _logger?.LogWarning("[AbilityManager] Attempted to get ability with empty RuntimeId.");
                return null;
            }
            _abilitiesByRuntimeId.TryGetValue(runtimeId, out var ability);
            if (ability == null)
            {
                // 使用Structured Logging记录未找到的情况，更利于查询
                _logger?.LogDebug("Ability with RuntimeId {AbilityRuntimeId} not found in _abilitiesByRuntimeId dictionary.", runtimeId);
            }
            return ability;
        }

        /// <summary>
        /// Gets a registered system ability instance by its type.
        /// </summary>
        /// <typeparam name="T">The type of the system ability to get.</typeparam>
        /// <returns>The singleton instance of the system ability, or null if not found.</returns>
        public T? GetSystemAbility<T>() where T : class, IAbility
        {
            // System abilities are registered as singletons. We can find them in the _allAbilities list.
            var ability = _allAbilities.OfType<T>().FirstOrDefault();
            if (ability == null)
            {
                _logger?.LogWarning("[AbilityManager] System ability of type {AbilityType} not found.", typeof(T).Name);
            }
            return ability;
        }

        /// <summary>
        /// 更新CardInstance的能力列表（双向绑定核心方法）
        /// </summary>
        /// <param name="cardInstance">要更新的卡牌实例</param>
        private void UpdateCardInstanceAbilities(CardInstance cardInstance)
        {
            if (cardInstance == null) return;

            var abilitiesForCard = GetAbilitiesForCard(cardInstance.InstanceId);
            // CardInstance.SetAbilities expects IReadOnlyList<Ability>.
            // We need to filter only Ability instances and cast them.
            // This assumes that system abilities (not of type Ability) should not be set on CardInstance.Abilities.
            var cardSpecificAbilities = abilitiesForCard.OfType<Ability>().ToList();
            cardInstance.SetAbilities(cardSpecificAbilities.AsReadOnly());

            _logger?.LogDebug("[AbilityManager] Updated abilities for card {CardId}: {Count} abilities",
                cardInstance.InstanceId, cardSpecificAbilities.Count);
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
                EventType.AbilityTriggered => new AbilityTriggeredEventDto(
                    gameEvent.Source?.Player?.PlayerId ?? Guid.Empty,
                    gameEvent.Source?.Card?.InstanceId ?? Guid.Empty,
                    (string)(gameEvent.Parameters.GetValueOrDefault("ability_tag") ?? string.Empty),
                    (string)(gameEvent.Parameters.GetValueOrDefault("trigger_event_type") ?? string.Empty),
                    gameEvent.Parameters
                ),
                _ => null
            };
        }

        /// <summary>
        /// Implements the new interface method to process a single ability.
        /// This centralizes the logic for checking conditions, resolving targets, and creating effects.
        /// </summary>
        public async Task<List<Effect>> ProcessSingleAbilityAsync(IAbility ability, GameContext context)
        {
            if (!await ability.CheckConditionsAsync(context))
            {
                _logger?.LogDebug("[AbilityManager] Ability {AbilityTag} (RuntimeId: {RuntimeId}) conditions not met. Skipping effect creation.",
                    ability.AbilityTag, ability.RuntimeId);
                return new List<Effect>(); // Return empty list if conditions fail
            }

            var resolvedTargets = await ability.ResolveTargetsAsync(context);
            var effects = await ability.CreateEffectsAsync(context, resolvedTargets);

            _logger?.LogDebug("[AbilityManager] Processed ability {AbilityTag} (RuntimeId: {RuntimeId}) and created {EffectCount} effects.",
                ability.AbilityTag, ability.RuntimeId, effects.Count);

            return effects;
        }
    }
} // Closing brace for namespace ShadowverseBeyond.Server.Domain.Abilities