using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static ShadowverseBeyond.Server.Domain.Abilities.Common.SelectorType;

namespace ShadowverseBeyond.Server.Domain.Abilities.Runtime
{
    /// <summary>
    /// 运行时目标包装类
    /// 包装TargetConfig并添加当前目标解析
    /// </summary>
    public class Target
    {
        /// <summary>
        /// 静态目标配置
        /// </summary>
        public TargetConfig Config { get; }

        /// <summary>
        /// 当前解析的目标
        /// </summary>
        public List<CardInstance> ResolvedTargets { get; private set; } = [];

        /// <summary>
        /// 目标解析次数
        /// </summary>
        public int ResolutionCount { get; private set; }

        /// <summary>
        /// 上次解析时间
        /// </summary>
        public DateTime? LastResolutionTime { get; private set; }

        /// <summary>
        /// 是否需要重新解析
        /// </summary>
        public bool NeedsResolution { get; private set; } = true;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 目标解析历史
        /// </summary>
        public List<TargetResolutionResult> ResolutionHistory { get; } = [];

        /// <summary>
        /// 查询执行器
        /// </summary>
        private readonly ICardQueryExecutor? _queryExecutor;

        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger? _logger;

        public Target(TargetConfig config, ICardQueryExecutor? queryExecutor = null, ILogger? logger = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _queryExecutor = queryExecutor;
            _logger = logger;
        }

        /// <summary>
        /// 解析目标
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>解析的目标列表</returns>
        public async Task<List<CardInstance>> ResolveTargetsAsync(GameContext context)
        {
            if (!IsEnabled)
                return [];

            var startTime = DateTime.UtcNow;
            List<CardInstance> targets;

            try
            {
                targets = await ResolveTargetLogic(context);
            }
            catch (Exception ex)
            {
                RecordResolutionResult(new TargetResolutionResult
                {
                    Success = false,
                    Targets = [],
                    ErrorMessage = ex.Message,
                    ResolutionTime = startTime,
                    Duration = DateTime.UtcNow - startTime
                });
                throw;
            }

            // 更新状态
            ResolvedTargets = targets;
            ResolutionCount++;
            LastResolutionTime = DateTime.UtcNow;
            NeedsResolution = false;

            // 记录解析结果
            RecordResolutionResult(new TargetResolutionResult
            {
                Success = true,
                Targets = targets,
                ResolutionTime = startTime,
                Duration = DateTime.UtcNow - startTime
            });

            return targets;
        }

        /// <summary>
        /// 实际的目标解析逻辑
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>解析的目标列表</returns>
        private async Task<List<CardInstance>> ResolveTargetLogic(GameContext context)
        {
            var targets = new List<CardInstance>();

            switch (Config.SelectorType)
            {
                case Self:
                    if (context.SourceCard != null)
                        targets.Add(context.SourceCard);
                    break;

                case Filtered:
                    targets = await GetFilteredTargets(context);
                    break;

                case PlayerChoice:
                    targets = await GetPlayerChoiceTargets(context);
                    break;

                case FromVariable:
                    targets = GetTargetsFromVariable(context);
                    break;

                case TriggeringEventData:
                    targets = GetTargetsFromTriggeringEvent(context);
                    break;

                case Composite:
                    targets = await GetCompositeTargets(context);
                    break;

                default:
                    // 未知的选择器类型，返回空列表
                    targets = [];
                    break;
            }

            return targets;
        }

        /// <summary>
        /// 获取过滤的目标
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>过滤的目标列表</returns>
        private async Task<List<CardInstance>> GetFilteredTargets(GameContext context)
        {
            if (_queryExecutor == null)
            {
                _logger?.LogWarning("QueryExecutor is null, cannot execute filtered target query");
                return [];
            }

            try
            {
                // 执行查询配置
                var queryResult = await _queryExecutor.ExecuteQueryAsync(Config.Query, context);

                if (!queryResult.Success)
                {
                    _logger?.LogWarning("Query execution failed for filtered targets");
                    return [];
                }

                // 将查询结果转换为CardInstance列表
                var targets = new List<CardInstance>();
                foreach (var result in queryResult.Results)
                {
                    if (result is CardInstance cardInstance)
                    {
                        targets.Add(cardInstance);
                    }
                    else if (result is CardInstanceDto dto)
                    {
                        // 如果需要，可以添加DTO到实体的转换逻辑
                        _logger?.LogDebug("Received CardInstanceDto, conversion to CardInstance needed");
                    }
                }

                return targets;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing filtered target query");
                return [];
            }
        }

