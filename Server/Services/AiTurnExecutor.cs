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

namespace ShadowverseBeyond.Server.Services
{
    public class AiTurnExecutor
    {
        private readonly IGameService _gameService; // To interact with game state and execute actions
        private readonly IAiService _aiService;     // To get AI decisions
        private readonly ILogger<AiTurnExecutor> _logger;
        private readonly IMapper _mapper; // Added for Entity to DTO mapping

        // Constructor injection
        public AiTurnExecutor(
            IGameService gameService,
            IAiService aiService,
            ILogger<AiTurnExecutor> logger,
            IMapper mapper)
        {
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        /// <summary>
        /// Executes the AI's turn by making decisions and enqueuing actions in a loop
        /// until no more actions can be taken.
        /// </summary>
        public async Task ExecuteTurnAsync(string gameId, Guid aiPlayerId)
        {
            _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI turn execution started.", gameId, aiPlayerId);

            try
            {
                while (true)
                {
                    var gameStateEntity = _gameService.GetGameState(gameId);
                    if (gameStateEntity == null)
                    {
                        _logger.LogError("[AI_EXECUTOR:{GameId}:{PlayerId}] Failed to retrieve game state. Aborting AI turn.", gameId, aiPlayerId);
                        return;
                    }

                    if (gameStateEntity.IsGameOver || gameStateEntity.CurrentPlayerId != aiPlayerId)
                    {
                        _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Not AI's turn or game is over. Aborting AI turn.", gameId, aiPlayerId);
                        return;
                    }

                    var gameStateDto = _mapper.Map<GameStateDto>(gameStateEntity);
                    bool actionTakenThisIteration = false;

                    // 1. Decide to Play a Card
                    var playCardAction = await _aiService.DecidePlayCardAsync(gameStateDto, aiPlayerId);
                    if (playCardAction != null)
                    {
                        _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI decided to play card: {CardUid}.", gameId, aiPlayerId, playCardAction.CardUid);
                        var wrapper = new PlayerActionWrapper { ActionType = "PlayCard", PlayerId = aiPlayerId, ActionData = playCardAction };
                        _gameService.EnqueuePlayerAction(gameId, wrapper);
                        actionTakenThisIteration = true;
                    }

                    // 2. Decide to Evolve a Unit
                    var evolveAction = await _aiService.DecideEvolveAsync(gameStateDto, aiPlayerId);
                    if (evolveAction != null)
                    {
                        _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI decided to evolve unit: {UnitUid}.", gameId, aiPlayerId, evolveAction.UnitUid);
                        var wrapper = new PlayerActionWrapper { ActionType = "PerformEvolve", PlayerId = aiPlayerId, ActionData = evolveAction };
                        _gameService.EnqueuePlayerAction(gameId, wrapper);
                        actionTakenThisIteration = true;
                    }

                    // 3. Decide to Attack
                    var attackActions = await _aiService.DecideAttacksAsync(gameStateDto, aiPlayerId);
                    if (attackActions != null && attackActions.Any())
                    {
                        _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI decided to perform {Count} attacks.", gameId, aiPlayerId, attackActions.Count);
                        foreach (var attackAction in attackActions)
                        {
                            var wrapper = new PlayerActionWrapper { ActionType = "PerformAttack", PlayerId = aiPlayerId, ActionData = attackAction };
                            _gameService.EnqueuePlayerAction(gameId, wrapper);
                        }
                        actionTakenThisIteration = true;
                    }

                    if (actionTakenThisIteration)
                    {
                        // Wait for the action(s) to be processed by the game loop.
                        // This is a simplification. A more robust solution would involve a completion signal from GameService.
                        await Task.Delay(1000); // Adjust delay as needed

                        continue; // Re-evaluate state for the next action
                    }

                    // If no action was taken in this iteration, break the loop to end the turn.
                    break;
                }

                // 4. End Turn
                _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI has no more actions and will end the turn.", gameId, aiPlayerId);
                var endTurnWrapper = new PlayerActionWrapper
                {
                    ActionType = "EndTurn",
                    PlayerId = aiPlayerId,
                    ActionData = new { } // No data needed
                };
                _gameService.EnqueuePlayerAction(gameId, endTurnWrapper);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AI_EXECUTOR:{GameId}:{PlayerId}] Critical error during AI turn execution.", gameId, aiPlayerId);
                // Attempt to end the turn gracefully to prevent the game from stalling.
                try
                {
                    var emergencyEndTurnWrapper = new PlayerActionWrapper
                    {
                        ActionType = "EndTurn",
                        PlayerId = aiPlayerId,
                        ActionData = new { Emergency = true }
                    };
                    _gameService.EnqueuePlayerAction(gameId, emergencyEndTurnWrapper);
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