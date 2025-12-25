using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
using ShadowverseBeyond.Server.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Dtos;
using AutoMapper;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Exceptions;

namespace ShadowverseBeyond.Server.Services
{
    public class PendingActionService : IPendingActionService
    {
        private readonly ConcurrentDictionary<Guid, PendingChoiceContext> _pendingChoices = new();
        private readonly IHubContext<GameHub, IGameClient> _hubContext;
        private readonly ILogger<PendingActionService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;

        public PendingActionService(IHubContext<GameHub, IGameClient> hubContext, ILogger<PendingActionService> logger, IServiceProvider serviceProvider, IMapper mapper)
        {
            _hubContext = hubContext;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _mapper = mapper;
        }

        public Task<PlayerChoiceResult> WaitForPlayerChoiceAsync(string gameId, PlayerChoiceRequest request, CancellationToken cancellationToken)
        {
            request.GameId = gameId;
            var tcs = new TaskCompletionSource<PlayerChoiceResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var context = new PendingChoiceContext(request, tcs, cancellationToken);

            if (!_pendingChoices.TryAdd(request.PendingChoiceId, context))
            {
                _logger.LogError("Failed to add pending choice with ID {ChoiceId}. It may already exist.", request.PendingChoiceId);
                throw new InvalidOperationException("Failed to store pending choice context.");
            }

            cancellationToken.Register(() =>
            {
                if (_pendingChoices.TryRemove(request.PendingChoiceId, out var removedContext))
                {
                    removedContext.Tcs.TrySetCanceled();
                    _logger.LogInformation("Pending choice {ChoiceId} was cancelled due to CancellationToken.", request.PendingChoiceId);
                }
            });

            // Resolve GameService here to get the game state, avoiding circular dependency in constructor
            using var initialScope = _serviceProvider.CreateScope();
            var gameServiceForState = initialScope.ServiceProvider.GetRequiredService<IGameService>();
            var game = gameServiceForState.GetGameState(gameId);
            var player = game?.GetPlayerState(request.RequestingPlayerId);

            if (player != null && player.IsAI)
            {
                _logger.LogInformation("Pending choice {ChoiceId} is for an AI player {PlayerId}. Triggering AI decision task.", request.PendingChoiceId, request.RequestingPlayerId);
                // Fire-and-forget the AI decision task to avoid deadlocking the game loop.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // We need a new scope to resolve scoped services like AiService.
                        using var scope = _serviceProvider.CreateScope();
                        var aiService = scope.ServiceProvider.GetRequiredService<IAiService>();
                        var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();

                        // Ensure game state is not null before proceeding
                        if (game == null)
                        {
                            _logger.LogError("Cannot trigger AI choice because game state is null for GameId {GameId}", gameId);
                            return;
                        }
                        var gameStateDto = _mapper.Map<GameStateDto>(game); // Create a snapshot for the AI to use.

                        var aiResult = await aiService.DecideTargetChoiceAsync(request, gameStateDto);

                        var dto = new SubmitTargetChoiceActionDto
                        {
                            PendingChoiceId = request.PendingChoiceId,
                            ChosenTargetIds = aiResult.ChosenTargetIds,
                            WasCancelled = aiResult.WasCancelled
                        };

                        // 【DEADLOCK_FIX】直接调用GameService的方法，而不是入队新动作
                        // 因为游戏循环正在等待这个Task返回，入队新动作会导致死锁
                        // AI的决定需要立刻、同步地恢复游戏循环
                        _logger.LogInformation("[AI_CHOICE_SUBMIT] AI {PlayerId} submitting choice for {ChoiceId}", player.PlayerId, dto.PendingChoiceId);
                        gameService.SubmitTargetChoiceAsync(game.GameId, player.PlayerId, dto);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in background AI choice decision task for choice {ChoiceId}.", request.PendingChoiceId);
                        // If the AI fails, we should probably cancel the choice to prevent the game from stalling.
                        var cancellationDto = new SubmitTargetChoiceActionDto { WasCancelled = true, PendingChoiceId = request.PendingChoiceId };
                        CompletePlayerChoice(request.PendingChoiceId, cancellationDto);
                    }
                }, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Requesting player choice {ChoiceId} from player {PlayerId}. Sending 'PlayerChoiceRequested' event to client.", request.PendingChoiceId, request.RequestingPlayerId);