        /// <summary>
        /// 获取玩家选择的目标
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>玩家选择的目标列表</returns>
        private async Task<List<CardInstance>> GetPlayerChoiceTargets(GameContext context)
        {
            if (Config.PlayerChoiceOptions == null)
            {
                _logger?.LogWarning("PlayerChoice selector configured but PlayerChoiceOptions is null");
                return [];
            }

            try
            {
                // 1. 首先获取可选的目标池
                var availableTargets = await GetAvailableTargetsForPlayerChoice(context);

                if (availableTargets.Count == 0)
                {
                    _logger?.LogDebug("No available targets for player choice");
                    return [];
                }

                // 2. 检查是否可以自动选择（基于PlayerChoiceTargetSelector的逻辑）
                if (Config.PlayerChoiceOptions.AutoSelect)
                {
                    var maxCount = Config.PlayerChoiceOptions.MaxCount ?? availableTargets.Count;
                    var minCount = Config.PlayerChoiceOptions.MinCount;

                    // 在设置自动选择的情况下，如果可选目标数量小于等于最小数量要求，自动选择全部
                    if (availableTargets.Count <= minCount)
                    {
                        _logger?.LogDebug("Auto-selecting all {Count} targets (equals minimum required)", availableTargets.Count);
                        return availableTargets;
                    }
                }

                // 3. 如果不需要玩家选择，返回所有可用目标（基于PlayerChoiceTargetSelector的逻辑）
                if (!Config.PlayerChoiceOptions.IsRequired)
                {
                    _logger?.LogDebug("Player choice not required, returning all {Count} available targets", availableTargets.Count);
                    return availableTargets;
                }

                // 4. 实际的玩家选择逻辑（这里需要与UI系统集成）
                var selectedTargets = await RequestPlayerChoiceAsync(availableTargets, Config.PlayerChoiceOptions);

                _logger?.LogDebug("Player choice completed. Selected {SelectedCount} targets from {AvailableCount} available",
                    selectedTargets.Count, availableTargets.Count);

                return selectedTargets;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in player choice target selection");

                if (Config.PlayerChoiceOptions.CanCancel)
                {
                    return [];
                }

                throw;
            }
        }

        /// <summary>
        /// 检查目标是否有效
        /// </summary>
        /// <param name="target">要检查的目标</param>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>目标是否有效</returns>
        private bool IsValidTarget(CardInstance target, GameContext context)
        {
            if (target == null) return false;

            // 基础有效性检查
            if (target.CurrentHealth <= 0) return false;

            // 根据Config.Filters进行更详细的过滤
            // 这里可以添加更多的过滤逻辑

            return true;
        }

        /// <summary>
        /// 从变量获取目标
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>从变量获取的目标列表</returns>
        private List<CardInstance> GetTargetsFromVariable(GameContext context)
        {
            // 从游戏上下文的变量中获取目标
            // 暂时返回空列表
            return [];
        }

        /// <summary>
        /// 从触发事件数据获取目标
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>从触发事件获取的目标列表</returns>
        private List<CardInstance> GetTargetsFromTriggeringEvent(GameContext context)
        {
            // 从触发事件中获取相关目标
            if (context.TriggeringEvent != null && context.SourceCard != null)
            {
                return [context.SourceCard];
            }
            return [];
        }

        /// <summary>
        /// 获取复合目标
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>复合目标列表</returns>
        private async Task<List<CardInstance>> GetCompositeTargets(GameContext context)
        {
            // 复合目标选择器，可以组合多种选择器
            // 暂时返回空列表
            await Task.CompletedTask;
            return [];
        }

