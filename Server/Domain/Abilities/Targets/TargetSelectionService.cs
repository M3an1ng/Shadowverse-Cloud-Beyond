using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper; // Added for IMapper
using ShadowverseBeyond.Server.Domain.Entities; // Corrected namespace
using ShadowverseBeyond.Server.Domain.Query; // Add missing using
using Microsoft.Extensions.Logging; // Add missing using
using ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors; // Add missing using
using ShadowverseBeyond.Server.Dtos; // Add missing using for GameStateDto
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext
using ShadowverseBeyond.Server.Domain.Abilities; // Added for AbilityContext (still needed for some methods)

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// 目标选择服务
    /// 提供统一的目标选择接口和管理功能
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public class TargetSelectionService
    {
        private readonly ICardQueryExecutor _queryExecutor; // Added dependency field
        private readonly ILogger<FilteredTargetSelector> _logger; // Added dependency field
        private readonly IMapper _mapper; // Added IMapper field

        // Add constructor to inject dependencies
        public TargetSelectionService(ICardQueryExecutor queryExecutor, ILogger<FilteredTargetSelector> logger, IMapper mapper) // Inject IMapper
        {
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper)); // Store IMapper
        }

        /// <summary>
        /// 根据配置选择目标
        /// </summary>
        /// <param name="config">目标选择器配置</param>
        /// <param name="context">统一游戏上下文</param> // Updated parameter name
        /// <returns>选定的目标ID列表</returns>
        // Updated signature to use unified GameContext
        public async Task<List<TargetId>> SelectTargetsAsync(TargetConfig config, GameContext context)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // Updated context type check
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                // Pass dependencies to the factory
                var selector = TargetSelectorFactory.CreateSelector(config, _queryExecutor, _logger);
                // Updated call to async version
                return await selector.SelectTargetsAsync(config, context);
            }
            catch (Exception ex)
            {
                // 根据配置处理失败情况
                // Updated context type in call
                HandleSelectionFailure(config.OnFailure, ex, context);
                // Return empty list for async method
                return new List<TargetId>();
            }
        }

        /// <summary>
        /// 验证目标是否有效
        /// </summary>
        /// <param name="targetId">目标ID</param>
        /// <param name="context">统一游戏上下文</param> // Updated parameter description
        /// <returns>是否有效</returns>
        // Updated signature to use GameContext
        public bool IsValidTarget(TargetId targetId, GameContext context)
        {
            if (targetId.IsEmpty)
                return false;

            // 在游戏状态中查找目标实体 (Use AutoMapper)
            // Ensure CurrentGameState is not null before mapping
            if (context.CurrentGameState == null)
            {
                // Consider logging a warning or error
                return false;
            }
            var gameStateDto = _mapper.Map<GameStateDto>(context.CurrentGameState);
            // Call the renamed method
            var targetEntity = FindTargetEntityById(targetId, gameStateDto);
            // Target is valid if an entity (Player or Card) was found
            return targetEntity != null;
        }

        /// <summary>
        /// 根据 TargetId 查找目标实体 (玩家状态 DTO 或卡牌实例 DTO)。
        /// </summary>
        /// <param name="targetId">目标ID (包含类型和Guid)</param>
        /// <param name="gameStateDto">游戏状态 DTO</param>
        /// <returns>找到的目标实体 (PlayerStateDto 或 CardInstanceDto) 或 null</returns>
        public object? FindTargetEntityById(TargetId targetId, GameStateDto? gameStateDto) // Allow nullable GameStateDto
        {
            // Check for null GameStateDto early
            // Check for null GameStateDto early
            if (targetId.IsEmpty || gameStateDto?.Players == null)
            {
                // Log if the targetId wasn't empty but gameStateDto or Players was null
                if (!targetId.IsEmpty) _logger.LogTrace("FindTargetEntityById called with valid TargetId but null GameStateDto or Players list."); // Use _logger
                return null;
            }

            var targetGuid = targetId.Id;

            // 根据 TargetId.Type 决定查找逻辑
            if (targetId.Type == TargetIdType.Player)
            {
                _logger.LogTrace("Attempting to find Player target with ID: {PlayerId}", targetGuid); // Use _logger
                // 查找玩家状态 DTO
                // Ensure Players list itself is not null before querying (already checked above)
                var player = gameStateDto.Players.FirstOrDefault(p => p?.PlayerId == targetGuid);
                if (player != null) _logger.LogTrace("Found Player target: {PlayerId}", player.PlayerId); // Use _logger
                else _logger.LogTrace("Player target with ID {PlayerId} not found.", targetGuid); // Use _logger
                return player;
            }
            else if (targetId.Type == TargetIdType.CardInstance)
            {
                _logger.LogTrace("Attempting to find CardInstance target with ID: {InstanceId}", targetGuid); // Use _logger
                // 查找卡牌实例 DTO (原有逻辑)
                // Ensure Players list itself is not null before iterating
                if (gameStateDto.Players == null) return null;

                // 检查领主卡牌
                foreach (var playerState in gameStateDto.Players)
                {
                    // Check playerState and LeaderCard before accessing InstanceId
                    if (playerState?.LeaderCard?.InstanceId == targetGuid)
                    {
                        _logger.LogTrace("Found CardInstance target (Leader) with ID: {InstanceId}", targetGuid); // Use _logger
                        return playerState.LeaderCard;
                    }
                }

                // 在所有玩家的所有区域中查找
                foreach (var playerState in gameStateDto.Players)
                {
                    if (playerState == null) continue;

                    // Check Field, Hand, Graveyard before querying and accessing InstanceId
                    var fieldCard = playerState.Field?.FirstOrDefault(c => c?.InstanceId == targetGuid);
                    if (fieldCard != null)
                    {
                        _logger.LogTrace("Found CardInstance target (Field) with ID: {InstanceId}", targetGuid); // Use _logger
                        return fieldCard;
                    }

                    var handCard = playerState.Hand?.FirstOrDefault(c => c?.InstanceId == targetGuid);
                    if (handCard != null)
                    {
                        _logger.LogTrace("Found CardInstance target (Hand) with ID: {InstanceId}", targetGuid); // Use _logger
                        return handCard;
                    }

                    var graveyardCard = playerState.Graveyard?.FirstOrDefault(c => c?.InstanceId == targetGuid);
                    if (graveyardCard != null)
                    {
                        _logger.LogTrace("Found CardInstance target (Graveyard) with ID: {InstanceId}", targetGuid); // Use _logger
                        return graveyardCard;
                    }

                    // Deck search usually avoided
                }
                _logger.LogTrace("CardInstance target with ID {InstanceId} not found in any player zone.", targetGuid); // Use _logger
                return null; // CardInstance not found
            }
            else // Undefined or other types
            {
                _logger.LogWarning("FindTargetEntityById called with unhandled TargetType: {TargetType}", targetId.Type); // Use _logger
                return null;
            }
        }


        /// <summary>
        /// 获取可选目标列表（用于UI显示）
        /// </summary>
        /// <param name="config">目标选择器配置</param>
        /// <param name="context">统一游戏上下文</param> // Updated parameter name
        /// <returns>可选目标列表</returns>
        // Updated signature to use GameContext
        // Updated return type to IEnumerable<CardInstanceDto>
        public async Task<IEnumerable<CardInstanceDto>> GetSelectableTargetsAsync(TargetConfig config, GameContext context)
        {
            // Updated context type check
            if (config == null || context == null)
                return Enumerable.Empty<CardInstanceDto>(); // Return empty DTO enumerable

            try
            {
                // Pass dependencies to the factory
                var selector = TargetSelectorFactory.CreateSelector(config, _queryExecutor, _logger);
                // Updated call to async version
                var targetIds = await selector.SelectTargetsAsync(config, context);

                var targets = new List<CardInstanceDto>(); // List of DTOs
                foreach (var targetId in targetIds)
                {
                    // Use AutoMapper to get GameStateDto
                    // Ensure CurrentGameState is not null before mapping
                    if (context.CurrentGameState == null)
                    {
                        _logger.LogWarning("GetSelectableTargetsAsync skipped targetId {TargetId} due to null CurrentGameState.", targetId); // Use _logger
                        continue; // Skip this targetId if game state is missing
                    }

                    var gameStateDto = _mapper.Map<GameStateDto>(context.CurrentGameState);
                    // Call the renamed method, but only process CardInstanceDto results for this specific method
                    var targetEntity = FindTargetEntityById(targetId, gameStateDto);
                    if (targetEntity is CardInstanceDto card) // Check if the found entity is a CardInstanceDto
                    {
                        targets.Add(card);
                    }
                    else if (targetEntity != null)
                    {
                        // Log if a non-card target was found where only cards are expected for UI selection
                        _logger.LogWarning("GetSelectableTargetsAsync found a non-card target ({TargetType}) with ID {TargetId}, which will be ignored for UI selection.", targetId.Type, targetId.Id); // Use _logger
                    }
                    // If targetEntity is null, FindTargetEntityById already logged it or the ID was invalid.
                }

                return targets; // Return list of DTOs
            }
            catch
            {
                // TODO: Log exception?
                return Enumerable.Empty<CardInstanceDto>(); // Return empty DTO enumerable
            }
        }

        /// <summary>
        /// 处理选择失败
        /// </summary>
        /// <param name="failureHandling">失败处理方式</param>
        /// <param name="exception">异常信息</param>
        /// <param name="context">统一游戏上下文</param> // Updated parameter name
        // Updated context parameter type
        private void HandleSelectionFailure(FailureHandling failureHandling, Exception exception, GameContext context)
        {
            switch (failureHandling)
            {
                case FailureHandling.AbortAbility:
                    throw new TargetSelectionException("目标选择失败，中止能力执行", exception);

                case FailureHandling.SkipEffect:
                    // 记录日志但不抛出异常
                    // TODO: 添加日志记录
                    break;

                case FailureHandling.LogWarning:
                    // 记录警告日志
                    // TODO: 添加日志记录
                    break;
            }
        }
    }

    /// <summary>
    /// 目标选择异常
    /// </summary>
    public class TargetSelectionException : Exception
    {
        public TargetSelectionException(string message) : base(message) { }
        public TargetSelectionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
