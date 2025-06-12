//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 错误处理与日志系统: 文档/05-游戏系统/08-错误处理与日志系统.md
//
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Buffs; // Added for Buff effects
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper; // Added for IMapper

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 统一的Event处理器，负责处理Event链和能力触发
    /// 基于最新Event驱动架构设计
    /// </summary>
    public class EventProcessor : IEventProcessor // Implement the interface
    {
        private readonly Queue<IGameEvent> _eventQueue = new();
        private readonly List<Ability> _activeAbilities = new(); // Use runtime Ability objects
        private readonly ILogger<EventProcessor> _logger;
        private readonly IAbilityTriggerService _abilityTriggerService;
        private readonly IMapper _mapper; // Added IMapper field
        private readonly IEffectExecutor _effectExecutor; // Added IEffectExecutor field

        public EventProcessor(ILogger<EventProcessor> logger, IAbilityTriggerService abilityTriggerService, IMapper mapper, IEffectExecutor effectExecutor) // Inject IEffectExecutor
        {
            _logger = logger;
            _abilityTriggerService = abilityTriggerService;
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper)); // Store IMapper
            _effectExecutor = effectExecutor ?? throw new ArgumentNullException(nameof(effectExecutor)); // Store IEffectExecutor
        }

        /// <summary>
        /// Processes a single game event, executes it, checks for triggered abilities,
        /// executes those abilities, and collects all resulting events.
        /// </summary>
        /// <param name="initialEvent">The initial game event to process.</param>
        /// <param name="context">The current game context.</param>
        /// <returns>A tuple containing a list of new domain events generated and a list of DTOs for broadcasting.</returns>
        public async Task<(List<IGameEvent> NewDomainEvents, List<GameEventDto> ResultingDtos)> ProcessEventAsync(IGameEvent initialEvent, GameContext context)
        {
            var resultingDtos = new List<GameEventDto>(); // Renamed from processedEvents
            var allNewDomainEvents = new List<IGameEvent>(); // New list to collect domain events
            var internalProcessingQueue = new Queue<IGameEvent>(); // Use a local queue for this processing chain

            internalProcessingQueue.Enqueue(initialEvent);
            // Do NOT add initialEvent to allNewDomainEvents here, it's the input, not an output of this process.

            _logger.LogDebug("[EventProcessor] Starting event processing chain with initial event: {EventType}", initialEvent.EventType);

            while (internalProcessingQueue.Count > 0)
            {
                var currentEvent = internalProcessingQueue.Dequeue();

                try
                {
                    _logger.LogDebug("[EventProcessor] Processing event: {EventType} (ID: {EventId})", currentEvent.EventType, currentEvent.EventId);

                    // 1. Execute the event itself
                    var eventsFromExecution = await currentEvent.ExecuteAsync(context);
                    if (eventsFromExecution != null && eventsFromExecution.Any())
                    {
                        allNewDomainEvents.AddRange(eventsFromExecution); // Collect generated domain events
                        foreach (var ev in eventsFromExecution) internalProcessingQueue.Enqueue(ev); // Enqueue for further processing in this chain
                        _logger.LogTrace("[EventProcessor] Event {EventType} generated {Count} subsequent events.", currentEvent.EventType, eventsFromExecution.Count);
                    }

                    // 2. Special handling for system events that need to call EffectHandlers
                    var systemEvents = await ProcessSystemEventsAsync(currentEvent, context);
                    if (systemEvents != null && systemEvents.Any())
                    {
                        allNewDomainEvents.AddRange(systemEvents); // Collect generated domain events
                        foreach (var ev in systemEvents) internalProcessingQueue.Enqueue(ev); // Enqueue for further processing
                        _logger.LogTrace("[EventProcessor] System event processing for {EventType} generated {Count} events.", currentEvent.EventType, systemEvents.Count);
                    }

                    // 2. Convert the *current* event to DTO and record it for broadcast
                    var eventDto = ConvertEventToDto(currentEvent);
                    if (eventDto != null)
                    {
                        resultingDtos.Add(eventDto);
                        _logger.LogTrace("[EventProcessor] Event {EventType} converted to DTO: {DtoType}", currentEvent.EventType, eventDto.Type);
                    }

                    // 3. Check for triggered abilities based on the *current* event using IAbilityTriggerService
                    var triggeredAbilities = await _abilityTriggerService.GetTriggeredAbilitiesAsync(currentEvent, context);
                    if (triggeredAbilities.Any())
                    {
                        _logger.LogDebug("[EventProcessor] Found {Count} triggered abilities for event {EventType}", triggeredAbilities.Count, currentEvent.EventType);

                        // 4. Execute triggered abilities and collect generated events
                        foreach (var ability in triggeredAbilities)
                        {
                            try
                            {
                                var eventsFromAbility = await ProcessAbilityAsync(ability, currentEvent, context);
                                if (eventsFromAbility != null && eventsFromAbility.Any())
                                {
                                    allNewDomainEvents.AddRange(eventsFromAbility); // Collect generated domain events
                                    foreach (var ev in eventsFromAbility) internalProcessingQueue.Enqueue(ev); // Enqueue for further processing
                                    _logger.LogTrace("[EventProcessor] Ability {AbilityTag} generated {Count} events.", ability.AbilityTag, eventsFromAbility.Count);
                                }

                                // Generate and enqueue the AbilityTriggeredEvent itself
                                var abilityTriggeredDomainEvent = new AbilityTriggeredEvent(ability, currentEvent);
                                allNewDomainEvents.Add(abilityTriggeredDomainEvent); // Collect this event
                                internalProcessingQueue.Enqueue(abilityTriggeredDomainEvent); // Enqueue for processing (e.g., logging, potential triggers)
                                _logger.LogTrace("[EventProcessor] Enqueued AbilityTriggeredEvent for {AbilityTag}.", ability.AbilityTag);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[EventProcessor] Error processing ability {AbilityTag} triggered by event {EventType}", ability.AbilityTag, currentEvent.EventType);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[EventProcessor] Error processing event {EventType} (ID: {EventId}) in chain.", currentEvent.EventType, currentEvent.EventId);
                    // Decide if the entire chain should fail or just log and continue
                }
            }

            _logger.LogDebug("[EventProcessor] Event processing chain completed. Generated {DomainEventCount} new domain events and {DtoCount} DTOs.",
                allNewDomainEvents.Count, resultingDtos.Count);

            // Return both collected lists
            return (allNewDomainEvents, resultingDtos);
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

        // Removed GetTriggeredAbilitiesAsync method - now delegated to AbilityManager

        // Removed ShouldTriggerAbilityAsync method - now handled by AbilityManager

        /// <summary>
        /// 处理系统事件，调用相应的 EffectHandler
        /// </summary>
        private async Task<List<IGameEvent>> ProcessSystemEventsAsync(IGameEvent gameEvent, GameContext context)
        {
            var events = new List<IGameEvent>();

            try
            {
                // 处理 MulliganSubmittedEvent
                if (gameEvent is Events.ActionRequests.MulliganSubmittedEvent mulliganEvent)
                {
                    _logger.LogDebug("[EventProcessor] Processing MulliganSubmittedEvent for Player {PlayerId}", mulliganEvent.PlayerId);

                    // 创建 Mulligan 效果配置
                    var effectConfig = new EffectConfig
                    {
                        Type = "mulligan",
                        Parameters = new Dictionary<string, ValueConfig>
                        {
                            { "playerId", ValueConfig.Static(mulliganEvent.PlayerId) },
                            { "cardUidsToReplace", ValueConfig.Static(mulliganEvent.CardUidsToReplace) },
                            { "forceMulligan", ValueConfig.Static(false) },
                            { "mulliganReason", ValueConfig.Static("PlayerChoice") }
                        }
                    };

                    // 执行换牌效果
                    var resultEvents = await _effectExecutor.ExecuteEffectAsync(effectConfig, context);
                    events.AddRange(resultEvents);

                    _logger.LogInformation("[EventProcessor] MulliganSubmittedEvent processed successfully for Player {PlayerId}. Generated {EventCount} events.",
                        mulliganEvent.PlayerId, resultEvents.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EventProcessor] Error processing system event {EventType}", gameEvent.EventType);
            }

            return events;
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

                // 解析目标 - 直接使用GameContext
                var targets = await ability.ResolveTargetsAsync(context);

                // 设置选定的目标到GameContext
                foreach (var target in targets)
                {
                    // Use the factory method for CardInstance targets
                    context.AddSelectedTarget(TargetId.Card(target.InstanceId));
                }

                // 执行效果 - 直接使用GameContext
                var generatedEvents = await ability.ExecuteEffectsAsync(context);

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
                    "buff_applied" => ConvertBuffAppliedEvent(gameEvent),
                    "buff_removed" => ConvertBuffRemovedEvent(gameEvent),
                    "silence_applied" => ConvertSilenceAppliedEvent(gameEvent),
                    "silence_removed" => ConvertSilenceRemovedEvent(gameEvent),
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
            // Use AutoMapper, handle null SourceCard
            var sourceCardDto = gameEvent.SourceCard == null ? null : _mapper.Map<CardInstanceDto>(gameEvent.SourceCard);
            return new CardPlayedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                sourceCardDto // Pass mapped DTO or null
            );
        }

        private GameEventDto ConvertSummonEvent(IGameEvent gameEvent)
        {
            // Use AutoMapper, handle null SourceCard
            var sourceCardDto = gameEvent.SourceCard == null ? null : _mapper.Map<CardInstanceDto>(gameEvent.SourceCard);
            return new CardSummonedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                sourceCardDto, // Pass mapped DTO or null
                "Played"
            );
        }

        private GameEventDto ConvertDamageEvent(IGameEvent gameEvent)
        {
            var target = gameEvent.Parameters.GetValueOrDefault("target") as CardInstance;
            var damage = (int)(gameEvent.Parameters.GetValueOrDefault("damage") ?? 0);

            return new DamageDealtEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                target?.InstanceId ?? Guid.Empty,
                damage,
                target?.OwnerPlayerId ?? Guid.Empty, // Convert Guid to string
                gameEvent.SourceCard?.InstanceId,
                "Effect"
            );
        }

        private GameEventDto ConvertCardDestroyedEvent(IGameEvent gameEvent)
        {
            return new UnitDestroyedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                gameEvent.SourceCard?.InstanceId ?? Guid.Empty,
                gameEvent.SourceCard?.OwnerPlayerId ?? Guid.Empty, // Convert Guid to string
                "Destroyed"
            );
        }

        private GameEventDto ConvertEvolveEvent(IGameEvent gameEvent)
        {
            // Use AutoMapper, handle null SourceCard
            var sourceCardDto = gameEvent.SourceCard == null ? null : _mapper.Map<CardInstanceDto>(gameEvent.SourceCard);
            return new UnitEvolvedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                sourceCardDto // Pass mapped DTO or null
            );
        }

        private GameEventDto ConvertDrawEvent(IGameEvent gameEvent)
        {
            return new CardDrawnEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                gameEvent.SourceCard?.InstanceId.ToString() ?? string.Empty,
                gameEvent.SourceCard?.DefinitionId.ToString() ?? "0" // Convert int to string
            );
        }

        private GameEventDto ConvertTurnStartEvent(IGameEvent gameEvent)
        {
            var gameState = gameEvent.Parameters.GetValueOrDefault("game_state") as GameStateDto;
            // Swap argument order: GameStateDto first, then PlayerId string
            return new TurnStartedEventDto(
                gameState ?? new GameStateDto(),
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty
            );
        }

        private GameEventDto ConvertTurnEndEvent(IGameEvent gameEvent)
        {
            var nextPlayerId = (Guid)(gameEvent.Parameters.GetValueOrDefault("next_player_id") ?? Guid.Empty);
            return new TurnEndedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                nextPlayerId
            );
        }

        private GameEventDto ConvertAbilityTriggeredEvent(IGameEvent gameEvent)
        {
            var abilityTag = (string)(gameEvent.Parameters.GetValueOrDefault("ability_tag") ?? string.Empty);
            var triggerEventType = (string)(gameEvent.Parameters.GetValueOrDefault("trigger_event_type") ?? string.Empty);

            return new AbilityTriggeredEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                gameEvent.SourceCard?.InstanceId ?? Guid.Empty,
                abilityTag,
                triggerEventType,
                gameEvent.Parameters
            );
        }

        private GameEventDto ConvertBuffAppliedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is Events.BuffAppliedEvent buffAppliedEvent)
            {
                var buff = buffAppliedEvent.AppliedBuff;
                // Convert Guid? to string? then use ??
                var sourcePlayerId = buff.SourceAbility?.SourceCard?.OwnerPlayerId ?? Guid.Empty;

                return new BuffAppliedEventDto(
                    sourcePlayerId, // Now a string
                    buff.Id,
                    buff.TargetCardInstanceId,
                    buff.SourceAbility?.SourceCard?.InstanceId,
                    buff.DefinitionId,
                    ExtractBuffType(buff),
                    buff.DurationType.ToString(),
                    buff.DurationParameter,
                    buff.IsNonStackable
                );
            }

            // Fallback for generic event
            var buffId = (Guid)(gameEvent.Parameters.GetValueOrDefault("buffId") ?? Guid.Empty);
            var targetCardInstanceId = (Guid)(gameEvent.Parameters.GetValueOrDefault("targetCardInstanceId") ?? Guid.Empty);

            return new BuffAppliedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                buffId,
                targetCardInstanceId,
                gameEvent.SourceCard?.InstanceId
            );
        }

        private GameEventDto ConvertBuffRemovedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is Events.BuffRemovedEvent buffRemovedEvent)
            {
                var buff = buffRemovedEvent.RemovedBuff;
                // Convert Guid? to string? then use ??
                var sourcePlayerId = buff.SourceAbility?.SourceCard?.OwnerPlayerId ?? Guid.Empty;

                return new BuffRemovedEventDto(
                    sourcePlayerId, // Now a string
                    buff.Id,
                    buff.TargetCardInstanceId,
                    buffRemovedEvent.RemovalReason,
                    buff.SourceAbility?.SourceCard?.InstanceId,
                    buff.DefinitionId,
                    ExtractBuffType(buff),
                    buff.IsNonStackable
                );
            }

            // Fallback for generic event
            var buffId = (Guid)(gameEvent.Parameters.GetValueOrDefault("buffId") ?? Guid.Empty);
            var targetCardInstanceId = (Guid)(gameEvent.Parameters.GetValueOrDefault("targetCardInstanceId") ?? Guid.Empty);
            var removalReason = (string)(gameEvent.Parameters.GetValueOrDefault("removalReason") ?? "unknown");

            return new BuffRemovedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                buffId,
                targetCardInstanceId,
                removalReason,
                gameEvent.SourceCard?.InstanceId
            );
        }

        /// <summary>
        /// 从Buff的Effects中提取Buff类型
        /// </summary>
        /// <param name="buff">要分析的Buff实例</param>
        /// <returns>Buff类型字符串</returns>
        private static string ExtractBuffType(Buff buff)
        {
            if (buff.Effects == null || buff.Effects.Count == 0)
                return "Unknown";

            // 检查不同类型的BuffEffect并返回相应的类型字符串
            var effectTypes = new List<string>();

            foreach (var effect in buff.Effects)
            {
                switch (effect)
                {
                    case StatBuffEffect:
                        effectTypes.Add("ModifyStats");
                        break;
                    case KeywordBuffEffect:
                        effectTypes.Add("AddKeyword");
                        break;
                    case AbilityBuffEffect:
                        effectTypes.Add("AddAbility");
                        break;
                    default:
                        effectTypes.Add("Unknown");
                        break;
                }
            }

            // 如果有多种效果类型，返回组合类型；否则返回单一类型
            if (effectTypes.Count == 1)
                return effectTypes[0];
            else if (effectTypes.Count > 1)
                return "Mixed"; // 混合类型Buff
            else
                return "Unknown";
        }

        private static GameEventDto ConvertSilenceAppliedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is Events.SilenceAppliedEvent silenceAppliedEvent)
            {
                var target = silenceAppliedEvent.Target;
                // Convert Guid? to string? then use ??
                var sourcePlayerId = silenceAppliedEvent.SourceCard?.OwnerPlayerId ?? Guid.Empty;

                return new SilenceAppliedEventDto(
                    sourcePlayerId,
                    target.InstanceId,
                    silenceAppliedEvent.SourceCard?.InstanceId,
                    silenceAppliedEvent.SourceAbility?.AbilityId,
                    0 // TODO: Extract removed buff count from event parameters
                );
            }

            // Fallback for generic event
            var targetCardInstanceId = (Guid)(gameEvent.Parameters.GetValueOrDefault("targetCardInstanceId") ?? Guid.Empty);
            var sourceCardInstanceId = (Guid?)gameEvent.Parameters.GetValueOrDefault("sourceCardInstanceId");
            var sourceAbilityId = gameEvent.Parameters.GetValueOrDefault("sourceAbilityId") as long?;

            return new SilenceAppliedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                targetCardInstanceId,
                sourceCardInstanceId,
                sourceAbilityId
            );
        }

        private static GameEventDto ConvertSilenceRemovedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is Events.SilenceRemovedEvent silenceRemovedEvent)
            {
                var target = silenceRemovedEvent.Target;
                var sourcePlayerId = target.OwnerPlayerId; // Convert Guid to string

                return new SilenceRemovedEventDto(
                    sourcePlayerId, // Now a string
                    target.InstanceId
                );
            }

            // Fallback for generic event
            var targetCardInstanceId = (Guid)(gameEvent.Parameters.GetValueOrDefault("targetCardInstanceId") ?? Guid.Empty);

            return new SilenceRemovedEventDto(
                gameEvent.SourcePlayer?.PlayerId ?? Guid.Empty,
                targetCardInstanceId
            );
        }
    }
}