        /// <summary>
        /// 记录解析结果
        /// </summary>
        /// <param name="result">解析结果</param>
        private void RecordResolutionResult(TargetResolutionResult result)
        {
            ResolutionHistory.Add(result);

            // 保留最近50次解析历史
            if (ResolutionHistory.Count > 50)
            {
                ResolutionHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// 标记需要重新解析
        /// </summary>
        public void MarkForResolution()
        {
            NeedsResolution = true;
        }

        /// <summary>
        /// 重置目标状态
        /// </summary>
        public void Reset()
        {
            ResolvedTargets.Clear();
            ResolutionCount = 0;
            LastResolutionTime = null;
            NeedsResolution = true;
            ResolutionHistory.Clear();
        }

        /// <summary>
        /// 检查目标是否有效
        /// </summary>
        /// <returns>目标是否有效</returns>
        public bool AreTargetsValid()
        {
            return ResolvedTargets.All(target =>
                target != null &&
                target.CurrentHealth > 0 &&
                !IsTargetDestroyed(target));
        }

        /// <summary>
        /// 检查目标是否已被摧毁
        /// </summary>
        /// <param name="target">要检查的目标</param>
        /// <returns>目标是否已被摧毁</returns>
        private bool IsTargetDestroyed(CardInstance target)
        {
            // 检查卡牌是否已被摧毁的逻辑
            // 可以检查生命值、是否在场上等
            return target.CurrentHealth <= 0 || target.CurrentZone == CardZone.Graveyard;
        }

        /// <summary>
        /// 获取玩家选择的可用目标池
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>可用目标列表</returns>
        private async Task<List<CardInstance>> GetAvailableTargetsForPlayerChoice(GameContext context)
        {
            // 如果有查询配置，使用查询系统获取目标池
            if (_queryExecutor != null && !string.IsNullOrEmpty(Config.Query.SourcePoolExpression))
            {
                try
                {
                    var queryResult = await _queryExecutor.ExecuteQueryAsync(Config.Query, context);
                    if (queryResult.Success)
                    {
                        var targets = new List<CardInstance>();
                        foreach (var result in queryResult.Results)
                        {
                            if (result is CardInstance cardInstance)
                            {
                                targets.Add(cardInstance);
                            }
                        }
                        return targets;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error executing query for player choice targets");
                }
            }

            // 回退到默认的目标池获取逻辑
            return GetDefaultTargetPool(context);
        }

        /// <summary>
        /// 获取默认的目标池（包含区域过滤优化）
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>默认目标列表</returns>
        private List<CardInstance> GetDefaultTargetPool(GameContext context)
        {
            var targets = new List<CardInstance>();

            try
            {
                // 优先添加活跃区域的目标（场上单位）
                if (context.RequestingPlayer?.Field != null)
                {
                    targets.AddRange(context.RequestingPlayer.Field.Where(card =>
                        card.CurrentZone == CardZone.Field));
                }

                if (context.OpponentPlayer?.Field != null)
                {
                    targets.AddRange(context.OpponentPlayer.Field.Where(card =>
                        card.CurrentZone == CardZone.Field));
                }

                // 如果没有场上目标，考虑添加领袖作为目标
                if (targets.Count == 0)
                {
                    if (context.RequestingPlayer?.Leader != null &&
                        context.RequestingPlayer.Leader.CurrentZone == CardZone.Leader)
                    {
                        targets.Add(context.RequestingPlayer.Leader);
                    }

                    if (context.OpponentPlayer?.Leader != null &&
                        context.OpponentPlayer.Leader.CurrentZone == CardZone.Leader)
                    {
                        targets.Add(context.OpponentPlayer.Leader);
                    }
                }

                _logger?.LogTrace("GetDefaultTargetPool: Found {Count} default targets", targets.Count);
                return targets;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting default target pool");
                return new List<CardInstance>();
            }
        }

        /// <summary>
        /// 请求玩家选择目标（基于PlayerChoiceTargetSelector.RequestPlayerChoice实现）
        /// </summary>
        /// <param name="availableTargets">可选目标列表</param>
        /// <param name="options">玩家选择配置</param>
        /// <returns>玩家选择的目标列表</returns>
        private async Task<List<CardInstance>> RequestPlayerChoiceAsync(List<CardInstance> availableTargets, PlayerChoiceConfig options)
        {
            // TODO: 实现与UI系统的交互
            // 这需要：
            // 1. 通过SignalR向客户端发送目标选择请求，包含Count限制信息
            // 2. 等待客户端响应
            // 3. 验证选择的有效性（数量和目标合法性）
            // 4. 返回选择结果

            // 临时实现：根据Count限制返回目标（与PlayerChoiceTargetSelector保持一致）
            await Task.Delay(100); // 模拟异步操作

            if (availableTargets.Any())
            {
                var maxCount = options.MaxCount ?? 1;
                var minCount = options.MinCount;

                // 临时逻辑：返回最小数量的目标
                var targetCount = Math.Min(Math.Max(minCount, 1), Math.Min(maxCount, availableTargets.Count));

                _logger?.LogDebug("RequestPlayerChoice: Selecting {TargetCount} targets from {AvailableCount} available (min: {MinCount}, max: {MaxCount})",
                    targetCount, availableTargets.Count, minCount, maxCount);

                return availableTargets.Take(targetCount).ToList();
            }

            _logger?.LogDebug("RequestPlayerChoice: No available targets to choose from");
            return [];
        }
    }



    /// <summary>
    /// 目标解析结果
    /// </summary>
    public class TargetResolutionResult
    {
        /// <summary>
        /// 是否成功解析
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 解析的目标
        /// </summary>
        public List<CardInstance> Targets { get; set; } = [];

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 解析时间
        /// </summary>
        public DateTime ResolutionTime { get; set; }

        /// <summary>
        /// 解析持续时间
        /// </summary>
        public TimeSpan Duration { get; set; }
    }
}
