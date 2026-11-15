using ShadowverseBeyond.Server.Mappings;
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
        private readonly ConcurrentDictionary<string, byte> _endingGames = new(StringComparer.OrdinalIgnoreCase);
        private readonly IHubContext<GameHub, IGameClient> _hubContext;
        private readonly ILogger<PendingActionService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly IMatchService? _matchService;
        private readonly ReplayRecorder? _replayRecorder;

        public event Action<string, GameStateDto>? WaitingSnapshotReady;

        public PendingActionService(IHubContext<GameHub, IGameClient> hubContext, ILogger<PendingActionService> logger, IServiceProvider serviceProvider, IMapper mapper, IMatchService? matchService = null, ReplayRecorder? replayRecorder = null)
        {
            _hubContext = hubContext;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _mapper = mapper;
            _matchService = matchService;
            _replayRecorder = replayRecorder;
        }

        public async Task<PlayerChoiceResult> WaitForPlayerChoiceAsync(string gameId, PlayerChoiceRequest request, CancellationToken cancellationToken)
        {
            request.GameId = gameId;
            var tcs = new TaskCompletionSource<PlayerChoiceResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var context = new PendingChoiceContext(request, tcs, cancellationToken);

            if (!_pendingChoices.TryAdd(request.PendingChoiceId, context))
            {
                _logger.LogError("Failed to add pending choice with ID {ChoiceId}. It may already exist.", request.PendingChoiceId);
                throw new InvalidOperationException("Failed to store pending choice context.");
            }

            // Register first, then check the exit marker so concurrent room exit cannot miss this choice.
            if (_endingGames.ContainsKey(gameId))
            {
                ForceResolveChoice(request.PendingChoiceId, PlayerChoiceResult.Cancelled);
                return await tcs.Task;
            }

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                if (_pendingChoices.TryRemove(request.PendingChoiceId, out var removedContext))
                {
                    _replayRecorder?.ChoiceResolved(request.PendingChoiceId, "action_cancelled", []);
                    removedContext.Tcs.TrySetCanceled();
                    _logger.LogInformation("Pending choice {ChoiceId} was cancelled due to CancellationToken.", request.PendingChoiceId);
                }
            });

            // Resolve GameService here to get the game state, avoiding circular dependency in constructor
            using var initialScope = _serviceProvider.CreateScope();
            var gameServiceForState = initialScope.ServiceProvider.GetRequiredService<IGameService>();
            var game = gameServiceForState.GetGameState(gameId);
            var player = game?.GetPlayerState(request.RequestingPlayerId);
            if (game != null && _replayRecorder != null)
                await _replayRecorder.WaitingAsync(game, System.Text.Json.JsonSerializer.SerializeToElement(request, ShadowverseBeyond.Server.Infrastructure.Services.SqliteReplayFrames.Json));

            if (game != null && game.Mode == Dtos.Enums.GameMode.Mirage && game.Mirage != null)
            {
                bool isClosingRound;
                lock (game.Mirage.RoundSyncLock)
                {
                    isClosingRound = game.Phase == Dtos.Enums.GamePhase.Recruit
                        && game.Mirage.ClosingRecruitRoundNumber == game.Mirage.RoundNumber;
                }

                if (isClosingRound)
                {
                    _logger.LogInformation("Pending choice {ChoiceId} created while Mirage round {Round} is closing due to timeout; auto-resolving immediately.",
                        request.PendingChoiceId, game.Mirage.RoundNumber);
                    ForceResolveChoice(request.PendingChoiceId, CreateTimeoutChoiceResult(request));
                    return await tcs.Task;
                }
            }

            var choiceEvent = player?.IsAI == true ? null : new PlayerChoiceRequestedEventDto(
                request.RequestingPlayerId, request, MatchCommandObservation.CurrentActionId, MatchCommandObservation.CurrentCommandId);

            if (game != null && !tcs.Task.IsCompleted)
            {
                try
                {
                    // Capture the board and its choice together on the game loop before it pauses.
                    var services = initialScope.ServiceProvider;
                    using var viewContext = new Domain.Events.GameContext(services, _mapper,
                        services.GetRequiredService<IGameContextAccessor>(), game, request.RequestingPlayerId, cancellationToken);
                    var snapshot = GameStateProjection.Create(game, viewContext) with { PendingChoice = choiceEvent };
                    context.WaitingSnapshot = snapshot;
                    WaitingSnapshotReady?.Invoke(gameId, snapshot);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not capture the board for choice {ChoiceId}.", request.PendingChoiceId);
                }
            }

            if (player?.IsAI == true)
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
                        var snapshot = context.WaitingSnapshot;
                        if (snapshot == null)
                        {
                            var accessor = scope.ServiceProvider.GetRequiredService<IGameContextAccessor>();
                            using var viewContext = new Domain.Events.GameContext(
                                scope.ServiceProvider, _mapper, accessor, game, request.RequestingPlayerId, cancellationToken);
                            snapshot = GameStateProjection.Create(game, viewContext);
                        }

                        var aiResult = await aiService.DecideTargetChoiceAsync(request, snapshot, cancellationToken);

                        var dto = new SubmitTargetChoiceActionDto
                        {
                            PendingChoiceId = request.PendingChoiceId,
                            SourceAbilityRuntimeId = request.SourceAbilityRuntimeId,
                            TargetKey = request.TargetKey,
                            ChosenTargetIds = aiResult.ChosenTargetIds,
                            WasCancelled = aiResult.WasCancelled
                        };

                        // 【DEADLOCK_FIX】直接调用GameService的方法，而不是入队新动作
                        // 因为游戏循环正在等待这个Task返回，入队新动作会导致死锁
                        // AI的决定需要立刻、同步地恢复游戏循环
                        _logger.LogInformation("[AI_CHOICE_SUBMIT] AI {PlayerId} submitting choice for {ChoiceId}", player.PlayerId, dto.PendingChoiceId);
                        await gameService.SubmitTargetChoiceAsync(game.GameId, player.PlayerId, dto);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "AI choice decision failed for pending choice {ChoiceId}.", request.PendingChoiceId);
                        if (_pendingChoices.TryRemove(request.PendingChoiceId, out var failedContext))
                        {
                            failedContext.Tcs.TrySetException(ex);
                        }
                    }
                }, cancellationToken);
            }
            else if (choiceEvent != null)
            {
                _logger.LogInformation("Requesting player choice {ChoiceId} from player {PlayerId}. Sending 'PlayerChoiceRequested' event to client.", request.PendingChoiceId, request.RequestingPlayerId);

                var playerGroup = SignalRGroupNames.Player(ResolveRoomScopedId(gameId), request.RequestingPlayerId);
                var client = _hubContext.Clients.Group(playerGroup);

                try
                {
                    if (tcs.Task.IsCompleted) return await tcs.Task;
                    _ = client.PlayerChoiceRequested(choiceEvent);
                    return await tcs.Task;
                }
                finally
                {
                    try
                    {
                        await client.PlayerChoiceResolved(request.PendingChoiceId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not deliver resolution of choice {ChoiceId}.", request.PendingChoiceId);
                    }
                }
            }

            return await tcs.Task;
        }

        public bool CompletePlayerChoice(string gameId, Guid playerId, SubmitTargetChoiceActionDto dto)
        {
            var choiceId = dto.PendingChoiceId;
            if (_pendingChoices.TryGetValue(choiceId, out var context)
                && context.Request.RequestingPlayerId == playerId
                && string.Equals(context.Request.GameId, gameId, StringComparison.OrdinalIgnoreCase))
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
                        _replayRecorder?.ChoiceResolved(choiceId, "cancelled", []);
                        removedContext.Tcs.TrySetResult(PlayerChoiceResult.Cancelled);
                        _logger.LogInformation("Completed pending choice {ChoiceId} as cancelled.", choiceId);
                        return true;
                    }
                    return false;
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
                    _replayRecorder?.ChoiceResolved(choiceId, "selected", distinctChosen);
                    var result = new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = distinctChosen
                    };
                    finalContext.Tcs.TrySetResult(result);
                    _logger.LogInformation("Completed pending choice {ChoiceId}.", choiceId);
                    return true;
                }
            }

            return false;
        }

        private string ResolveRoomScopedId(string gameOrRoomId)
        {
            if (!string.IsNullOrWhiteSpace(gameOrRoomId)
                && _matchService != null
                && _matchService.TryGetRoomIdByGameId(gameOrRoomId, out var roomId)
                && !string.IsNullOrWhiteSpace(roomId))
            {
                return roomId;
            }

            return gameOrRoomId;
        }

        public PendingChoiceContext? GetPendingChoiceForPlayer(string gameId, Guid playerId)
        {
            return _pendingChoices.Values.FirstOrDefault(c =>
                c.Request.RequestingPlayerId == playerId &&
                string.Equals(c.Request.GameId, gameId, StringComparison.OrdinalIgnoreCase));
        }

        public GameStateDto? GetWaitingSnapshot(string gameId)
        {
            foreach (var pending in _pendingChoices.Values)
            {
                if (string.Equals(pending.Request.GameId, gameId, StringComparison.OrdinalIgnoreCase)
                    && pending.WaitingSnapshot is { } snapshot)
                {
                    return snapshot;
                }
            }
            return null;
        }

        public void CancelChoicesForGame(string gameId)
        {
            _endingGames[gameId] = 0;
            foreach (var choice in _pendingChoices.Values.Where(c =>
                         string.Equals(c.Request.GameId, gameId, StringComparison.OrdinalIgnoreCase)))
            {
                ForceResolveChoice(choice.Request.PendingChoiceId, PlayerChoiceResult.Cancelled);
            }
        }

        public void ClearGameChoiceCancellation(string gameId) => _endingGames.TryRemove(gameId, out _);

        public void ForceResolveChoice(Guid choiceId, PlayerChoiceResult forcedResult)
        {
            if (_pendingChoices.TryRemove(choiceId, out var context))
            {
                _replayRecorder?.ChoiceResolved(choiceId, forcedResult.WasCancelled ? "forced_cancelled" : "forced_selected", forcedResult.ChosenTargetIds);
                context.Tcs.TrySetResult(forcedResult);
                _logger.LogInformation("Force-resolved pending choice {ChoiceId}.", choiceId);
            }
        }

        public static PlayerChoiceResult CreateTimeoutChoiceResult(PlayerChoiceRequest request)
        {
            if (request.IsCancellable)
            {
                return PlayerChoiceResult.Cancelled;
            }

            var selectable = request.SelectableTargetIds ?? new System.Collections.Generic.List<Guid>();
            var min = Math.Max(0, request.MinSelectionCount);
            var count = Math.Min(min, selectable.Count);
            if (count <= 0 || selectable.Count == 0)
            {
                return new PlayerChoiceResult
                {
                    WasCancelled = false,
                    ChosenTargetIds = new System.Collections.Generic.List<Guid>()
                };
            }

            if (count >= selectable.Count)
            {
                return new PlayerChoiceResult
                {
                    WasCancelled = false,
                    ChosenTargetIds = selectable.ToList()
                };
            }

            var chosen = new System.Collections.Generic.List<Guid>(count);
            var used = new System.Collections.Generic.HashSet<int>();
            while (chosen.Count < count)
            {
                var index = Random.Shared.Next(selectable.Count);
                if (used.Add(index))
                {
                    chosen.Add(selectable[index]);
                }
            }

            return new PlayerChoiceResult
            {
                WasCancelled = false,
                ChosenTargetIds = chosen
            };
        }
    }
}