                // Create the DTO to send to the client, matching the frontend handler's expectation.
                var choiceEventDto = new PlayerChoiceRequestedEventDto(request.RequestingPlayerId, request);
                string groupName = $"Player_{request.RequestingPlayerId}";

                // Asynchronously send the request to all clients in the game group.
                // The client-side will be responsible for checking if the request is for them.
                _ = _hubContext.Clients.Group(groupName).PlayerChoiceRequested(choiceEventDto);
            }

            return tcs.Task;
        }

        public void CompletePlayerChoice(Guid choiceId, SubmitTargetChoiceActionDto dto)
        {
            if (_pendingChoices.TryGetValue(choiceId, out var context))
            {
                if (dto.SourceAbilityRuntimeId != context.Request.SourceAbilityRuntimeId)
                {
                    throw new GameLogicException($"Invalid choice submission: SourceAbilityRuntimeId mismatch (expected {context.Request.SourceAbilityRuntimeId}, got {dto.SourceAbilityRuntimeId}).");
                }

                if (dto.WasCancelled)
                {
                    if (!context.Request.IsCancellable)
                    {
                        throw new GameLogicException("This choice cannot be cancelled.");
                    }

                    if (_pendingChoices.TryRemove(choiceId, out var removedContext))
                    {
                        removedContext.Tcs.TrySetResult(PlayerChoiceResult.Cancelled);
                        _logger.LogInformation("Completed pending choice {ChoiceId} as cancelled.", choiceId);
                    }
                    return;
                }

                var chosen = dto.ChosenTargetIds ?? new List<Guid>();
                var distinctChosen = chosen.Distinct().ToList();

                if (distinctChosen.Count < context.Request.MinSelectionCount)
                {
                    throw new GameLogicException($"Not enough targets selected. Minimum is {context.Request.MinSelectionCount}.");
                }

                if (distinctChosen.Count > context.Request.MaxSelectionCount)
                {
                    throw new GameLogicException($"Too many targets selected. Maximum is {context.Request.MaxSelectionCount}.");
                }

                var selectable = context.Request.SelectableTargetIds ?? new List<Guid>();
                var selectableSet = new HashSet<Guid>(selectable);
                if (distinctChosen.Any(id => !selectableSet.Contains(id)))
                {
                    throw new GameLogicException("Invalid target selected (not in selectable target set).");
                }

                if (_pendingChoices.TryRemove(choiceId, out var finalContext))
                {
                    var result = new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = distinctChosen
                    };
                    finalContext.Tcs.TrySetResult(result);
                    _logger.LogInformation("Completed pending choice {ChoiceId}.", choiceId);
                }
                return;
            }
            else
            {
                _logger.LogWarning("Could not find pending choice with ID {ChoiceId} to complete.", choiceId);
            }
        }

        public PendingChoiceContext? GetPendingChoiceForPlayer(string gameId, Guid playerId)
        {
            _logger.LogWarning("[STATE_POLLUTION_TRACE] GetPendingChoiceForPlayer called for GameId: {GameId}, PlayerId: {PlayerId}. Current pending choices count: {Count}",
                gameId, playerId, _pendingChoices.Count);

            // This implementation is more complex as it requires iterating.
            // It's better if we can look up by player ID directly.
            // For now, let's find the first match.
            var result = _pendingChoices.Values.FirstOrDefault(c => c.Request.RequestingPlayerId == playerId);

            if (result != null)
            {
                _logger.LogWarning("[STATE_POLLUTION_TRACE] Found a pending choice for PlayerId {PlayerId}. ChoiceId: {ChoiceId}, SourceAbilityRuntimeId: {AbilityId}, Original GameId in Request: {OriginalGameId}",
                    playerId, result.Request.PendingChoiceId, result.Request.SourceAbilityRuntimeId, result.Request.GameId);
            }
            else
            {
                _logger.LogInformation("[STATE_POLLUTION_TRACE] No pending choice found for PlayerId {PlayerId}.", playerId);
            }

            return result;
        }

        public void ForceResolveChoice(Guid choiceId, PlayerChoiceResult forcedResult)
        {
            if (_pendingChoices.TryRemove(choiceId, out var context))
            {
                context.Tcs.TrySetResult(forcedResult);
                _logger.LogInformation("Force-resolved pending choice {ChoiceId}.", choiceId);
            }
        }
    }
}
