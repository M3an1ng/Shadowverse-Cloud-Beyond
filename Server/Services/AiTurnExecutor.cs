using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Models;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

public class AiTurnExecutor
{
    private const int ActionObservationDelayMilliseconds = 1000;
    private readonly IGameService _gameService;
    private readonly IAiService _aiService;
    private readonly ILogger<AiTurnExecutor> _logger;
    private readonly IMapper _mapper;
    private readonly IServiceProvider _serviceProvider;

    public AiTurnExecutor(
        IGameService gameService,
        IAiService aiService,
        ILogger<AiTurnExecutor> logger,
        IMapper mapper,
        IServiceProvider serviceProvider)
    {
        _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task ExecuteTurnAsync(string gameId, Guid aiPlayerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var game = _gameService.GetGameState(gameId);
        if (game == null || game.IsGameOver || game.CurrentPlayerId != aiPlayerId)
        {
            return;
        }

        var turnNumber = game.GetPlayerState(aiPlayerId)!.TurnNumber;
        while (IsCurrentTurn(game, aiPlayerId, turnNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var scope = _serviceProvider.CreateScope();
            var services = scope.ServiceProvider;
            var accessor = services.GetRequiredService<IGameContextAccessor>();
            ShadowverseBeyond.Server.Dtos.GameStateDto snapshot;
            using (var context = new GameContext(services, _mapper, accessor, game, aiPlayerId, cancellationToken))
            {
                snapshot = GameStateProjection.Create(game, context);
            }

            // 保留原来的决策优先级；所有动作共用同一完成边界，避免不同分支再次漂移。
            var actionType = "EndTurn";
            object actionData = new { };
            if (await _aiService.DecidePpBoostAsync(snapshot, aiPlayerId))
            {
                actionType = "PerformPpBoost";
            }
            else if (await _aiService.DecidePlayCardAsync(snapshot, aiPlayerId) is { } playCard)
            {
                actionType = "PlayCard";
                actionData = playCard;
            }
            else if (await _aiService.DecideEvolveAsync(snapshot, aiPlayerId) is { } evolve)
            {
                actionType = "PerformEvolve";
                actionData = evolve;
            }
            else if (await _aiService.DecideNextAttackAsync(snapshot, aiPlayerId) is { } attack)
            {
                actionType = "PerformAttack";
                actionData = attack;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentTurn(game, aiPlayerId, turnNumber))
            {
                return;
            }

            var action = new PlayerActionWrapper(actionType, aiPlayerId, actionData)
            {
                ExpectedTurnNumber = turnNumber,
                CancellationToken = cancellationToken
            };
            if (!await _gameService.EnqueuePlayerActionAsync(gameId, action))
            {
                _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Action {ActionType} was not admitted.",
                    gameId, aiPlayerId, actionType);
                return;
            }

            var completion = await action.Completion.WaitAsync(cancellationToken);
            if (completion.Status == PlayerActionCompletionStatus.Failed)
            {
                throw new InvalidOperationException(completion.Reason);
            }
            if (completion.Status != PlayerActionCompletionStatus.Completed)
            {
                _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Action {ActionType} {Status}: {Reason}",
                    gameId, aiPlayerId, actionType, completion.Status, completion.Reason);
                return;
            }
            if (actionType == "EndTurn")
            {
                return;
            }

            // 演出等待不再充当动作完成证明；取消会立即停止等待及后续决策。
            await Task.Delay(ActionObservationDelayMilliseconds, cancellationToken);
        }
    }

    private bool IsCurrentTurn(GameState game, Guid playerId, int turnNumber)
    {
        return ReferenceEquals(_gameService.GetGameState(game.GameId), game)
            && !game.IsGameOver
            && game.CurrentPlayerId == playerId
            && game.GetPlayerState(playerId)!.TurnNumber == turnNumber;
    }
}
