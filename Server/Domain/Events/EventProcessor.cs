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
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters; // 【修复BUG-017】添加AttackTargetType
using ShadowverseBeyond.Server.Domain.Buffs; // Added for Buff effects
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Dtos.Enums;
using AutoMapper; // Added for IMapper
using ShadowverseBeyond.Server.Services.Abstractions; // 【修复】添加IGameDataProvider
using ShadowverseBeyond.Server.Domain.Events.Services; // Added for IEventHistoryService

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// 统一的Event处理器，负责处理Event链和能力触发
    /// 基于最新Event驱动架构设计
    /// </summary>
    public class EventProcessor : IEventProcessor // Implement the interface
    {
        private readonly Queue<IGameEvent> _eventQueue = new();
        private readonly ILogger<EventProcessor> _logger;
        private readonly IMapper _mapper; // Added IMapper field
        private readonly IEffectExecutor _effectExecutor; // Added IEffectExecutor field
        private readonly IGameDataProvider _gameDataProvider; // 【修复】添加IGameDataProvider依赖
        private readonly IEventHistoryService _eventHistoryService; // Added for event history
        private readonly IServiceProvider _serviceProvider; // Added for service location

        public EventProcessor(ILogger<EventProcessor> logger, IMapper mapper, IEffectExecutor effectExecutor, IGameDataProvider gameDataProvider, IEventHistoryService eventHistoryService, IServiceProvider serviceProvider) // Inject IServiceProvider
        {
            _logger = logger;
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper)); // Store IMapper
            _effectExecutor = effectExecutor ?? throw new ArgumentNullException(nameof(effectExecutor)); // Store IEffectExecutor
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider)); // Store IGameDataProvider
            _eventHistoryService = eventHistoryService ?? throw new ArgumentNullException(nameof(eventHistoryService)); // Store IEventHistoryService
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)); // Store IServiceProvider
        }

        /// <summary>
        /// Processes a single game event, executes it, checks for triggered abilities,
        /// executes those abilities, and collects all resulting events.
        /// </summary>
        /// <param name="initialEvent">The initial game event to process.</param>
        /// <param name="context">The current game context.</param>
        /// <returns>A tuple containing a list of new domain events generated, a list of DTOs for broadcasting, and a list of effect requests to be queued.</returns>
        public async Task<ProcessorResult> ProcessEventAsync(IGameEvent initialEvent, GameContext context)
        {
            var result = new ProcessorResult();
            var abilityManager = context.AbilityManager;
            _logger.LogInformation("[EventProcessor] Identifying abilities for event {EventType}.", initialEvent.EventType);

            try
            {
                var processingContext = context with { TriggeringEvent = initialEvent };

                // 1. Generate DTO for the initial event
                var initialEventDto = ConvertEventToDto(initialEvent, processingContext);
                if (initialEventDto != null)
                {
                    result.ResultingDtos.Add(initialEventDto);
                    _logger.LogTrace("[EventProcessor] Added DTO for initial event {EventType}", initialEvent.EventType);
                }

                // Event history persistence is handled in the transaction phase.
                // Keep only GameEnd cleanup here to avoid stale history across matches.
                if (processingContext.CurrentGameState != null && initialEvent.EventType == EventType.GameEnd)
                {
                    await _eventHistoryService.ClearHistoryForGameAsync(processingContext.CurrentGameState.GameId);
                    _logger.LogInformation("[EventProcessor] Cleared event history for ended game {GameId}.", processingContext.CurrentGameState.GameId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EventProcessor] Error processing event {EventType} (ID: {EventId}).", initialEvent.EventType, initialEvent.EventId);
                result.ResultingDtos.Add(new ErrorOccurredEventDto(context.RequestingPlayerId, $"Error processing event {initialEvent.EventType}: {ex.Message}"));
            }

            _logger.LogDebug("[EventProcessor] Event processing completed. Identified {AbilityCount} activated abilities and generated {DtoCount} DTOs.",
                result.ActivatedAbilities.Count, result.ResultingDtos.Count);

            return result;
        }

        // Removed _activeAbilities field and its management methods (RegisterAbility, UnregisterAbility, ClearAbilities)
        // EventProcessor now relies on IAbilityTriggerService for all ability retrieval.

        // Removed ProcessSystemEventsAsync method.
        // PlayerActionRequestEvents (MulliganSubmitted, EndTurnRequested, PlayCardRequested, AttackRequested, EvolveRequested)
        // will now be handled by corresponding system abilities registered in AbilityManager
        // and retrieved via _abilityTriggerService.GetTriggeredAbilitiesAsync.


        /// <summary>
        /// Processes a single runtime ability, creating its effects.
        /// </summary>
        /// <param name="ability">The runtime ability to process (can be card or system ability).</param>
        /// <param name="triggerEvent">The event that triggered this ability.</param>
        /// <param name="context">The current game context.</param>
        /// <returns>A list of effects generated by the ability.</returns>

        /// <summary>
        /// 将Event转换为DTO
        /// </summary>
        private GameEventDto? ConvertEventToDto(IGameEvent gameEvent, GameContext context)
        {
            try
            {
                return gameEvent.EventType switch
                {
                    EventType.PlayCard => ConvertPlayCardEvent(gameEvent),
                    EventType.Summon => ConvertSummonEvent(gameEvent),
                    EventType.Damage => ConvertDamageEvent(gameEvent),
                    EventType.Heal => ConvertHealEvent(gameEvent),
                    EventType.Destroy => ConvertCardDestroyedEvent(gameEvent),
                    EventType.Evolve => ConvertEvolveEvent(gameEvent),
                    EventType.Draw => ConvertDrawEvent(gameEvent),
                    EventType.TurnStart => ConvertTurnStartEvent(gameEvent, context),
                    EventType.TurnEnd => ConvertTurnEndEvent(gameEvent, context),
                    EventType.AbilityTriggered => ConvertAbilityTriggeredEvent(gameEvent),
                    EventType.BuffApplied => ConvertBuffAppliedEvent(gameEvent, context),
                    EventType.BuffRemoved => ConvertBuffRemovedEvent(gameEvent, context),
                    EventType.SilenceApplied => ConvertSilenceAppliedEvent(gameEvent),
                    EventType.SilenceRemoved => ConvertSilenceRemovedEvent(gameEvent),
                    EventType.MulliganCompleted => ConvertMulliganCompletedEvent(gameEvent),
                    EventType.HandUpdated => ConvertHandUpdatedEvent(gameEvent),
                    EventType.CardInstanceUpdated => ConvertCardInstanceUpdatedEvent(gameEvent),
                    EventType.PpUpdated => ConvertPpUpdateEvent(gameEvent),
                    EventType.PpMaxUpdated => ConvertPpMaxUpdateEvent(gameEvent),
                    EventType.PpBoostStateUpdated => ConvertPpBoostStateUpdatedEvent(gameEvent),
                    EventType.EpUpdated => ConvertEpUpdateEvent(gameEvent),
                    EventType.EpMaxUpdated => ConvertEpMaxUpdateEvent(gameEvent),
                    EventType.SepUpdated => ConvertSepUpdateEvent(gameEvent),
                    EventType.SepMaxUpdated => ConvertSepMaxUpdateEvent(gameEvent),
                    EventType.GraveyardUpdated => ConvertGraveyardUpdatedEvent(gameEvent),
                    EventType.DeckUpdated => ConvertDeckUpdatedEvent(gameEvent),
                    EventType.CounterChanged => ConvertCounterChangedEvent(gameEvent),
                    EventType.CardMoved => ConvertCardMovedEvent(gameEvent),
                    EventType.Vanish => ConvertVanishEvent(gameEvent),
                    EventType.Return => ConvertReturnEvent(gameEvent),
                    EventType.Tuck => ConvertTuckEvent(gameEvent),
                    EventType.Transform => ConvertTransformEvent(gameEvent),
                    EventType.GameEnd => ConvertGameEndEvent(gameEvent),
                    EventType.AddEmblem => ConvertAddEmblemEvent(gameEvent),
                    // 【重复伤害修复】Action Request Events - 不转换为DTO，避免重复广播
                    // 这些事件只是请求事件，不应该直接转换为DTO
                    // 它们会生成真正的领域事件（如HandUpdatedEvent、SummonEvent等），这些领域事件才应该转换为DTO
                    EventType.MulliganSubmitted => null,
                    EventType.AttackRequested => null,
                    EventType.EndTurnRequested => null,
                    EventType.EvolveRequested => null,
                    EventType.SuperEvolveRequested => null,
                    EventType.PlayCardRequested => null,
                    EventType.PpBoostRequested => null,
                    EventType.CombatResolution => null,
                    // Keyword Events - 关键字事件转换
                    EventType.KeywordApplied => ConvertKeywordAddedEvent(gameEvent),
                    EventType.KeywordRemoved => ConvertKeywordRemovedEvent(gameEvent),
                    EventType.HealthChanged => ConvertHealthChangedEvent(gameEvent),
                    EventType.StatChanged => ConvertStatChangedEvent(gameEvent),
                    EventType.AttackStateUpdated => ConvertAttackStateUpdatedEvent(gameEvent),
                    EventType.ActStateUpdated => ConvertActStateUpdatedEvent(gameEvent),
                    EventType.EvolveStateUpdated => ConvertEvolveStateUpdatedEvent(gameEvent),
                    EventType.SuperEvolveStateUpdated => ConvertSuperEvolveStateUpdatedEvent(gameEvent),
                    EventType.CardRevealed => ConvertCardRevealedEvent(gameEvent),
                    EventType.CardFused => ConvertFusionEvent(gameEvent),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EventProcessor] Error converting event {EventType} to DTO", gameEvent.EventType);
                // 【BUG修复】返回准确的错误信息，而不是硬编码的误导性信息。
                // 【修复】使用context中的请求玩家ID作为回退
                return new ErrorOccurredEventDto(context.RequestingPlayerId, $"Cannot convert {gameEvent.EventType} to DTO: {ex.Message}");
            }
        }

        // DTO转换方法（简化实现）
        private GameEventDto? ConvertPlayCardEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not PlayCardEvent e || e.Source?.Player == null || e.Target.FirstOrDefault()?.Card == null) return null;
            var cardDto = _mapper.Map<CardInstanceDto>(e.Target.FirstOrDefault().Card);

            // The CardPlayedEventDto constructor only accepts the player and the card.
            // The target information is not part of this DTO.
            return new CardPlayedEventDto(e.Source.Player.PlayerId, cardDto);
        }

        private GameEventDto? ConvertSummonEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not SummonEvent e || e.Source?.Player == null || e.Target.FirstOrDefault()?.Card == null) return null;
            var cardDto = _mapper.Map<CardInstanceDto>(e.Target.FirstOrDefault()!.Card);

            if (cardDto == null)
            {
                _logger.LogWarning("Cannot convert SummonEvent to DTO: Card could not be mapped.");
                return new ErrorOccurredEventDto(e.Source.Player.PlayerId, "Cannot convert SummonEvent to DTO: Card could not be mapped.");
            }

            return new CardSummonedEventDto(
                e.Source.Player.PlayerId,
                cardDto,
                "Played" // TODO: The source of summon (e.g., "Played", "Effect") should come from the event.
            );
        }

        private GameEventDto? ConvertDamageEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not DamageEvent e || e.Target.FirstOrDefault()?.Card == null) return null;
            var targetCard = e.Target.FirstOrDefault()!.Card;
            var sourcePlayerId = e.Source?.Player?.PlayerId ?? targetCard.OwnerPlayerId;

            return new DamageDealtEventDto(
                sourcePlayerId,
                targetCard.InstanceId,
                e.ActualDamage,
                targetCard.OwnerPlayerId,
                e.Source?.Card?.InstanceId,
                "Effect" // TODO: The source of damage (e.g., "Attack", "Effect") should come from the event.
            );
        }

        private GameEventDto? ConvertHealEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not HealEvent e || e.Target.FirstOrDefault()?.Card == null) return null;
            var targetCard = e.Target.FirstOrDefault()!.Card;
            var sourcePlayerId = e.Source?.Player?.PlayerId ?? targetCard.OwnerPlayerId;

            return new HealAppliedEventDto(
                sourcePlayerId,
                targetCard.InstanceId,
                e.HealAmount,
                targetCard.OwnerPlayerId,
                e.Source?.Card?.InstanceId,
                "Effect"
            );
        }

        private GameEventDto? ConvertCardDestroyedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not DestroyEvent e || e.Target.FirstOrDefault()?.Card == null) return null;
            var targetCard = e.Target.FirstOrDefault()!.Card;
            if (targetCard.CurrentZone != CardZone.Graveyard)
            {
                return null;
            }
            return new UnitDestroyedEventDto(
                targetCard.OwnerPlayerId, // The owner of the card that was destroyed
                targetCard.InstanceId,
                targetCard.OwnerPlayerId,
                e.Reason.ToString()
            );
        }

        private GameEventDto? ConvertVanishEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not CardVanishedEvent e || e.VanishedCard == null) return null;
            var card = e.VanishedCard;
            var sourcePlayerId = card.OwnerPlayerId;

            return new CardVanishedEventDto(
                sourcePlayerId,
                card.InstanceId,
                card.OwnerPlayerId,
                e.VanishReason ?? "Unknown"
            );
        }

        private GameEventDto? ConvertReturnEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not ReturnEvent e || e.ReturnedCard == null) return null;
            var card = e.ReturnedCard;
            var playerId = e.Source?.Player?.PlayerId ?? card.OwnerPlayerId;
            var cardDto = _mapper.Map<CardInstanceDto>(card);

            return new CardReturnedDto(
                sourcePlayerId: playerId,
                playerId: playerId,
                cardUid: card.InstanceId,
                returnedCard: cardDto,
                reason: e.Reason.ToString()
            );
        }

        private GameEventDto? ConvertTuckEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not TuckEvent e || e.TuckedCard == null) return null;
            var card = e.TuckedCard;
            var playerId = card.OwnerPlayerId;
            var cardDto = _mapper.Map<CardInstanceDto>(card);

            return new TuckEventDto(
                sourcePlayerId: playerId,
                playerId: playerId,
                cardUid: card.InstanceId,
                tuckedCard: cardDto,
                reason: e.Reason.ToString()
            );
        }

        private GameEventDto? ConvertCardMovedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not CardMovedEvent e || e.MovedCard == null) return null;
            var card = e.MovedCard;
            var sourcePlayerId = card.OwnerPlayerId;
            var cardDto = _mapper.Map<CardInstanceDto>(card);

            return new CardMovedEventDto(
                sourcePlayerId: sourcePlayerId,
                playerId: card.OwnerPlayerId,
                cardUid: card.InstanceId,
                fromZone: e.FromZone,
                toZone: e.ToZone,
                moveReason: e.MoveReason.ToString(),
                movedCard: cardDto
            );
        }

        private GameEventDto? ConvertEvolveEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not EvolveEvent e) return null;
            var evolvedCard = e.EvolvedCard;
            if (evolvedCard == null) return null;
            var cardDto = _mapper.Map<CardInstanceDto>(evolvedCard);
            if (cardDto == null)
            {
                return new ErrorOccurredEventDto(evolvedCard.OwnerPlayerId, "Cannot convert EvolveEvent to DTO: EvolvedCard could not be mapped.");
            }
            return new UnitEvolvedEventDto(
                evolvedCard.OwnerPlayerId,
                cardDto
            );
        }

        private GameEventDto? ConvertDrawEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not DrawEvent) return null;
            // DrawEvent uses the log announcement for client-facing output; skip per-card DTOs.
            return null;
        }

        private GameEventDto? ConvertTurnStartEvent(IGameEvent gameEvent, GameContext context)
        {
            if (gameEvent is not TurnStartEvent turnStartEvent || turnStartEvent.Source?.Player == null)
            {
                _logger.LogError("ConvertTurnStartEvent called with incorrect event type or null source player: {EventType}", gameEvent.EventType);
                return new ErrorOccurredEventDto(context.RequestingPlayerId, "Internal server error: Invalid event type for TurnStart conversion.");
            }

            // The most reliable source for the game state is the context, which holds the state
            // at the point of event processing.
            var gameStateSnapshot = _mapper.Map<GameStateDto>(context.CurrentGameState);

            return new TurnStartedEventDto(
                gameStateSnapshot,
                turnStartEvent.Source.Player.PlayerId
            );
        }

        private GameEventDto? ConvertTurnEndEvent(IGameEvent gameEvent, GameContext context)
        {
            if (gameEvent is not TurnEndEvent turnEndEvent)
            {
                _logger.LogError("ConvertTurnEndEvent called with incorrect event type: {EventType}", gameEvent.EventType);
                return null; // Or an error DTO
            }

            if (turnEndEvent.Source?.Player == null) return null;
            var nextPlayer = context.CurrentGameState.GetOpponentPlayerState(turnEndEvent.Source.Player.PlayerId);

            return new TurnEndedEventDto(
                turnEndEvent.Source.Player.PlayerId,
                nextPlayer?.PlayerId ?? Guid.Empty
            );
        }

        private GameEventDto? ConvertAbilityTriggeredEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not AbilityTriggeredEvent e) return null;

            var sourceCard = e.TriggeredAbility.SourceCard;
            var sourcePlayerId = sourceCard?.OwnerPlayerId ?? Guid.Empty;
            var concreteAbility = e.TriggeredAbility as Ability;
            var abilityName = concreteAbility?.Name ?? e.TriggeredAbility.Name;
            var abilityDescription = concreteAbility?.Description ?? string.Empty;

            // The DTO requires a parameters dictionary. We reconstruct the essential parts
            // from the strongly-typed event properties for client-side use.
            var parameters = new Dictionary<string, object>
            {
                { "ability_definition_id", e.TriggeredAbility.DefinitionId },
                { "ability_runtime_id", e.TriggeredAbility.RuntimeId },
                { "ability_name", abilityName },
                { "ability_description", abilityDescription },
                { "source_card_name", sourceCard?.Name ?? string.Empty },
                { "source_card_art_preview", sourceCard?.CurrentDefinition?.ArtPreview ?? string.Empty }
            };

            return new AbilityTriggeredEventDto(
                sourcePlayerId,
                sourceCard?.InstanceId ?? Guid.Empty,
                e.TriggerEventType.ToString(),
                parameters,
                e.AbilityKeywords.Select(k => k.ToString()).ToList()
            );
        }

        private GameEventDto? ConvertBuffAppliedEvent(IGameEvent gameEvent, GameContext context)
        {
            if (gameEvent is Events.BuffAppliedEvent buffAppliedEvent)
            {
                var buff = buffAppliedEvent.AppliedBuff;
                var sourceAbility = ResolveSourceAbility(context, buff.SourceAbilityId);
                var sourceCard = sourceAbility?.SourceCard;
                var sourcePlayerId = sourceCard?.OwnerPlayerId ?? Guid.Empty;

                return new BuffAppliedEventDto(
                    sourcePlayerId,
                    buff.Id,
                    buff.TargetCardInstanceId,
                    sourceAbility?.SourceCardId,
                    buff.DefinitionId,
                    ExtractBuffType(buff),
                    buff.DurationType.ToString(),
                    buff.DurationParameter
                );
            }

            // Fallback logic removed as it relied on obsolete base properties.
            // If the event is not a strongly-typed BuffAppliedEvent, we cannot correctly convert it.
            _logger.LogWarning("Attempted to convert a generic event to BuffAppliedEventDto. This is not supported.");
            return null;
        }

        private GameEventDto? ConvertBuffRemovedEvent(IGameEvent gameEvent, GameContext context)
        {
            if (gameEvent is Events.BuffRemovedEvent buffRemovedEvent)
            {
                var buff = buffRemovedEvent.RemovedBuff;
                var sourceAbility = ResolveSourceAbility(context, buff.SourceAbilityId);
                var sourceCard = sourceAbility?.SourceCard;
                var sourcePlayerId = sourceCard?.OwnerPlayerId ?? Guid.Empty;

                return new BuffRemovedEventDto(
                    sourcePlayerId,
                    buff.Id,
                    buff.TargetCardInstanceId,
                    buffRemovedEvent.RemovalReason,
                    sourceAbility?.SourceCardId,
                    buff.DefinitionId,
                    ExtractBuffType(buff)
                );
            }

            // Fallback logic removed as it relied on obsolete base properties.
            _logger.LogWarning("Attempted to convert a generic event to BuffRemovedEventDto. This is not supported.");
            return null;
        }

        private static IAbility? ResolveSourceAbility(GameContext context, Guid sourceAbilityId)
        {
            if (sourceAbilityId == Guid.Empty)
            {
                return null;
            }

            return context.AbilityManager.GetAbilityByRuntimeId(sourceAbilityId);
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
                    case TribeBuffEffect:
                        effectTypes.Add("AddTribe");
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

        private GameEventDto? ConvertSilenceAppliedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is SilenceAppliedEvent silenceAppliedEvent)
            {
                var targetCard = silenceAppliedEvent.Target.FirstOrDefault()?.Card;
                if (targetCard == null) return null;
                var sourceAbility = silenceAppliedEvent.SourceAbility;
                var sourceCard = sourceAbility?.SourceCard;
                var sourcePlayerId = sourceCard?.OwnerPlayerId ?? targetCard.OwnerPlayerId; // Fallback to target's owner if source is unknown

                return new SilenceAppliedEventDto(
                    sourcePlayerId,
                    targetCard.InstanceId,
                    sourceCard?.InstanceId,
                    sourceAbility?.DefinitionId ?? 0,
                    0 // TODO: Extract removed buff count from event parameters
                );
            }
            // Fallback logic removed as it's unreliable and uses obsolete properties.
            _logger.LogWarning("Attempted to convert a generic event to SilenceAppliedEventDto. This is not supported.");
            return null;
        }

        private GameEventDto? ConvertSilenceRemovedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is Events.SilenceRemovedEvent silenceRemovedEvent)
            {
                var targetCard = silenceRemovedEvent.Target.FirstOrDefault()?.Card;
                if (targetCard == null) return null;
                var sourcePlayerId = targetCard.OwnerPlayerId; // Convert Guid to string

                return new SilenceRemovedEventDto(
                    sourcePlayerId, // Now a string
                    targetCard.InstanceId
                );
            }

            // Fallback logic removed.
            _logger.LogWarning("Attempted to convert a generic event to SilenceRemovedEventDto. This is not supported.");
            return null;
        }

        private static GameEventDto? ConvertMulliganCompletedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not MulliganCompletedEvent e) return null;
            if (e.Source?.Player == null) return null;
            return new MulliganCompletedEventDto(e.Source.Player.PlayerId);
        }

        private GameEventDto? ConvertHandUpdatedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not HandUpdatedEvent e) return null;
            if (e.Source?.Player == null) return null;

            var playerId = e.Source.Player.PlayerId;
            var updatedHand = e.UpdatedHand;

            // 【修复】Convert CardInstance list to CardInstanceDto list using complete mapping
            var handDtos = _mapper.Map<List<CardInstanceDto>>(updatedHand);

            return new HandUpdatedEventDto(playerId, handDtos);
        }

        private GameEventDto? ConvertCardInstanceUpdatedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not CardInstanceUpdatedEvent e) return null;

            var updatedCards = _mapper.Map<List<CardInstanceDto>>(e.UpdatedCards);
            return new CardInstanceUpdatedEventDto(Guid.Empty, updatedCards);
        }

        private GameEventDto? ConvertTransformEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not TransformEvent e) return null;
            if (e.TransformingCard == null) return null;

            var transformedCardDto = _mapper.Map<CardInstanceDto>(e.TransformingCard);
            if (transformedCardDto == null) return null;

            return new CardInstanceUpdatedEventDto(
                e.TransformingCard.OwnerPlayerId,
                new List<CardInstanceDto> { transformedCardDto });
        }

        private GameEventDto? ConvertPpUpdateEvent(IGameEvent gameEvent)
        {
            var player = gameEvent.Source?.Player;
            if (player == null) return null;
            return new PpUpdateEventDto(
                player.PlayerId,
                player.CurrentPlayPoints,
                player.MaxPlayPoints
            );
        }

        private GameEventDto? ConvertPpMaxUpdateEvent(IGameEvent gameEvent)
        {
            var player = gameEvent.Source?.Player;
            if (player == null) return null;
            return new PpMaxUpdateEventDto(
                player.PlayerId,
                player.CurrentPlayPoints,
                player.MaxPlayPoints
            );
        }

        private GameEventDto? ConvertPpBoostStateUpdatedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not PpBoostStateUpdatedEvent e) return null;
            var player = e.Source?.Player;
            if (player == null) return null;
            return new PpBoostStateUpdatedEventDto(
                player.PlayerId,
                e.PpBoostUsesAvailable
            );
        }

        private GameEventDto? ConvertEpMaxUpdateEvent(IGameEvent gameEvent)
        {
            var player = gameEvent.Source?.Player;
            if (player == null) return null;
            return new EpMaxUpdateEventDto(
                player.PlayerId,
                player.CurrentEvolutionPoints,
                player.MaxEvolutionPoints
            );
        }



        private GameEventDto? ConvertEpUpdateEvent(IGameEvent gameEvent)
        {
            var player = gameEvent.Source?.Player;
            if (player == null) return null;
            return new EpUpdateEventDto(
                player.PlayerId,
                player.CurrentEvolutionPoints,
                player.MaxEvolutionPoints
            );
        }

        private GameEventDto? ConvertSepUpdateEvent(IGameEvent gameEvent)
        {
            var player = gameEvent.Source?.Player;
            if (player == null) return null;
            return new SepUpdateEventDto(
                player.PlayerId,
                player.CurrentSuperEvolutionPoints,
                player.MaxSuperEvolutionPoints
            );
        }

        private GameEventDto? ConvertSepMaxUpdateEvent(IGameEvent gameEvent)
        {
            var player = gameEvent.Source?.Player;
            if (player == null) return null;
            return new SepMaxUpdateEventDto(
                player.PlayerId,
                player.CurrentSuperEvolutionPoints,
                player.MaxSuperEvolutionPoints
            );
        }

        private GameEventDto? ConvertEvolveStateUpdatedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not EvolveStateUpdatedEvent e) return null;
            var player = e.Source?.Player;
            if (player == null) return null;
            return new EvolveStateUpdatedEventDto(
                player.PlayerId,
                e.CanEvolve
            );
        }

        private GameEventDto? ConvertSuperEvolveStateUpdatedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not SuperEvolveStateUpdatedEvent e) return null;
            var player = e.Source?.Player;
            if (player == null) return null;
            return new SuperEvolveStateUpdatedEventDto(
                player.PlayerId,
                e.CanSuperEvolve
            );
        }

        private GameEventDto? ConvertKeywordAddedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not KeywordAddedEvent e) return null;
            if (e.TargetCard == null) return null;

            return new Dtos.SignalR.Events.KeywordAddedEventDto(
                sourcePlayerId: e.TargetCard.OwnerPlayerId,
                cardInstanceId: e.TargetCard.InstanceId,
                keyword: e.KeywordAdded);
        }

        private GameEventDto? ConvertKeywordRemovedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not KeywordRemovedEvent e) return null;
            if (e.TargetCard == null) return null;

            return new Dtos.SignalR.Events.KeywordRemovedEventDto(
                sourcePlayerId: e.TargetCard.OwnerPlayerId,
                cardInstanceId: e.TargetCard.InstanceId,
                keyword: e.Keyword);
        }

        private GameEventDto? ConvertHealthChangedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not HealthChangedEvent e) return null;

            if (e.Source?.Card == null)
            {
                _logger.LogWarning("[EventProcessor] HealthChangedEvent is missing a source card. Cannot create DTO.");
                return null;
            }

            var targetCard = e.Source.Card;
            var sourcePlayerId = targetCard.OwnerPlayerId;

            // DamageTaken should be a positive value representing damage.
            // ChangeAmount is negative for damage, positive for heal.
            var damageTaken = e.ChangeAmount < 0 ? -e.ChangeAmount : 0;

            return new Dtos.SignalR.Events.HealthChangedEventDto(
                sourcePlayerId,
                targetCard.InstanceId,
                e.NewHealth,
                damageTaken
            );
        }

        private GameEventDto? ConvertStatChangedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not StatChangedEvent e) return null;
            if (e.TargetCard == null) return null;

            var targetCard = e.TargetCard;
            return new StatChangeEventDto(
                sourcePlayerId: targetCard.OwnerPlayerId,
                targetEntityUid: targetCard.InstanceId,
                newAttack: targetCard.CurrentAttack,
                newCurrentHealth: targetCard.CurrentHealth,
                newMaxHealth: targetCard.MaxHealth
            );
        }

        private GameEventDto? ConvertGraveyardUpdatedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not GraveyardUpdatedEvent e) return null;
            if (e.Source?.Player == null) return null;

            // The GraveyardUpdateEventDto does not require the destroyed card info,
            // only the player ID and the new count.
            return new GraveyardUpdateEventDto(
                e.Source.Player.PlayerId, // This is the player whose graveyard was updated
                e.NewGraveyardCount,
                null // destroyedCardDto is optional and can be null
            );
        }

        private GameEventDto? ConvertDeckUpdatedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not DeckCountChangedEvent e) return null;
            if (e.Source?.Player == null) return null;

            return new DeckUpdateEventDto(
                e.Source.Player.PlayerId,
                e.NewDeckCount
            );
        }

        private GameEventDto? ConvertCounterChangedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not CounterChangedEvent e) return null;
            var sourcePlayerId = e.Source?.Player?.PlayerId
                ?? e.Source?.Card?.OwnerPlayerId
                ?? Guid.Empty;
            var targetPlayerId = e.Source?.Player?.PlayerId;
            var targetCardId = e.Source?.Card?.InstanceId;

            return new CounterChangedEventDto(
                sourcePlayerId,
                targetCardId,
                targetPlayerId,
                e.CounterName,
                e.OldValue,
                e.NewValue);
        }

        private GameEventDto? ConvertAddEmblemEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not AddEmblemEvent e) return null;
            var player = e.TargetPlayer;
            if (player == null) return null;

            var addedEmblems = e.CardsToAdd
                .Where(card => card != null)
                .Select(card => _mapper.Map<CardInstanceDto>(card))
                .Where(cardDto => cardDto != null)
                .ToList();

            if (addedEmblems.Count == 0)
            {
                return null;
            }

            return new EmblemUpdatedEventDto(player.PlayerId, addedEmblems);
        }

        private GameEventDto? ConvertAttackStateUpdatedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not AttackStateUpdatedEvent e) return null;
            if (e.Source?.Card == null) return null;

            return new AttackStateUpdatedEventDto(
                e.Source.Card.OwnerPlayerId,
                e.Source.Card.InstanceId,
                e.CanAttack
            );
        }

        private GameEventDto? ConvertActStateUpdatedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not ActStateUpdatedEvent e) return null;
            if (e.Source?.Card == null) return null;

            return new ActStateUpdatedEventDto(
                e.Source.Card.OwnerPlayerId,
                e.Source.Card.InstanceId,
                e.ActLastUsedTurnNumber
            );
        }

        private GameEventDto? ConvertFusionEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not FusionEvent e) return null;
            var sourcePlayerId = e.FusionCard?.OwnerPlayerId ?? Guid.Empty;
            var fusedIds = e.FusedCards.Select(card => card.InstanceId).ToList();
            return new FusionEventDto(sourcePlayerId, fusedIds);
        }

        private GameEventDto? ConvertCardRevealedEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not CardRevealedEvent e) return null;
            var revealingPlayerId = e.Source?.Player?.PlayerId;
            var card = e.RevealedCard;
            if (!revealingPlayerId.HasValue || card == null) return null;

            var cardDto = _mapper.Map<CardInstanceDto>(card);
            return new CardRevealedEventDto(
                sourcePlayerId: revealingPlayerId.Value,
                revealingPlayerId: revealingPlayerId.Value,
                revealedCardInstanceId: card.InstanceId,
                revealedCard: cardDto);
        }

        private static GameEventDto? ConvertGameEndEvent(IGameEvent gameEvent)
        {
            if (gameEvent is not GameEndEvent e) return null;
            return new GameOverEventDto(e.WinnerId, e.WinnerId, e.LoserId, e.Reason);
        }
    }
}
