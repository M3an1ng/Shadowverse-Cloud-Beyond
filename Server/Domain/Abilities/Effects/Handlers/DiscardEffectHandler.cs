using Microsoft.Extensions.Logging;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;

using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// 弃牌效果处理器
    /// 强制玩家弃置手牌
    /// </summary>
    public class DiscardEffectHandler : EffectHandlerBase<DiscardEffectParams>
    {
        private readonly IMapper _mapper;
        private readonly ITargetProcessingService _targetProcessingService;

        public DiscardEffectHandler(ILogger logger, IMapper mapper, ITargetProcessingService targetProcessingService)
            : base(logger)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
        }

        public override async Task<GameStateActionResult> ApplyAsync(GameContext context, DiscardEffectParams parameters)
        {
            const string effectName = "DiscardEffect";
            Logger.LogDebug("{EffectName}: Starting with DiscardCount={Count}, IsRandom={IsRandom}.",
                           effectName, parameters.DiscardCount, parameters.IsRandom);

            // Validate discard count
            if (parameters.DiscardCount <= 0)
            {
                Logger.LogWarning("{EffectName}: DiscardCount is {Count}, no cards will be discarded.", effectName, parameters.DiscardCount);
                return new GameStateActionResult { Success = false, ErrorMessage = "DiscardCount must be positive." };
            }

            // Define the action for discarding cards from a PlayerState
            Func<PlayerState, GameContext, Task<IndividualActionResult>> playerDiscardAction =
                async (player, ctx) =>
                {
                    Logger.LogDebug("{EffectName}: Processing Player {PlayerId} to discard {Count} card(s).",
                                   effectName, player.PlayerId, parameters.DiscardCount);

                    // Check if player has cards to discard
                    if (player.Hand.Count == 0)
                    {
                        Logger.LogInformation("{EffectName}: Player {PlayerId} has no cards in hand to discard.",
                                             effectName, player.PlayerId);

                        await Task.CompletedTask;
                        return new IndividualActionResult
                        {
                            TargetId = TargetId.Player(player.PlayerId),
                            Success = true,
                            Message = "No cards in hand to discard.",
                            GeneratedEvents = new List<IGameEvent>()
                        };
                    }

                    // Determine actual discard count
                    int actualDiscardCount = Math.Min(parameters.DiscardCount, player.Hand.Count);
                    var cardsToDiscard = new List<CardInstance>();

                    if (parameters.IsRandom)
                    {
                        // Random discard
                        var random = new Random();
                        var availableCards = player.Hand.ToList();

                        for (int i = 0; i < actualDiscardCount; i++)
                        {
                            var randomIndex = random.Next(availableCards.Count);
                            cardsToDiscard.Add(availableCards[randomIndex]);
                            availableCards.RemoveAt(randomIndex);
                        }

                        Logger.LogDebug("{EffectName}: Randomly selected {Count} card(s) for discard from Player {PlayerId}.",
                                       effectName, cardsToDiscard.Count, player.PlayerId);
                    }
                    else
                    {
                        // Player choice discard - for now, just take the first cards as placeholder
                        // TODO: Implement player choice mechanism when UI interaction system is available
                        cardsToDiscard = player.Hand.Take(actualDiscardCount).ToList();

                        Logger.LogDebug("{EffectName}: Selected first {Count} card(s) for discard from Player {PlayerId} (player choice not yet implemented).",
                                       effectName, cardsToDiscard.Count, player.PlayerId);
                    }

                    // Generate discard events
                    var individualEvents = new List<IGameEvent>();
                    var playerDto = _mapper.Map<PlayerStateDto>(player);

                    foreach (var card in cardsToDiscard)
                    {
                        // TODO: Create CardDiscardedEvent when event is defined
                        // var discardEvent = new CardDiscardedEvent(playerDto, card, parameters.DiscardReason);
                        // individualEvents.Add(discardEvent);

                        Logger.LogTrace("{EffectName}: Generated discard event for card {InstanceId} (DefId: {DefId}) from Player {PlayerId}.",
                                       effectName, card.InstanceId, card.DefinitionId, player.PlayerId);
                    }

                    Logger.LogInformation("{EffectName}: Generated {EventCount} discard event(s) for Player {PlayerId}.",
                                         effectName, cardsToDiscard.Count, player.PlayerId);

                    await Task.CompletedTask; // Discard event generation is synchronous

                    return new IndividualActionResult
                    {
                        TargetId = TargetId.Player(player.PlayerId),
                        Success = true,
                        Message = $"Generated discard events for {cardsToDiscard.Count} card(s).",
                        GeneratedEvents = individualEvents
                    };
                };

            // Discard effect targets Players
            Logger.LogDebug("{EffectName}: Processing targets assuming Player type.", effectName);

            return await _targetProcessingService.ProcessTargetsAsync(
                context,
                TargetIdType.Player, // Expect Player targets
                playerDiscardAction,
                null, // No CardInstance action needed
                effectName);
        }
    }
}
