using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Hubs.Filters
{
    /// <summary>
    /// A SignalR Hub filter that creates and manages the GameContext for each Hub method invocation.
    /// This filter is responsible for setting up the ambient context required for game logic to operate correctly.
    /// It leverages the existing DI scope provided by SignalR for each Hub activation.
    /// </summary>
    public class GameContextFilter : IHubFilter
    {
        private readonly IGameContextAccessor _gameContextAccessor;
        private readonly IGameStateManager _gameStateManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMapper _mapper;
        private readonly ILogger<GameContextFilter> _logger;

        public GameContextFilter(
            IGameContextAccessor gameContextAccessor,
            IGameStateManager gameStateManager,
            IServiceProvider serviceProvider,
            IMapper mapper,
            ILogger<GameContextFilter> logger)
        {
            _gameContextAccessor = gameContextAccessor;
            _gameStateManager = gameStateManager;
            _serviceProvider = serviceProvider;
            _mapper = mapper;
            _logger = logger;
        }

        public async ValueTask<object?> InvokeMethodAsync(
            HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object?>> next)
        {
            // Some Hub methods (like OnConnectedAsync) might not have a game context.
            // We'll only create a context if we can find a gameId in the connection's items.
            var gameId = invocationContext.Context.Items["GameId"] as string;
            if (string.IsNullOrEmpty(gameId))
            {
                _logger.LogTrace("No GameId found in HubContext.Items. Skipping GameContext creation for {MethodName}.", invocationContext.HubMethodName);
                return await next(invocationContext);
            }

            // Prefer UserIdentifier, fallback to Items["PlayerId"] set by GameHub validation
            Guid playerId;
            var userIdentifier = invocationContext.Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userIdentifier) && Guid.TryParse(userIdentifier, out var parsedUser))
            {
                playerId = parsedUser;
            }
            else if (invocationContext.Context.Items.TryGetValue("PlayerId", out var playerIdObj) &&
                     playerIdObj is string playerIdStr &&
                     Guid.TryParse(playerIdStr, out var parsedFromItems))
            {
                playerId = parsedFromItems;
            }
            else
            {
                _logger.LogWarning("UserIdentifier/PlayerId missing or invalid for {MethodName}. Cannot create GameContext.", invocationContext.HubMethodName);
                RuntimeGuardrailMetrics.RecordCannotCreateGameContext("missing_or_invalid_player_id", gameId, invocationContext.HubMethodName);
                return await next(invocationContext);
            }

            // Use the existing async method to retrieve the game state.
            var gameState = await _gameStateManager.GetGameStateAsync(gameId);
            if (gameState == null)
            {
                _logger.LogWarning("Failed to find game state for GameId: {GameId}. Cannot create GameContext.", gameId);
                RuntimeGuardrailMetrics.RecordCannotCreateGameContext("missing_game_state", gameId, invocationContext.HubMethodName);
                // Let the Hub method handle the logic of a missing game state.
                return await next(invocationContext);
            }

            // Create the pure, immutable context for this scope.
            // The new GameContext constructor requires the accessor and handles setting the context itself.
            var context = new GameContext(
                _serviceProvider,
                _mapper,
                _gameContextAccessor,
                gameState,
                playerId,
                invocationContext.Context.ConnectionAborted
            );
            _logger.LogDebug("GameContext created and set for GameId: {GameId}, PlayerId: {PlayerId}", gameId, playerId);

            try
            {
                // Continue the invocation pipeline.
                return await next(invocationContext);
            }
            finally
            {
                // Clear the context after the Hub method has finished executing.
                _gameContextAccessor.GameContext = null;
                _logger.LogDebug("GameContext cleared for GameId: {GameId}", gameId);
            }
        }
    }
}
