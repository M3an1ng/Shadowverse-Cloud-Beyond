//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 错误处理与日志系统: 文档/05-游戏系统/08-错误处理与日志系统.md
//
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 统一的Event处理器，负责处理Event链和能力触发
    /// 基于最新Event驱动架构设计
    /// </summary>
    public class EventProcessor
    {
        private readonly Queue<IGameEvent> _eventQueue = new();
        private readonly List<Ability> _activeAbilities = new(); // Use runtime Ability objects
        private readonly ILogger<EventProcessor> _logger;
        private readonly AbilityManager _abilityManager;

        public EventProcessor(ILogger<EventProcessor> logger, AbilityManager abilityManager)
        {
            _logger = logger;
            _abilityManager = abilityManager;
        }

        /// <summary>
        /// 处理初始事件并返回所有生成的事件DTO列表
        /// </summary>
        /// <param name="initialEvent">初始触发事件</param>
        /// <param name="context">游戏上下文</param>
        /// <returns>处理完成的事件DTO列表</returns>
        public async Task<List<GameEventDto>> ProcessEventAsync(IGameEvent initialEvent, GameContext context)
        {
            var processedEvents = new List<GameEventDto>();
            _eventQueue.Clear();
            _eventQueue.Enqueue(initialEvent);

            _logger.LogDebug("[EventProcessor] Starting event processing with initial event: {EventType}",
                initialEvent.EventType);

            while (_eventQueue.Count > 0)
            {
                var currentEvent = _eventQueue.Dequeue();

                try
                {
                    _logger.LogDebug("[EventProcessor] Processing event: {EventType} (ID: {EventId})",
                        currentEvent.EventType, currentEvent.EventId);

                    // 1. 执行Event本身
                    var newEvents = await currentEvent.ExecuteAsync(context);

                    // 2. 转换为DTO并记录
                    var eventDto = ConvertEventToDto(currentEvent);
                    if (eventDto != null)
                    {
                        processedEvents.Add(eventDto);
                        _logger.LogDebug("[EventProcessor] Event converted to DTO: {EventType}", eventDto.Type);
                    }

                    // 3. 检查是否触发能力
                    var triggeredAbilities = await GetTriggeredAbilitiesAsync(currentEvent, context);
                    _logger.LogDebug("[EventProcessor] Found {Count} triggered abilities for event {EventType}",
                        triggeredAbilities.Count, currentEvent.EventType);

                    // 4. 执行触发的能力，产生新Event
                    foreach (var ability in triggeredAbilities)
                    {
                        try
                        {
                            var abilityEvents = await ProcessAbilityAsync(ability, currentEvent, context);
                            foreach (var abilityEvent in abilityEvents)
                            {
                                _eventQueue.Enqueue(abilityEvent);
                            }

                            // 播报能力触发事件
                            var abilityTriggeredEvent = new AbilityTriggeredEvent(ability, currentEvent);
                            _eventQueue.Enqueue(abilityTriggeredEvent);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[EventProcessor] Error processing ability {AbilityTag} for event {EventType}",
                                ability.AbilityTag, currentEvent.EventType);
                        }
                    }

                    // 5. 将Event本身产生的新Event加入队列
                    foreach (var newEvent in newEvents)
                    {
                        _eventQueue.Enqueue(newEvent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[EventProcessor] Error processing event {EventType} (ID: {EventId})",
                        currentEvent.EventType, currentEvent.EventId);
                }
            }

            _logger.LogDebug("[EventProcessor] Event processing completed. Generated {Count} events",
                processedEvents.Count);

            return processedEvents;
        }

        /// <summary>
        /// 注册激活的能力
        /// </summary>
        /// <param name="ability">要注册的能力</param>
        public void RegisterAbility(Ability ability)
        {
            // Runtime Ability objects don't have IsActive property, they are active by being registered
            if (!_activeAbilities.Contains(ability))
            {
                _activeAbilities.Add(ability);
                _logger.LogDebug("[EventProcessor] Registered ability: {AbilityTag} for card {CardId}",
                    ability.AbilityTag, ability.CardId);
            }
        }

        /// <summary>
        /// 注销能力
        /// </summary>
        /// <param name="ability">要注销的能力</param>
        public void UnregisterAbility(Ability ability)
        {
            if (_activeAbilities.Remove(ability))
            {
                _logger.LogDebug("[EventProcessor] Unregistered ability: {AbilityTag} for card {CardId}",
                    ability.AbilityTag, ability.CardId);
            }
        }

        /// <summary>
        /// 清除所有注册的能力
        /// </summary>
        public void ClearAbilities()
        {
            var count = _activeAbilities.Count;
            _activeAbilities.Clear();
            _logger.LogDebug("[EventProcessor] Cleared {Count} abilities", count);
        }

        /// <summary>
        /// 获取被触发的能力列表
        /// </summary>
        private async Task<List<Ability>> GetTriggeredAbilitiesAsync(IGameEvent gameEvent, GameContext context)
        {
            var triggeredAbilities = new List<Ability>();

            foreach (var ability in _activeAbilities)
            {
                // Use the new runtime Ability.CanTrigger method
                if (ability.CanTrigger(gameEvent))
                {
                    if (await ShouldTriggerAbilityAsync(ability, gameEvent, context))
                    {
                        triggeredAbilities.Add(ability);
                    }
                }
            }

            // 按优先级排序 - Priority handling can be added later if needed

            return triggeredAbilities;
        }

        /// <summary>
        /// 检查能力是否应该触发
        /// </summary>
        private async Task<bool> ShouldTriggerAbilityAsync(Ability ability, IGameEvent triggerEvent, GameContext context)
        {
            try
            {
                // 使用新的运行时Ability方法检查条件
                var conditionContext = new ConditionEvaluationContext
                {
                    SourceCard = ability.SourceCard,
                    TriggeringEvent = triggerEvent,
                    // TODO: Convert GameStateDto to GameState or update context types
                    GameState = null // Temporarily null until we resolve the type mismatch
                };

                return await ability.CheckConditionsAsync(conditionContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EventProcessor] Error checking if ability {AbilityTag} should trigger",
                    ability.AbilityTag);
                return false;
            }
        }



        /// <summary>
        /// 处理能力触发
        /// </summary>
        private async Task<List<IGameEvent>> ProcessAbilityAsync(Ability ability, IGameEvent triggerEvent, GameContext context)
        {
            try
            {
                // 记录触发
                ability.RecordTrigger();

                // 解析目标
                var targetContext = new TargetResolutionContext
                {
                    SourceCard = ability.SourceCard,
                    TriggeringEvent = triggerEvent,
                    // TODO: Convert GameStateDto to GameState or update context types
                    GameState = null // Temporarily null until we resolve the type mismatch
                };

                var targets = await ability.ResolveTargetsAsync(targetContext);

                // 执行效果
                var effectContext = new EffectExecutionContext
                {
                    SourceCard = ability.SourceCard,
                    TriggeringEvent = triggerEvent,
                    SelectedTargets = targets,
                    // TODO: Convert GameStateDto to GameState or update context types
                    GameState = null // Temporarily null until we resolve the type mismatch
                };

                var generatedEvents = await ability.ExecuteEffectsAsync(effectContext);

                _logger.LogDebug("[EventProcessor] Ability {AbilityTag} generated {Count} events",
                    ability.AbilityTag, generatedEvents.Count);

                return generatedEvents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EventProcessor] Error processing ability {AbilityTag}", ability.AbilityTag);
                return new List<IGameEvent>();
            }
        }



        /// <summary>
        /// 将Event转换为DTO
        /// </summary>
        private GameEventDto? ConvertEventToDto(IGameEvent gameEvent)
        {
            try
            {
                return gameEvent.EventType switch
                {
                    "play_card" => ConvertPlayCardEvent(gameEvent),
                    "summon" => ConvertSummonEvent(gameEvent),
                    "damage" => ConvertDamageEvent(gameEvent),
                    "card_destroyed" => ConvertCardDestroyedEvent(gameEvent),
                    "evolve" => ConvertEvolveEvent(gameEvent),
                    "draw" => ConvertDrawEvent(gameEvent),
                    "turn_start" => ConvertTurnStartEvent(gameEvent),
                    "turn_end" => ConvertTurnEndEvent(gameEvent),
                    "ability_triggered" => ConvertAbilityTriggeredEvent(gameEvent),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EventProcessor] Error converting event {EventType} to DTO", gameEvent.EventType);
                return null;
            }
        }

        // DTO转换方法（简化实现）
        private GameEventDto ConvertPlayCardEvent(IGameEvent gameEvent)
        {
            return new CardPlayedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? string.Empty,
                gameEvent.SourceCard?.ToDto() ?? new CardInstanceDto()
            );
        }

        private GameEventDto ConvertSummonEvent(IGameEvent gameEvent)
        {
            return new CardSummonedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? string.Empty,
                gameEvent.SourceCard?.ToDto() ?? new CardInstanceDto(),
                "Played"
            );
        }

        private GameEventDto ConvertDamageEvent(IGameEvent gameEvent)
        {
            var target = gameEvent.Parameters.GetValueOrDefault("target") as CardInstance;
            var damage = (int)(gameEvent.Parameters.GetValueOrDefault("damage") ?? 0);

            return new DamageDealtEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? string.Empty,
                target?.InstanceId ?? string.Empty,
                damage,
                target?.OwnerPlayerId ?? string.Empty,
                gameEvent.SourceCard?.InstanceId,
                "Effect"
            );
        }

        private GameEventDto ConvertCardDestroyedEvent(IGameEvent gameEvent)
        {
            return new UnitDestroyedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? string.Empty,
                gameEvent.SourceCard?.InstanceId ?? string.Empty,
                gameEvent.SourceCard?.OwnerPlayerId ?? string.Empty,
                "Destroyed"
            );
        }

        private GameEventDto ConvertEvolveEvent(IGameEvent gameEvent)
        {
            return new UnitEvolvedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? string.Empty,
                gameEvent.SourceCard?.ToDto() ?? new CardInstanceDto()
            );
        }

        private GameEventDto ConvertDrawEvent(IGameEvent gameEvent)
        {
            return new CardDrawnEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? string.Empty,
                gameEvent.SourceCard?.InstanceId ?? string.Empty,
                gameEvent.SourceCard?.DefinitionId.ToString() ?? "0" // Convert int to string
            );
        }

        private GameEventDto ConvertTurnStartEvent(IGameEvent gameEvent)
        {
            var gameState = gameEvent.Parameters.GetValueOrDefault("game_state") as GameStateDto;
            // Swap argument order: GameStateDto first, then PlayerId string
            return new TurnStartedEventDto(
                gameState ?? new GameStateDto(),
                gameEvent.SourcePlayer?.PlayerId ?? string.Empty
            );
        }

        private GameEventDto ConvertTurnEndEvent(IGameEvent gameEvent)
        {
            var nextPlayerId = (string)(gameEvent.Parameters.GetValueOrDefault("next_player_id") ?? string.Empty);
            return new TurnEndedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? string.Empty,
                nextPlayerId
            );
        }

        private GameEventDto ConvertAbilityTriggeredEvent(IGameEvent gameEvent)
        {
            var abilityTag = (string)(gameEvent.Parameters.GetValueOrDefault("ability_tag") ?? string.Empty);
            var triggerEventType = (string)(gameEvent.Parameters.GetValueOrDefault("trigger_event_type") ?? string.Empty);

            return new AbilityTriggeredEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? string.Empty,
                gameEvent.SourceCard?.InstanceId ?? string.Empty,
                abilityTag,
                triggerEventType,
                gameEvent.Parameters
            );
        }
    }
}
