// Server/Services/AiTurnExecutor.cs
//
// 相关文档:
// - AI决策系统: 文档/05-游戏系统/05-AI决策系统.md
// - 回合管理系统: 文档/05-游戏系统/03-回合管理系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Exceptions;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Domain.Entities; // Added for GameState
using ShadowverseBeyond.Server.Domain.Events.ActionRequests; // For ActionRequest events
using ShadowverseBeyond.Server.Domain.Events; // For IEventProcessor
using AutoMapper; // Added for IMapper
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic; // Added for List<>
using ShadowverseBeyond.Server.Models; // For GameActionContext
using ShadowverseBeyond.Server.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ShadowverseBeyond.Server.Services
{
    public class AiTurnExecutor
    {
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

        public async Task ExecuteTurnAsync(string gameId, Guid aiPlayerId)
        {
            _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI turn execution started.", gameId, aiPlayerId);

            try
            {
                bool actionTakenInLoop;
                do
                {
                    actionTakenInLoop = false;
                    using var scope = _serviceProvider.CreateScope();
                    var scopedServiceProvider = scope.ServiceProvider;
                    var contextAccessor = scopedServiceProvider.GetRequiredService<IGameContextAccessor>();

                    var gameStateEntity = _gameService.GetGameState(gameId);
                    if (gameStateEntity == null || gameStateEntity.IsGameOver || gameStateEntity.CurrentPlayerId != aiPlayerId)
                    {
                        _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Aborting AI turn: Game state invalid, game over, or not AI's turn.", gameId, aiPlayerId);
                        return;
                    }

                    var gameStateDto = _mapper.Map<GameStateDto>(gameStateEntity);

                    // 1. Decide to play a card
                    var playCardAction = await _aiService.DecidePlayCardAsync(gameStateDto, aiPlayerId);
                    if (playCardAction != null)
                    {
                        _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI decided to PLAY CARD: {CardUid}.", gameId, aiPlayerId, playCardAction.CardUid);
                        var wrapper = new PlayerActionWrapper { ActionType = "PlayCard", PlayerId = aiPlayerId, ActionData = playCardAction };
                        await _gameService.EnqueuePlayerActionAsync(gameId, wrapper);
                        actionTakenInLoop = true;
                        await Task.Delay(1000); // 为玩家增加1秒的反应时间
                        continue;
                    }

                    // 2. Decide to evolve a unit
                    var evolveAction = await _aiService.DecideEvolveAsync(gameStateDto, aiPlayerId);
                    if (evolveAction != null)
                    {
                        _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI decided to EVOLVE unit: {UnitUid}.", gameId, aiPlayerId, evolveAction.UnitUid);
                        var wrapper = new PlayerActionWrapper { ActionType = "PerformEvolve", PlayerId = aiPlayerId, ActionData = evolveAction };
                        await _gameService.EnqueuePlayerActionAsync(gameId, wrapper);
                        actionTakenInLoop = true;
                        await Task.Delay(1000); // 为玩家增加1秒的反应时间
                        continue;
                    }

                    // 3. Decide to perform the next single attack
                    var nextAttackAction = await _aiService.DecideNextAttackAsync(gameStateDto, aiPlayerId);
                    if (nextAttackAction != null)
                    {
                        _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI decided to PERFORM ATTACK: {AttackerUid} -> {TargetUid}.",
                            gameId, aiPlayerId, nextAttackAction.AttackerUid, nextAttackAction.TargetUid);

                        var wrapper = new PlayerActionWrapper { ActionType = "PerformAttack", PlayerId = aiPlayerId, ActionData = nextAttackAction };
                        await _gameService.EnqueuePlayerActionAsync(gameId, wrapper);
                        actionTakenInLoop = true;
                        await Task.Delay(1000); // Pause after the attack for observation
                        continue;
                    }

                } while (actionTakenInLoop);

                // 4. If no other actions are taken, end the turn.
                _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI has no more actions and will END TURN.", gameId, aiPlayerId);
                var endTurnWrapper = new PlayerActionWrapper
                {
                    ActionType = "EndTurn",
                    PlayerId = aiPlayerId,
                    ActionData = new { }
                };
                await _gameService.EnqueuePlayerActionAsync(gameId, endTurnWrapper);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AI_EXECUTOR:{GameId}:{PlayerId}] Critical error during AI turn execution.", gameId, aiPlayerId);
                // Emergency End Turn
                try
                {
                    var emergencyEndTurnWrapper = new PlayerActionWrapper
                    {
                        ActionType = "EndTurn",
                        PlayerId = aiPlayerId,
                        ActionData = new { Emergency = true }
                    };
                    await _gameService.EnqueuePlayerActionAsync(gameId, emergencyEndTurnWrapper);
                    _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Enqueued emergency EndTurn due to an error.", gameId, aiPlayerId);
                }
                catch (Exception emergencyEx)
                {
                    _logger.LogError(emergencyEx, "[AI_EXECUTOR:{GameId}:{PlayerId}] Failed to enqueue emergency EndTurn.", gameId, aiPlayerId);
                }
            }
        }
    }
}