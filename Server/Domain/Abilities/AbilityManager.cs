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
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Dtos.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Exceptions;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants; // Added for IAbility

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// 配置驱动的能力管理器，负责管理和协调所有卡片能力
    /// 基于最新Event驱动架构设计
    /// </summary>
    public class AbilityManager : IAbilityManager
    {
        private readonly ConcurrentDictionary<string, List<IAbility>> _abilitiesByEventType = new();
        private readonly ConcurrentDictionary<Guid, List<IAbility>> _abilitiesByCardId = new();
        private readonly List<IAbility> _allAbilities = new();
        private readonly ConcurrentDictionary<Guid, IAbility> _abilitiesByRuntimeId = new(); // 新增：按RuntimeId索引能力
        private readonly ConcurrentDictionary<Guid, List<Guid>> _abilityRuntimeIdsByBuffId = new(); // 新增：按BuffId索引能力运行时ID
        private readonly ILogger<AbilityManager>? _logger;
        private readonly IAbilityFactory _abilityFactory;
        // Removed: _eventProcessor to break circular dependency
        // Removed: _buffManager, _cardRepository, _functionRegistry

        public AbilityManager(
            ILogger<AbilityManager>? logger,
            IAbilityFactory abilityFactory,
            IEnumerable<IAbility> systemAbilities) // Added systemAbilities
        {
            _logger = logger;
            _abilityFactory = abilityFactory ?? throw new ArgumentNullException(nameof(abilityFactory));

            if (systemAbilities != null)
            {
                foreach (var systemAbility in systemAbilities)
                {
                    RegisterAbility(systemAbility);
                }
            }
        }

        /// <summary>
        /// 从配置创建并注册卡片的所有能力
        /// </summary>
        /// <param name="cardInstance">卡片实例</param>
        /// <param name="abilityConfigs">能力配置列表</param>
        /// <returns>创建的能力列表</returns>
        public List<Ability> CreateAbilitiesForCard(CardInstance cardInstance, List<AbilityConfig> abilityConfigs, Guid? sourceBuffId = null)
        {
            // 1. Use the factory to create the list of abilities, passing the optional sourceBuffId
            var abilities = _abilityFactory.CreateAbilities(cardInstance, abilityConfigs, sourceBuffId);

            // 2. Iterate and register each ability
            foreach (var ability in abilities)
            {
                RegisterAbility(ability);
            }

            _logger?.LogInformation("[AbilityManager] Created and registered {Count} abilities for card {CardName} ({CardId}) from source BuffId {BuffId}. Total Abilities: {TotalCount}",
                abilities.Count, cardInstance.Name, cardInstance.InstanceId, sourceBuffId ?? Guid.Empty, _allAbilities.Count);

            // 3. Return the list of abilities created by the factory
            return abilities;
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
                if (ability.SourceCardId.HasValue)
                {
                    _abilitiesByCardId.AddOrUpdate(
                        ability.SourceCardId.Value,
                        new List<IAbility> { ability },
                        (key, existingList) =>
                        {
                            existingList.Add(ability);
                            return existingList;
                        });
                }

                // 新增：更新CardInstance的能力列表（双向绑定）
                UpdateCardInstanceAbilities(ability.SourceCard);
            }

            // 新增：填充 _abilitiesByRuntimeId 字典
            // IAbility 保证了 RuntimeId 的存在 (Ability.cs 中初始化)
            _abilitiesByRuntimeId.TryAdd(ability.RuntimeId, ability);

            // 新增：如果能力来自Buff，则更新Buff到能力ID的映射
            if (ability is Ability abilityInstance && abilityInstance.SourceBuffId.HasValue)
            {
                var buffId = abilityInstance.SourceBuffId.Value;
                _abilityRuntimeIdsByBuffId.AddOrUpdate(
                    buffId,
                    new List<Guid> { ability.RuntimeId },
                    (key, existingList) =>
                    {
                        existingList.Add(ability.RuntimeId);
                        return existingList;
                    });
            }


            // Use structured logging for better querying
            _logger?.LogDebug("Registered ability {AbilityName} (RuntimeId: {RuntimeId}) for CardId: {CardId}, BuffId: {BuffId}",
                ability.Name, ability.RuntimeId, ability.SourceCardId, ability.SourceBuffId);
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
                if (ability.SourceCardId.HasValue && _abilitiesByCardId.TryGetValue(ability.SourceCardId.Value, out var cardAbilities))
                {
                    _logger?.LogDebug("Before removing {AbilityName}, CardId {CardId} has abilities: {AbilityNames}", ability.Name, ability.SourceCardId.Value, string.Join(", ", cardAbilities.Select(a => a.Name)));
                    cardAbilities.Remove(ability);
                    _logger?.LogDebug("After removing {AbilityName}, CardId {CardId} has abilities: {AbilityNames}", ability.Name, ability.SourceCardId.Value, string.Join(", ", cardAbilities.Select(a => a.Name)));
                    if (cardAbilities.Count == 0)
                    {
                        _logger?.LogDebug("No abilities left for CardId {CardId}. Removing from dictionary.", ability.SourceCardId.Value);
                        _abilitiesByCardId.TryRemove(ability.SourceCardId.Value, out _);
                    }
                }
                // 新增：更新CardInstance的能力列表（双向绑定）
                UpdateCardInstanceAbilities(ability.SourceCard);
            }
            else
            {
                _logger?.LogWarning("Ability {AbilityName} (RuntimeId: {RuntimeId}) has SourceCardId {CardId} but SourceCard object is null. Card-specific ability list will not be updated.",
                    ability.Name, ability.RuntimeId, ability.SourceCardId);
            }

            // 新增：从 _abilitiesByRuntimeId 字典中移除
            _abilitiesByRuntimeId.TryRemove(ability.RuntimeId, out _);

            // 新增：如果能力来自Buff，则从Buff到能力ID的映射中移除
            if (ability is Ability abilityInstance && abilityInstance.SourceBuffId.HasValue)
            {
                var buffId = abilityInstance.SourceBuffId.Value;
                if (_abilityRuntimeIdsByBuffId.TryGetValue(buffId, out var abilityIds))
                {
                    abilityIds.Remove(ability.RuntimeId);
                    if (abilityIds.Count == 0)
                    {
                        _abilityRuntimeIdsByBuffId.TryRemove(buffId, out _);
                    }
                }
            }

            _logger?.LogDebug("Unregistered ability {AbilityName} (RuntimeId: {RuntimeId}) from CardId: {CardId}, BuffId: {BuffId}",
                ability.Name, ability.RuntimeId, ability.SourceCardId, ability.SourceBuffId);
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
        /// 注销由特定Buff赋予的所有能力。
        /// </summary>
        /// <param name="buffId">源Buff的ID。</param>
        public void UnregisterAbilitiesFromBuff(Guid buffId)
        {
            _logger?.LogDebug("Attempting to unregister abilities for BuffId: {BuffId}", buffId);
            if (_abilityRuntimeIdsByBuffId.TryRemove(buffId, out var abilityRuntimeIds))
            {
                _logger?.LogDebug("Found {Count} abilities to unregister for BuffId {BuffId}: {RuntimeIds}",
                    abilityRuntimeIds.Count, buffId, string.Join(", ", abilityRuntimeIds));

                // Create a copy for safe iteration, as UnregisterAbility might modify the source collection indirectly
                var runtimeIdsCopy = abilityRuntimeIds.ToList();

                foreach (var runtimeId in runtimeIdsCopy)
                {
                    if (_abilitiesByRuntimeId.TryGetValue(runtimeId, out var abilityToUnregister))
                    {
                        UnregisterAbility(abilityToUnregister);
                    }
                    else
                    {
                        _logger?.LogWarning("Could not find ability with RuntimeId {RuntimeId} in _abilitiesByRuntimeId dictionary while unregistering for BuffId {BuffId}.", runtimeId, buffId);
                    }
                }
                _logger?.LogInformation("Unregistered {Count} abilities for BuffId {BuffId}", runtimeIdsCopy.Count, buffId);
            }
            else
            {
                _logger?.LogDebug("No abilities found to unregister for BuffId: {BuffId}", buffId);
            }
        }

        /// <summary>
        /// 获取指定事件触发的能力列表
        /// 注意：此方法不再处理事件，只返回相关能力。事件处理应通过EventProcessor进行。
        /// </summary>
        /// <param name="gameEvent">触发的游戏事件</param>
        /// <param name="context">游戏上下文</param>
        /// <returns>触发的能力列表</returns>
        public async Task<List<IAbility>> GetTriggeredAbilitiesAsync(IGameEvent gameEvent, GameContext context, TriggerTiming timing)
        {
            var triggeredAbilities = new List<IAbility>();
            var eventTypeString = gameEvent.EventType.ToString();

            var relevantAbilities = GetAbilitiesForEvent(eventTypeString);

            foreach (var ability in relevantAbilities)
            {
                try
                {
                    // Filter by the specified timing first
                    if (ability.TriggerConfig.Timing == timing && await ability.CanTrigger(gameEvent, context))
                    {
                        triggeredAbilities.Add(ability);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[AbilityManager] Error checking trigger for ability {AbilityTag} (Type: {AbilityType})",
                        ability.AbilityTag, ability.Type);
                }
            }

            if (!triggeredAbilities.Any())
            {
                return triggeredAbilities; // Return empty list early
            }

            // --- NEW SORTING LOGIC ---
            var sortedAbilities = triggeredAbilities
                .OrderBy(ability =>
                {
                    // Rule 1: The event's source ability itself has the highest priority (e.g., Fanfare)
                    if (gameEvent.Source?.Card != null && ability.SourceCard?.InstanceId == gameEvent.Source.Card.InstanceId)
                    {
                        return 0;
                    }
                    // Rule 2: System abilities without a source card have lower priority
                    if (ability.SourceCard == null)
                    {
                        return 100;
                    }
                    // Rule 3: Sort by zone and ownership
                    var card = ability.SourceCard;
                    var activePlayerId = context.CurrentGameState.CurrentPlayerId;
                    bool isMyTurn = card.OwnerPlayerId == activePlayerId;
                    return GameConstants.AbilityTrigger.GetZonePriority(card.CurrentZone, isMyTurn);
                })
                .ToList();

            _logger?.LogDebug("Triggered and sorted {Count} abilities for event {EventType} at timing {Timing}.",
                sortedAbilities.Count, gameEvent.EventType, timing);

            return sortedAbilities;
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
            var result = _abilitiesByCardId.TryGetValue(cardInstanceId, out var abilities)
                ? abilities.ToList() // ToList creates a copy
                : new List<IAbility>();

            _logger?.LogDebug("GetAbilitiesForCard for CardId {CardId} is returning {Count} abilities: {AbilityNames}",
                cardInstanceId, result.Count, string.Join(", ", result.Select(a => a.Name)));

            return result;
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
            _abilityRuntimeIdsByBuffId.Clear(); // 新增：清空BuffId字典

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
            if (cardInstance == null)
            {
                _logger?.LogWarning("UpdateCardInstanceAbilities called with a null cardInstance.");
                return;
            }

            _logger?.LogDebug("Updating abilities for CardInstance {CardName} (InstanceId: {InstanceId})", cardInstance.Name, cardInstance.InstanceId);
            var abilitiesForCard = GetAbilitiesForCard(cardInstance.InstanceId);
            var cardSpecificAbilities = abilitiesForCard.OfType<Ability>().ToList();
            cardInstance.SetAbilities(cardSpecificAbilities.AsReadOnly());
            _logger?.LogDebug("Successfully set {Count} abilities on CardInstance {CardName}: {AbilityNames}",
                cardSpecificAbilities.Count, cardInstance.Name, string.Join(", ", cardSpecificAbilities.Select(a => a.Name)));
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
                    (gameEvent as AbilityTriggeredEvent)?.AbilityTag ?? string.Empty,
                    (gameEvent as AbilityTriggeredEvent)?.TriggeringEvent.EventType.ToString() ?? string.Empty,
                    new Dictionary<string, object>()
                ),
                _ => null
            };
        }

        /// <summary>
        /// Implements the new interface method to process a single ability.
        /// This centralizes the logic for checking conditions, resolving targets, and creating effects.
        /// </summary>
        public async Task<AbilityProcessResult> ProcessSingleAbilityAsync(IAbility ability, GameContext context, bool isCancellable)
        {
            context = context with { IsCancellable = isCancellable };

            // The partialResult logic is now removed. The process is always "start or continue".
            // The state is held within the Target objects themselves.

            // 1. Check initial conditions. This is always done.
            // If an ability was paused, its conditions were already met, but re-checking is safe and handles edge cases.
            if (!ability.CheckConditions(context))
            {
                _logger?.LogDebug("[AbilityManager] Ability {AbilityTag} (RuntimeId: {RuntimeId}) conditions not met. Skipping.",
                    ability.AbilityTag, ability.RuntimeId);
                return AbilityProcessResult.Success(new List<Effect>()); // Return a successful result with no effects.
            }

            // 2. Resolve targets. This method now internally handles resuming from a specific point.
            var targetResult = await ability.ResolveTargetsAsync(context, isCancellable);

            // 3. Handle the result from the target resolution process.
            if (targetResult.Status == ResolutionStatus.Failed)
            {
                _logger?.LogError("[AbilityManager] Target resolution failed for ability {AbilityTag} (RuntimeId: {RuntimeId}): {ErrorMessage}",
                    ability.AbilityTag, ability.RuntimeId, targetResult.ErrorMessage);
                return AbilityProcessResult.Failure(targetResult.ErrorMessage ?? "Target resolution failed.");
            }

            // This block is no longer needed.
            // The new architecture with async/await means that ResolveTargetsAsync will not return
            // a "PendingPlayerChoice" status. Instead, it will internally await the player's
            // choice, and the entire ProcessSingleAbilityAsync task will simply not complete until
            // the choice is made. This naturally pauses the EffectQueueProcessor without any
            // explicit "Paused" state.

            // 4. If resolution is complete, create the effects.
            List<Effect> effects;
            try
            {
                effects = await ability.CreateEffectsAsync(context, targetResult);
            }
            catch (GameLogicException ex)
            {
                _logger?.LogInformation(ex, "[AbilityManager] Ability {AbilityTag} (RuntimeId: {RuntimeId}) aborted by game logic: {Message}",
                    ability.AbilityTag, ability.RuntimeId, ex.Message);
                return AbilityProcessResult.Failure(ex.Message);
            }

            _logger?.LogDebug("[AbilityManager] Processed ability {AbilityTag} (RuntimeId: {RuntimeId}) and created {EffectCount} effects.",
                ability.AbilityTag, ability.RuntimeId, effects.Count);

            return AbilityProcessResult.Success(effects);
        }
    }
} // Closing brace for namespace ShadowverseBeyond.Server.Domain.Abilities
