using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors
{
    /// <summary>
    /// 玩家选择目标选择器
    /// 明确要求玩家从指定的池中选择
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public class PlayerChoiceTargetSelector : ITargetSelector
    {
        private readonly TargetConfig _config;
        private readonly ICardQueryExecutor _queryExecutor;
        private readonly ILogger<PlayerChoiceTargetSelector> _logger;

        public PlayerChoiceTargetSelector(TargetConfig config, ICardQueryExecutor queryExecutor, ILogger<PlayerChoiceTargetSelector> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 异步选择需要玩家手动选择的目标
        /// </summary>
        /// <param name="config">目标配置</param>
        /// <param name="context">统一游戏上下文</param>
        /// <returns>玩家选择的目标ID列表</returns>
        public async Task<List<TargetId>> SelectTargetsAsync(TargetConfig config, GameContext context)
        {
            // 检查是否有玩家选择配置
            if (config.PlayerChoiceOptions == null)
            {
                return new List<TargetId>();
            }

            try
            {
                // 1. 获取可选目标池
                var availableTargets = await GetAvailableTargets(config, context);

                if (!availableTargets.Any())
                {
                    return new List<TargetId>();
                }

                // 2. 检查是否可以自动选择
                if (config.PlayerChoiceOptions.AutoSelect)
                {
                    var maxCount = config.PlayerChoiceOptions.MaxCount ?? availableTargets.Count;
                    var minCount = config.PlayerChoiceOptions.MinCount;

                    // 如果可选目标数量小于等于最小数量要求，自动选择全部
                    if (availableTargets.Count <= minCount)
                    {
                        return availableTargets;
                    }
                }

                // 3. 如果不需要玩家选择，返回所有可用目标
                if (!config.PlayerChoiceOptions.IsRequired)
                {
                    return availableTargets;
                }

                // 4. 请求玩家选择（这里需要与UI系统集成）
                var selectedTargets = await RequestPlayerChoice(config, context, availableTargets);

                return selectedTargets;
            }
            catch (Exception)
            {
                // 选择失败时的处理
                if (config.PlayerChoiceOptions.CanCancel)
                {
                    return new List<TargetId>();
                }

                // 如果不能取消，抛出异常
                throw;
            }
        }

        /// <summary>
        /// 获取可选目标池
        /// 使用依赖注入的查询执行器和日志记录器来创建过滤选择器
        /// </summary>
        /// <param name="config">目标配置</param>
        /// <param name="context">统一游戏上下文</param>
        /// <returns>可选目标列表</returns>
        private async Task<List<TargetId>> GetAvailableTargets(TargetConfig config, GameContext context)
        {
            try
            {
                _logger.LogDebug("[PlayerChoiceTargetSelector] Getting available targets for player choice. Query: {Query}",
                    config.Query?.SourcePoolExpression ?? "None");

                // 如果有查询配置，使用过滤选择器获取目标池
                if (config.Query != null && !string.IsNullOrEmpty(config.Query.SourcePoolExpression))
                {
                    // 使用注入的依赖创建过滤选择器
                    var filteredSelector = new FilteredTargetSelector(_queryExecutor,
                        _logger as ILogger<FilteredTargetSelector> ??
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<FilteredTargetSelector>.Instance);

                    var targets = await filteredSelector.SelectTargetsAsync(config, context);

                    // Apply zone-based filtering for performance optimization
                    var filteredTargets = ApplyFiltering(targets, context);

                    _logger.LogDebug("[PlayerChoiceTargetSelector] Found {Count} available targets from query, {FilteredCount} after zone filtering",
                        targets.Count, filteredTargets.Count);
                    return filteredTargets;
                }

                // 如果没有查询配置，尝试从上下文中获取默认目标
                var defaultTargets = GetDefaultTargetsFromContext(context);

                _logger.LogDebug("[PlayerChoiceTargetSelector] No query config, using default targets. Count: {Count}", defaultTargets.Count);
                return defaultTargets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlayerChoiceTargetSelector] Error getting available targets");

                // 根据失败处理策略决定是否抛出异常
                if (config.OnFailure == FailureHandling.AbortAbility)
                {
                    throw;
                }

                // 返回空列表作为降级处理
                return new List<TargetId>();
            }
        }

        /// <summary>
        /// 请求玩家选择目标
        /// </summary>
        /// <param name="config">目标配置</param>
        /// <param name="context">统一游戏上下文</param>
        /// <param name="availableTargets">可选目标列表</param>
        /// <returns>玩家选择的目标列表</returns>
        private async Task<List<TargetId>> RequestPlayerChoice(TargetConfig config, GameContext context, List<TargetId> availableTargets)
        {
            // TODO: 实现与UI系统的交互
            // 这需要：
            // 1. 通过SignalR向客户端发送目标选择请求，包含Count限制信息
            // 2. 等待客户端响应
            // 3. 验证选择的有效性（数量和目标合法性）
            // 4. 返回选择结果

            // 临时实现：根据Count限制返回目标
            await Task.Delay(100); // 模拟异步操作

            if (availableTargets.Any())
            {
                var maxCount = config.PlayerChoiceOptions?.MaxCount ?? 1;
                var minCount = config.PlayerChoiceOptions?.MinCount ?? 1;

                // 临时逻辑：返回最小数量的目标
                var targetCount = Math.Min(Math.Max(minCount, 1), Math.Min(maxCount, availableTargets.Count));
                return availableTargets.Take(targetCount).ToList();
            }

            return new List<TargetId>();
        }

        /// <summary>
        /// 从游戏上下文中获取默认可选目标
        /// 当没有明确的查询配置时使用
        /// </summary>
        /// <param name="context">统一游戏上下文</param>
        /// <returns>默认目标列表</returns>
        private List<TargetId> GetDefaultTargetsFromContext(GameContext context)
        {
            var targets = new List<TargetId>();

            try
            {
                // 如果有源卡牌，将其作为默认目标
                if (context.SourceCard != null)
                {
                    targets.Add(TargetId.Card(context.SourceCard.InstanceId));
                    _logger.LogTrace("[PlayerChoiceTargetSelector] Added source card as default target: {CardId}",
                        context.SourceCard.InstanceId);
                }

                // 如果有当前玩家，将其作为默认目标
                if (context.RequestingPlayer != null)
                {
                    targets.Add(TargetId.Player(context.RequestingPlayer.PlayerId));
                    _logger.LogTrace("[PlayerChoiceTargetSelector] Added current player as default target: {PlayerId}",
                        context.RequestingPlayer.PlayerId);
                }

                // 可以根据需要添加更多默认目标逻辑
                // 例如：场上的所有卡牌、对手等

                return targets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlayerChoiceTargetSelector] Error getting default targets from context");
                return new List<TargetId>();
            }
        }

        /// <summary>
        /// 目标选择
        /// </summary>
        /// <param name="targets">原始目标列表</param>
        /// <param name="context">游戏上下文</param>
        /// <returns>过滤后的目标列表</returns>
        private List<TargetId> ApplyFiltering(List<TargetId> targets, GameContext context)
        {
            if (targets == null || targets.Count == 0)
                return targets;

            try
            {
                var filteredTargets = new List<TargetId>();
                var filteredCount = 0;

                foreach (var targetId in targets)
                {
                    // 只对卡牌目标应用区域过滤
                    if (targetId.Type == TargetIdType.CardInstance)
                    {
                        // 尝试从游戏状态中找到对应的卡牌实例
                        var cardInstance = FindCardInstanceById(targetId.Id, context);
                        if (cardInstance != null) filteredTargets.Add(targetId);
                    }
                    else
                    {
                        // 非卡牌目标（如玩家）直接保留
                        filteredTargets.Add(targetId);
                    }
                }

                if (filteredCount > 0)
                {
                    _logger.LogDebug("[PlayerChoiceTargetSelector] Zone filtering removed {FilteredCount} targets from inactive zones",
                        filteredCount);
                }

                return filteredTargets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlayerChoiceTargetSelector] Error applying zone-based filtering, returning original targets");
                return targets; // 出错时返回原始目标列表
            }
        }

        /// <summary>
        /// 根据ID在游戏状态中查找卡牌实例
        /// </summary>
        /// <param name="cardId">卡牌实例ID</param>
        /// <param name="context">游戏上下文</param>
        /// <returns>找到的卡牌实例，如果未找到则返回null</returns>
        private CardInstance? FindCardInstanceById(Guid cardId, GameContext context)
        {
            try
            {
                if (context.CurrentGameState == null)
                    return null;

                // 搜索所有玩家的所有区域
                foreach (var player in new[] { context.CurrentGameState.Player1, context.CurrentGameState.Player2 })
                {
                    if (player == null) continue;

                    // 搜索各个区域
                    var allCards = new List<CardInstance>();

                    if (player.Hand != null) allCards.AddRange(player.Hand);
                    if (player.Field != null) allCards.AddRange(player.Field);
                    if (player.Deck != null) allCards.AddRange(player.Deck);
                    if (player.Graveyard != null) allCards.AddRange(player.Graveyard);
                    if (player.Leader != null) allCards.Add(player.Leader);

                    var foundCard = allCards.FirstOrDefault(card => card.InstanceId == cardId);
                    if (foundCard != null)
                        return foundCard;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PlayerChoiceTargetSelector] Error finding card instance by ID {CardId}", cardId);
                return null;
            }
        }
    }
}
