using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Entities; // Corrected namespace
using ShadowverseBeyond.Server.Domain.Query; // Add missing using
using Microsoft.Extensions.Logging; // Add missing using
using ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors; // Add missing using
using ShadowverseBeyond.Server.Dtos; // Add missing using for GameStateDto

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

        // Add constructor to inject dependencies
        public TargetSelectionService(ICardQueryExecutor queryExecutor, ILogger<FilteredTargetSelector> logger)
        {
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 根据配置选择目标
        /// </summary>
        /// <param name="config">目标选择器配置</param>
        /// <param name="context">能力上下文</param> // Updated parameter name
        /// <returns>选定的目标ID列表</returns>
        // Updated signature to async and AbilityContext
        public async Task<List<TargetId>> SelectTargetsAsync(TargetConfig config, AbilityContext context)
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
        /// <param name="context">能力上下文</param> // Updated parameter description
        /// <returns>是否有效</returns>
        // Updated signature to use AbilityContext
        public bool IsValidTarget(TargetId targetId, AbilityContext context)
        {
            if (targetId.IsEmpty)
                return false;

            // 在游戏状态中查找目标 (Use AbilityContext.CurrentGameState)
            var target = FindCardInstance(targetId, context.CurrentGameState);
            return target != null;
        }

        /// <summary>
        /// 根据TargetId查找CardInstance
        /// </summary>
        /// <param name="targetId">目标ID</param>
        /// <param name="gameStateDto">游戏状态 DTO</param> // Updated parameter name and type
        /// <returns>卡牌实例 DTO，如果未找到则返回null</returns> // Updated return description
        // Updated method signature to accept GameStateDto
        public CardInstanceDto? FindCardInstance(TargetId targetId, GameStateDto gameStateDto)
        {
            if (targetId.IsEmpty || gameStateDto == null)
                return null;

            // Try parsing the string ID to Guid
            if (!Guid.TryParse(targetId.Id, out var targetGuid))
            {
                // If parsing fails, the ID is not a valid Guid, so it cannot match a CardInstanceDto.InstanceId
                return null;
            }

            // 检查领主卡牌 (Iterate through Players list and compare Guids)
            foreach (var playerState in gameStateDto.Players)
            {
                // Use correct property name: LeaderCard and compare Guids
                if (playerState?.LeaderCard?.InstanceId == targetGuid)
                    return playerState.LeaderCard;
            }

            // 在所有玩家的所有区域中查找 (Iterate through Players list and compare Guids)
            foreach (var playerState in gameStateDto.Players)
            {
                if (playerState == null) continue; // Skip if player state is null

                // 检查场上卡牌 (Use correct property name: Field and compare Guids)
                var fieldCard = playerState.Field?.FirstOrDefault(c => c.InstanceId == targetGuid);
                if (fieldCard != null)
                    return fieldCard;

                // 检查手牌 (Use correct property name: Hand and compare Guids)
                var handCard = playerState.Hand?.FirstOrDefault(c => c.InstanceId == targetGuid);
                if (handCard != null)
                    return handCard;

                // 检查牌库 (Use correct property name: Deck - Keep commented out for now)
                // Note: Searching the deck might not be standard, confirm if needed.
                // var deckCard = playerState.Deck?.FirstOrDefault(c => c.InstanceId == targetGuid);
                // if (deckCard != null)
                //     return deckCard;

                // 检查墓地 (Use correct property name: Graveyard and compare Guids)
                var graveyardCard = playerState.Graveyard?.FirstOrDefault(c => c.InstanceId == targetGuid);
                if (graveyardCard != null)
                    return graveyardCard;
            }

            return null;
        }

        /// <summary>
        /// 获取可选目标列表（用于UI显示）
        /// </summary>
        /// <param name="config">目标选择器配置</param>
        /// <param name="context">能力上下文</param> // Updated parameter name
        /// <returns>可选目标列表</returns>
        // Updated signature to async and AbilityContext
        // Updated return type to IEnumerable<CardInstanceDto>
        public async Task<IEnumerable<CardInstanceDto>> GetSelectableTargetsAsync(TargetConfig config, AbilityContext context)
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
                    // Use AbilityContext.CurrentGameState (which is GameStateDto)
                    var card = FindCardInstance(targetId, context.CurrentGameState); // Pass DTO
                    if (card != null) // card is now CardInstanceDto?
                        targets.Add(card);
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
        /// <param name="context">能力上下文</param> // Updated parameter name
        // Updated context parameter type
        private void HandleSelectionFailure(FailureHandling failureHandling, Exception exception, AbilityContext context)
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
