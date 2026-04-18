//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // Add using for IConditionFactory
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // Add using for IEffectExecutor
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
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
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Ordering;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
using System.Threading;
using ShadowverseBeyond.Server.Services;

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
        private readonly ConcurrentDictionary<Guid, AbilitySnapshot> _abilityTombstones = new();
        private readonly ConcurrentDictionary<Guid, byte> _abilityTriggeredListenerRuntimeIds = new();
        private readonly ConcurrentDictionary<Guid, long> _abilityRegistrationOrders = new();
        private readonly ILogger<AbilityManager>? _logger;
        private readonly IAbilityFactory _abilityFactory;
        private readonly ITriggerOrderingPolicy _triggerOrderingPolicy;
        private long _abilityRegistrationSequence;
        // Removed: _eventProcessor to break circular dependency
        // Removed: _buffManager, _cardRepository, _functionRegistry

        public AbilityManager(
            ILogger<AbilityManager>? logger,
            IAbilityFactory abilityFactory,
            IEnumerable<IAbility> systemAbilities)
            : this(logger, abilityFactory, systemAbilities, new DefaultTriggerOrderingPolicy())
        {
        }

        public AbilityManager(
            ILogger<AbilityManager>? logger,
            IAbilityFactory abilityFactory,
            IEnumerable<IAbility> systemAbilities,
            ITriggerOrderingPolicy triggerOrderingPolicy)
        {
            _logger = logger;
            _abilityFactory = abilityFactory ?? throw new ArgumentNullException(nameof(abilityFactory));
            _triggerOrderingPolicy = triggerOrderingPolicy ?? throw new ArgumentNullException(nameof(triggerOrderingPolicy));

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
        public List<Ability> CreateAbilitiesForCard(
            CardInstance cardInstance,
            List<AbilityConfig> abilityConfigs,
            Guid? sourceBuffId = null)
        {
            return CreateAbilitiesForCard(cardInstance, abilityConfigs, sourceBuffId, null);
        }

        public List<Ability> CreateAbilitiesForCard(
            CardInstance cardInstance,
            List<AbilityConfig> abilityConfigs,
            Guid? sourceBuffId,
            GameState? gameState)
        {
            InitializeCountersFromConfigs(cardInstance, abilityConfigs, gameState);

            // 1. Use the factory to create the list of abilities, passing the optional sourceBuffId
            var abilities = _abilityFactory.CreateAbilities(cardInstance, abilityConfigs, sourceBuffId);

            // 2. Iterate and register each ability
            foreach (var ability in abilities)
            {
                RegisterAbility(ability);
            }

            // _logger?.LogInformation("[AbilityManager] Created and registered {Count} abilities for card {CardName} ({CardId}) from source BuffId {BuffId}. Total Abilities: {TotalCount}",
            //     abilities.Count, cardInstance.Name, cardInstance.InstanceId, sourceBuffId ?? Guid.Empty, _allAbilities.Count);

            // 3. Return the list of abilities created by the factory
            return abilities;
        }

        private static void InitializeCountersFromConfigs(
            CardInstance cardInstance,
            IEnumerable<AbilityConfig> abilityConfigs,
            GameState? gameState)
        {
            if (cardInstance == null || abilityConfigs == null)
            {
                return;
            }

            foreach (var config in abilityConfigs)
            {
                if (config == null)
                {
                    continue;
                }

                if (config.CounterRegistrations != null && config.CounterRegistrations.Count > 0)
                {
                    foreach (var registration in config.CounterRegistrations)
                    {
                        if (registration == null || string.IsNullOrWhiteSpace(registration.Name))
                        {
                            continue;
                        }

                        cardInstance.Counters.TryInitialize(registration.Name, registration.InitialValue);
                    }
                }

                if (gameState == null || config.PlayerCounterRegistrations == null || config.PlayerCounterRegistrations.Count == 0)
                {
                    continue;
                }

                foreach (var registration in config.PlayerCounterRegistrations)
                {
                    if (registration == null || string.IsNullOrWhiteSpace(registration.Name))
                    {
                        continue;
                    }

                    var players = PlayerScopeResolver.ResolvePlayers(gameState, cardInstance.OwnerPlayerId, registration.PlayerScope);
                    foreach (var player in players)
                    {
                        player.Counters.TryInitialize(registration.Name, registration.InitialValue);
                    }
                }
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
                _logger?.LogWarning("[AbilityManager] Attempted to register a card ability (Type: Ability) without a SourceCard. Keywords: {Keywords}. Registration skipped.", ability.KeywordSummary);
                return;
            }


            _allAbilities.Add(ability);
            _abilityRegistrationOrders.TryAdd(ability.RuntimeId, Interlocked.Increment(ref _abilityRegistrationSequence));
            var indexedEventTypes = GetAbilityEventTypesForIndexing(ability).ToList();
            if (indexedEventTypes.Contains(EventType.AbilityTriggered))
            {
                _abilityTriggeredListenerRuntimeIds[ability.RuntimeId] = 0;
            }

            // 按事件类型索引能力
            // Ensure TriggerConfig is used for event types
            foreach (var eventType in indexedEventTypes)
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
        public void UnregisterAbility(IAbility ability, string removalReason = "unregistered")
        {
            if (ability == null) return;
            // Allow system abilities without SourceCard to be unregistered from _allAbilities
            // but SourceCard specific logic should only run if SourceCard exists.

            RegisterAbilityTombstone(ability, removalReason);

            _allAbilities.Remove(ability);
            _abilityTriggeredListenerRuntimeIds.TryRemove(ability.RuntimeId, out _);
            _abilityRegistrationOrders.TryRemove(ability.RuntimeId, out _);

            // 从事件类型索引中移除
            // Ensure TriggerConfig is used for event types
            foreach (var eventType in GetAbilityEventTypesForIndexing(ability))
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
        public void UnregisterCardAbilities(Guid cardInstanceId, string removalReason = "card_unregistered")
        {
            if (_abilitiesByCardId.TryGetValue(cardInstanceId, out var abilities))
            {
                var abilitiesToRemove = abilities.ToList();
                CardInstance? cardInstance = null;

                foreach (var ability in abilitiesToRemove)
                {
                    if (cardInstance == null)
                        cardInstance = ability.SourceCard; // 获取CardInstance引用

                    UnregisterAbility(ability, removalReason);
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
        public void UnregisterAbilitiesFromBuff(Guid buffId, string removalReason = "buff_removed")
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
                        UnregisterAbility(abilityToUnregister, removalReason);
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

        public IReadOnlyList<Guid> GetAbilityRuntimeIdsForBuff(Guid buffId)
        {
            if (buffId == Guid.Empty)
            {
                return Array.Empty<Guid>();
            }

            return _abilityRuntimeIdsByBuffId.TryGetValue(buffId, out var abilityRuntimeIds)
                ? abilityRuntimeIds.ToList()
                : Array.Empty<Guid>();
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

            // NOTE: Do not hard-filter abilities by a single runtime ability id here.
            // Let DSL conditions decide whether a specific ability should trigger.
            // if (targetedAbilityRuntimeId.HasValue && targetedAbilityRuntimeId.Value != Guid.Empty)
            // {
            //     relevantAbilities = relevantAbilities
            //         .Where(ability => ability.RuntimeId == targetedAbilityRuntimeId.Value)
            //         .ToList();
            // }

            foreach (var ability in relevantAbilities)
            {
                try
                {
                    if (IsAbilitySuppressed(ability, context))
                    {
                        continue;
                    }

                    if (ability is Ability concreteAbility)
                    {
                        if (IsAuraAbility(concreteAbility))
                        {
                            // Aura refresh is unified into After timing.
                            // TriggerConfig timing is ignored for aura matching so legacy configs remain compatible.
                            if (timing == TriggerTiming.After
                                && !context.IsAuraRefresh
                                && ShouldTriggerAuraAbility(concreteAbility, gameEvent))
                            {
                                triggeredAbilities.Add(ability);
                            }
                            continue;
                        }
                    }

                    // Filter by the specified timing first
                    if (ability.TriggerConfig.Timing == timing && await ability.CanTrigger(gameEvent, context))
                    {
                        triggeredAbilities.Add(ability);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[AbilityManager] Error checking trigger for ability {AbilityKeywords} (Type: {AbilityType})",
                        ability.KeywordSummary, ability.Type);
                }
            }

            if (!triggeredAbilities.Any())
            {
                return triggeredAbilities; // Return empty list early
            }

            var orderedAbilities = _triggerOrderingPolicy.OrderTriggeredAbilities(
                triggeredAbilities,
                gameEvent,
                context,
                _abilityRegistrationOrders);

            LogTriggerOrdering(gameEvent, timing, orderedAbilities);

            return orderedAbilities.Select(item => item.Ability).ToList();
        }

        private void LogTriggerOrdering(IGameEvent gameEvent, TriggerTiming timing, IReadOnlyList<OrderedTriggeredAbility> orderedAbilities)
        {
            if (_logger == null || !_logger.IsEnabled(LogLevel.Debug) || orderedAbilities.Count == 0)
            {
                return;
            }

            var orderingDetails = string.Join(" | ", orderedAbilities.Select(item =>
                $"{item.Ability.KeywordSummary}({item.Ability.RuntimeId})[{item.Key}]"));

            _logger.LogDebug(
                "Triggered and sorted {Count} abilities for event {EventType} at timing {Timing}. Order: {Order}",
                orderedAbilities.Count,
                gameEvent.EventType,
                timing,
                orderingDetails);
        }

        private static bool IsAuraAbility(Ability ability)
        {
            return ability.Type == AbilityType.Continuous;
        }

        private static IEnumerable<EventType> GetAbilityEventTypesForIndexing(IAbility ability)
        {
            var eventTypes = new HashSet<EventType>();
            if (ability.TriggerConfig?.EventTypes != null)
            {
                foreach (var eventType in ability.TriggerConfig.EventTypes)
                {
                    eventTypes.Add(eventType);
                }
            }

            if (ability is Ability concreteAbility && IsAuraAbility(concreteAbility) && concreteAbility.Config.Auras.Any())
            {
                foreach (var auraConfig in concreteAbility.Config.Auras)
                {
                    if (auraConfig == null || !auraConfig.IsEnabled)
                    {
                        continue;
                    }

                    foreach (var trigger in GetAuraTriggerConfigs(concreteAbility, auraConfig))
                    {
                        if (trigger?.EventTypes == null)
                        {
                            continue;
                        }

                        foreach (var eventType in trigger.EventTypes)
                        {
                            eventTypes.Add(eventType);
                        }
                    }
                }
            }

            return eventTypes;
        }

        private static IEnumerable<TriggerConfig> GetAuraTriggerConfigs(Ability ability, AuraConfig auraConfig)
        {
            return new[] { ability.Config.Trigger };
        }

        private static bool ShouldTriggerAuraAbility(Ability ability, IGameEvent gameEvent)
        {
            foreach (var auraConfig in ability.Config.Auras)
            {
                if (auraConfig == null || !auraConfig.IsEnabled)
                {
                    continue;
                }

                foreach (var trigger in GetAuraTriggerConfigs(ability, auraConfig))
                {
                    if (TriggerConfigMatchesEvent(
                        trigger,
                        gameEvent,
                        TriggerTiming.After,
                        ability.SourceCard,
                        ignoreTiming: true))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TriggerConfigMatchesEvent(
            TriggerConfig trigger,
            IGameEvent gameEvent,
            TriggerTiming expectedTiming,
            CardInstance? sourceCard,
            bool ignoreTiming = false)
        {
            if (trigger == null || !trigger.IsEnabled)
            {
                return false;
            }

            if (!ignoreTiming && trigger.Timing != expectedTiming)
            {
                return false;
            }

            if (trigger.EventTypes == null || trigger.EventTypes.Count == 0)
            {
                return false;
            }

            if (!trigger.EventTypes.Contains(gameEvent.EventType))
            {
                return false;
            }

            if (sourceCard != null && !trigger.IsValidZone(sourceCard.CurrentZone))
            {
                return false;
            }

            if (!IsRelationMatch(trigger.RelationType, gameEvent, sourceCard))
            {
                return false;
            }

            return true;
        }

        private static bool IsRelationMatch(TriggerRelationType relationType, IGameEvent gameEvent, CardInstance? sourceCard)
        {
            if (relationType == TriggerRelationType.Any)
            {
                return true;
            }

            if (sourceCard == null)
            {
                return relationType != TriggerRelationType.SourceSelf
                       && relationType != TriggerRelationType.TargetSelf;
            }

            var sourceEventCard = gameEvent.Source?.Card;
            var targetCards = gameEvent.Target
                .Select(target => target.Card)
                .Where(card => card != null)
                .Select(card => card!)
                .ToList();

            return relationType switch
            {
                TriggerRelationType.SourceSelf => sourceEventCard != null && sourceEventCard.InstanceId == sourceCard.InstanceId,
                TriggerRelationType.SourceOther => sourceEventCard != null && sourceEventCard.InstanceId != sourceCard.InstanceId,
                TriggerRelationType.TargetSelf => targetCards.Any(card => card.InstanceId == sourceCard.InstanceId),
                TriggerRelationType.TargetOther => targetCards.Any(card => card.InstanceId != sourceCard.InstanceId),
                _ => true
            };
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
        /// 获取指定卡片的所有有效能力（不包含被抑制的能力）
        /// </summary>
        public List<IAbility> GetEffectiveAbilitiesForCard(Guid cardInstanceId, GameContext context)
        {
            var abilities = GetAbilitiesForCard(cardInstanceId);
            if (abilities.Count == 0)
            {
                return abilities;
            }

            var buffs = context.BuffManager.GetBuffsForCard(cardInstanceId);
            if (buffs.Count == 0)
            {
                return abilities;
            }

            var buffOrderById = BuildBuffOrderLookup(buffs);
            return abilities
                .Where(ability => !IsAbilitySuppressedByBuffs(ability, buffs, buffOrderById))
                .ToList();
        }

        /// <summary>
        /// 判断能力是否在当前上下文中被抑制
        /// </summary>
        public bool IsAbilitySuppressed(IAbility ability, GameContext context)
        {
            if (ability == null || context == null)
            {
                return false;
            }

            if (!ability.SourceCardId.HasValue)
            {
                return false;
            }

            var buffs = context.BuffManager.GetBuffsForCard(ability.SourceCardId.Value);
            if (buffs.Count == 0)
            {
                return false;
            }

            var buffOrderById = BuildBuffOrderLookup(buffs);
            return IsAbilitySuppressedByBuffs(ability, buffs, buffOrderById);
        }

        private static bool IsAbilitySuppressedByBuffs(
            IAbility ability,
            IReadOnlyList<Buff> orderedBuffs,
            IReadOnlyDictionary<Guid, int> buffOrderById)
        {
            var abilityOriginOrder = ResolveAbilityOriginOrder(ability, buffOrderById);

            foreach (var buff in orderedBuffs)
            {
                if (!buff.IsActive)
                {
                    continue;
                }

                if (!buffOrderById.TryGetValue(buff.Id, out var suppressionOrder))
                {
                    continue;
                }

                // Suppression only affects abilities that already existed when this suppression buff was applied.
                if (suppressionOrder <= abilityOriginOrder)
                {
                    continue;
                }

                var suppressionEffects = buff.Effects.OfType<AbilitySuppressionBuffEffect>();
                foreach (var suppressionEffect in suppressionEffects)
                {
                    if (AbilitySuppressionEvaluator.Matches(ability, suppressionEffect))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Dictionary<Guid, int> BuildBuffOrderLookup(IReadOnlyList<Buff> orderedBuffs)
        {
            var lookup = new Dictionary<Guid, int>(orderedBuffs.Count);
            for (var index = 0; index < orderedBuffs.Count; index++)
            {
                lookup[orderedBuffs[index].Id] = index + 1;
            }

            return lookup;
        }

        private static int ResolveAbilityOriginOrder(IAbility ability, IReadOnlyDictionary<Guid, int> buffOrderById)
        {
            if (!ability.SourceBuffId.HasValue)
            {
                // Base/system ability: exists before all buffs.
                return 0;
            }

            if (buffOrderById.TryGetValue(ability.SourceBuffId.Value, out var originOrder))
            {
                return originOrder;
            }

            // Unknown origin buff: treat as latest to avoid suppressing by older snapshots.
            return int.MaxValue;
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
            _abilityTombstones.Clear();
            _abilityTriggeredListenerRuntimeIds.Clear();
            _abilityRegistrationOrders.Clear();
            _abilityRegistrationSequence = 0;

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

        public bool TryGetAbilitySnapshot(Guid runtimeId, out AbilitySnapshot snapshot)
        {
            return _abilityTombstones.TryGetValue(runtimeId, out snapshot);
        }

        public void ClearAbilityTombstones()
        {
            _abilityTombstones.Clear();
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

        public bool HasAbilityTriggeredListeners()
        {
            return !_abilityTriggeredListenerRuntimeIds.IsEmpty;
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

        private void RegisterAbilityTombstone(IAbility ability, string removalReason)
        {
            if (ability.RuntimeId == Guid.Empty)
            {
                return;
            }

            var concreteAbility = ability as Ability;
            var invalidationVersion = concreteAbility?.QueuedEffectStateVersion ?? 0;
            var invalidationReason = concreteAbility?.LastQueuedEffectInvalidationReason;
            var wasSuppressedWhenUnregistered = invalidationVersion > 0;
            var sourceCard = ability.SourceCard;
            var snapshot = new AbilitySnapshot(
                ability.RuntimeId,
                ability.SourceCardId,
                sourceCard?.Name,
                ability.Name,
                ability.DefinitionId,
                ability.SourceBuffId,
                ability.Keywords.ToList(),
                ability.KeywordSummary,
                string.IsNullOrWhiteSpace(removalReason) ? "unregistered" : removalReason,
                wasSuppressedWhenUnregistered,
                wasSuppressedWhenUnregistered,
                concreteAbility?.BuffDisplay ?? true,
                invalidationVersion,
                invalidationReason);

            _abilityTombstones[ability.RuntimeId] = snapshot;
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
        /// Resets per-event trigger flags for all registered abilities.
        /// </summary>
        public void ResetEventFlagsForAllAbilities()
        {
            foreach (var ability in _allAbilities.OfType<Ability>().ToList())
            {
                ability.Trigger.ResetEventFlags();
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
            var abilityTriggeredEvent = gameEvent as AbilityTriggeredEvent;
            var triggeredAbility = abilityTriggeredEvent?.TriggeredAbility;
            var concreteAbility = triggeredAbility as Ability;
            var sourceCard = triggeredAbility?.SourceCard;
            var abilityName = concreteAbility?.Name ?? triggeredAbility?.Name ?? string.Empty;
            var abilityDescription = concreteAbility?.Description ?? string.Empty;
            var parameters = new Dictionary<string, object>
            {
                { "ability_definition_id", triggeredAbility?.DefinitionId ?? 0L },
                { "ability_runtime_id", triggeredAbility?.RuntimeId ?? Guid.Empty },
                { "ability_name", abilityName },
                { "ability_description", abilityDescription },
                { "source_card_name", sourceCard?.Name ?? string.Empty },
                { "source_card_art_preview", sourceCard?.CurrentDefinition?.ArtPreview ?? string.Empty }
            };

            return gameEvent.EventType switch
            {
                EventType.AbilityTriggered => new AbilityTriggeredEventDto(
                    gameEvent.Source?.Player?.PlayerId ?? Guid.Empty,
                    gameEvent.Source?.Card?.InstanceId ?? Guid.Empty,
                    abilityTriggeredEvent?.TriggerEventType.ToString() ?? string.Empty,
                    parameters,
                    abilityTriggeredEvent?.AbilityKeywords.Select(k => k.ToString()).ToArray() ?? Array.Empty<string>()
                ),
                _ => null
            };
        }

        /// <summary>
        /// Implements the new interface method to process a single ability.
        /// This centralizes the logic for checking conditions, resolving targets, and creating effects.
        /// </summary>
        public async Task<AbilityProcessResult> ProcessSingleAbilityAsync(IAbility ability, GameContext context, bool isCancellable, bool deferTargetResolution, bool recordTrigger = true)
        {
            context = context with { IsCancellable = isCancellable, SourceAbility = ability };

            var auraEvents = new List<IGameEvent>();
            var concreteAbility = ability as Ability;
            var auraConfigs = concreteAbility?.Config.Auras ?? new List<AuraConfig>();
            var isAuraAbility = concreteAbility != null && IsAuraAbility(concreteAbility);
            var runsAura = !isCancellable
                           && isAuraAbility
                           && auraConfigs.Any(a => a != null && a.IsEnabled);
            var runsEffects = !isAuraAbility;

            // The partialResult logic is now removed. The process is always "start or continue".
            // The state is held within the Target objects themselves.

            // 1. Check initial conditions. This is always done.
            // If an ability was paused, its conditions were already met, but re-checking is safe and handles edge cases.
            var needsAbilityConditions = runsEffects || runsAura;
            var abilityConditionsPassed = true;
            if (needsAbilityConditions)
            {
                abilityConditionsPassed = ability.CheckConditions(context);
            }

            if (runsEffects && !abilityConditionsPassed && !runsAura)
            {
                _logger?.LogDebug("[AbilityManager] Ability {AbilityKeywords} (RuntimeId: {RuntimeId}) conditions not met. Skipping.",
                    ability.KeywordSummary, ability.RuntimeId);
                return AbilityProcessResult.Success(new List<Effect>(), auraEvents); // Return a successful result with no effects.
            }

            var hasPlayerChoice = ability.Targets.Any(t => t.Config.PlayerChoiceOptions?.IsRequired == true);
            var shouldDeferResolution = deferTargetResolution && !hasPlayerChoice && !runsAura;

            TargetResolutionResult targetResult;
            var needsAbilityTargets = (runsEffects && abilityConditionsPassed) || runsAura;
            if (!needsAbilityTargets)
            {
                targetResult = new TargetResolutionResult
                {
                    Status = ResolutionStatus.Completed,
                    ResolvedTargets = new Dictionary<string, List<TargetInfo>>()
                };
            }
            else if (shouldDeferResolution)
            {
                foreach (var target in ability.Targets)
                {
                    target.MarkForResolution();
                }

                targetResult = new TargetResolutionResult
                {
                    Status = ResolutionStatus.Completed,
                    ResolvedTargets = new Dictionary<string, List<TargetInfo>>()
                };

                _logger?.LogDebug("[AbilityManager] Deferring target resolution for ability {AbilityKeywords} (RuntimeId: {RuntimeId}).",
                    ability.KeywordSummary, ability.RuntimeId);
            }
            else
            {
                // 2. Resolve targets. This method now internally handles resuming from a specific point.
                targetResult = await ability.ResolveTargetsAsync(context, isCancellable);

                // 3. Handle the result from the target resolution process.
                if (targetResult.Status == ResolutionStatus.Failed)
                {
                    _logger?.LogError("[AbilityManager] Target resolution failed for ability {AbilityKeywords} (RuntimeId: {RuntimeId}): {ErrorMessage}",
                        ability.KeywordSummary, ability.RuntimeId, targetResult.ErrorMessage);
                    if (targetResult.WasCancelled)
                    {
                        return AbilityProcessResult.Cancelled(targetResult.ErrorMessage ?? "Target resolution cancelled.");
                    }
                    return AbilityProcessResult.Failure(targetResult.ErrorMessage ?? "Target resolution failed.");
                }

                if (targetResult.Status == ResolutionStatus.Skipped)
                {
                    _logger?.LogDebug(
                        "[AbilityManager] Target resolution skipped ability execution for {AbilityKeywords} (RuntimeId: {RuntimeId}): {Reason}",
                        ability.KeywordSummary,
                        ability.RuntimeId,
                        targetResult.ErrorMessage);
                    return AbilityProcessResult.Success(new List<Effect>(), auraEvents);
                }
            }

            // This block is no longer needed.
            // The new architecture with async/await means that ResolveTargetsAsync will not return
            // a "PendingPlayerChoice" status. Instead, it will internally await the player's
            // choice, and the entire ProcessSingleAbilityAsync task will simply not complete until
            // the choice is made. This naturally pauses the EffectQueueProcessor without any
            // explicit "Paused" state.

            // 4. If resolution is complete, create the effects.
            List<Effect> effects = new();
            if (runsEffects && abilityConditionsPassed)
            {
                try
                {
                    effects = await ability.CreateEffectsAsync(context, targetResult);
                }
                catch (GameLogicException ex)
                {
                    _logger?.LogInformation(ex, "[AbilityManager] Ability {AbilityKeywords} (RuntimeId: {RuntimeId}) aborted by game logic: {Message}",
                        ability.KeywordSummary, ability.RuntimeId, ex.Message);
                    return AbilityProcessResult.Failure(ex.Message);
                }

                // Record a successful trigger after effects are created (optional for pre-analysis).
                if (recordTrigger)
                {
                    ability.RecordTrigger(context);
                    if (ShouldEmitAbilityTriggeredEvent(ability, context))
                    {
                        auraEvents.Add(new AbilityTriggeredEvent(ability, context.TriggeringEvent!));
                    }
                }
            }

            if (effects.Count > 0 && shouldDeferResolution && ability.Targets.Any(t => t.Config.IsEnabled))
            {
                foreach (var effect in effects)
                {
                    effect.ResolveTargetsOnExecution = true;
                    effect.SourceAbility = ability;
                }
            }

            if (runsAura && concreteAbility != null)
            {
                var conditionEvaluator = context.ServiceProvider.GetRequiredService<IConditionEvaluator>();

                for (var index = 0; index < auraConfigs.Count; index++)
                {
                    var auraConfig = auraConfigs[index];
                    if (auraConfig == null || !auraConfig.IsEnabled)
                    {
                        continue;
                    }

                    var auraConditionsPassed = abilityConditionsPassed;

                    if (!auraConditionsPassed)
                    {
                        var clearResult = context.AuraManager.ClearAura(auraConfig, ability, context, index);
                        if (!clearResult.Success)
                        {
                            return AbilityProcessResult.Failure(clearResult.ErrorMessage ?? "Aura clear failed.");
                        }
                        auraEvents.AddRange(clearResult.GeneratedEvents);
                        continue;
                    }

                    IReadOnlyDictionary<string, List<TargetInfo>> auraTargets = targetResult.ResolvedTargets;

                    var refreshResult = context.AuraManager.RefreshAura(auraConfig, ability, context, index, auraTargets);
                    if (!refreshResult.Success)
                    {
                        return AbilityProcessResult.Failure(refreshResult.ErrorMessage ?? "Aura refresh failed.");
                    }
                    auraEvents.AddRange(refreshResult.GeneratedEvents);
                }
            }

            _logger?.LogDebug("[AbilityManager] Processed ability {AbilityKeywords} (RuntimeId: {RuntimeId}) and created {EffectCount} effects.",
                ability.KeywordSummary, ability.RuntimeId, effects.Count);


            return AbilityProcessResult.Success(effects, auraEvents);
        }

        private static bool ShouldEmitAbilityTriggeredEvent(IAbility ability, GameContext context)
        {
            if (context.TriggeringEvent == null)
            {
                return false;
            }

            if (ability is Ability concreteAbility)
            {
                return concreteAbility.BroadcastOnTrigger;
            }

            return true;
        }

        public DomainOperationResult RefreshAuraForAbility(IAbility ability, GameContext context)
        {
            if (ability is not Ability concreteAbility || context == null)
            {
                return new DomainOperationResult { Success = true };
            }

            if (!IsAuraAbility(concreteAbility) || concreteAbility.Config.Auras.All(aura => aura == null || !aura.IsEnabled))
            {
                return new DomainOperationResult { Success = true };
            }

            if (concreteAbility.Targets.Any(t => t.Config.PlayerChoiceOptions?.IsRequired == true))
            {
                _logger?.LogDebug("[AbilityManager] Skipping aura refresh for ability {AbilityKeywords} (RuntimeId: {RuntimeId}) because it requires player choice targets.",
                    ability.KeywordSummary, ability.RuntimeId);
                return new DomainOperationResult { Success = true };
            }

            var auraContext = context with
            {
                IsAuraRefresh = true,
                IsCancellable = false,
                SourceCard = concreteAbility.SourceCard ?? context.SourceCard,
                SourceAbility = ability
            };

            try
            {
                var refreshResult = ProcessSingleAbilityAsync(concreteAbility, auraContext, false, false)
                    .GetAwaiter()
                    .GetResult();

                if (!refreshResult.IsSuccess)
                {
                    return new DomainOperationResult
                    {
                        Success = false,
                        ErrorMessage = refreshResult.ErrorMessage
                    };
                }

                var result = new DomainOperationResult { Success = true };
                result.GeneratedEvents.AddRange(refreshResult.GeneratedEvents);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[AbilityManager] Failed to refresh aura for ability {AbilityKeywords} (RuntimeId: {RuntimeId}).",
                    ability.KeywordSummary, ability.RuntimeId);
                return new DomainOperationResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
} // Closing brace for namespace ShadowverseBeyond.Server.Domain.Abilities
