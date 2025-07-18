using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos.Enums;
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
        public List<TargetInfo> ResolvedTargets { get; private set; } = [];

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
        public async Task<List<TargetInfo>> ResolveTargetsAsync(GameContext context)
        {
            if (!IsEnabled)
                return [];

            var startTime = DateTime.UtcNow;
            List<TargetInfo> targets;

            try
            {
                targets = await ResolveTargetLogic(context);
            }
            catch (Exception ex)
            {
                RecordResolutionResult(new TargetResolutionResult
                {
                    Success = false,
                    Targets = [], // Empty List<TargetInfo>
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
        private async Task<List<TargetInfo>> ResolveTargetLogic(GameContext context)
        {
            var targets = new List<TargetInfo>();

            switch (Config.SelectorType)
            {
                case Self:
                    if (context.SourceCard != null)
                    {
                        targets.Add(TargetInfo.ForCard(context.SourceCard));
                    }
                    else if (context.RequestingPlayer != null)
                    {
                        targets.Add(TargetInfo.ForPlayer(context.RequestingPlayer));
                    }
                    break;

                case Filtered:
                    targets = await GetFilteredTargets(context);
                    break;

                case PlayerChoice:
                    targets = await GetPlayerChoiceTargets(context);
                    break;

                case TriggeringEventData:
                    targets = GetTargetsFromTriggeringEvent(context);
                    break;

                case Composite:
                    targets = await GetCompositeTargets(context);
                    break;

                default:
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
        private async Task<List<TargetInfo>> GetFilteredTargets(GameContext context)
        {
            if (_queryExecutor == null)
            {
                _logger?.LogWarning("QueryExecutor is null, cannot execute filtered target query");
                return [];
            }

            try
            {
                var queryResult = await _queryExecutor.ExecuteQueryAsync(Config.Query, context);

                if (!queryResult.Success)
                {
                    _logger?.LogWarning("Query execution failed for filtered targets: {ErrorMessage}", queryResult.ErrorMessage);
                    return [];
                }

                var targets = new List<TargetInfo>();
                foreach (var item in queryResult.Results)
                {
                    if (item is CardInstance cardInstance)
                    {
                        targets.Add(TargetInfo.ForCard(cardInstance));
                    }
                    else if (item is PlayerState playerState)
                    {
                        targets.Add(TargetInfo.ForPlayer(playerState));
                    }
                    else
                    {
                        _logger?.LogWarning("Unhandled item type in query result: {ItemType}", item?.GetType().FullName ?? "null");
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
        private async Task<List<TargetInfo>> GetPlayerChoiceTargets(GameContext context)
        {
            if (Config.PlayerChoiceOptions == null)
            {
                _logger?.LogWarning("PlayerChoice selector configured but PlayerChoiceOptions is null");
                return [];
            }

            try
            {
                var availableTargets = await GetAvailableTargetsForPlayerChoice(context);

                if (availableTargets.Count == 0)
                {
                    _logger?.LogDebug("No available targets for player choice");
                    return [];
                }

                if (Config.PlayerChoiceOptions.AutoSelect)
                {
                    var minCount = Config.PlayerChoiceOptions.MinCount;
                    if (availableTargets.Count <= minCount)
                    {
                        _logger?.LogDebug("Auto-selecting all {Count} targets (equals minimum required)", availableTargets.Count);
                        return availableTargets;
                    }
                }

                if (!Config.PlayerChoiceOptions.IsRequired)
                {
                    _logger?.LogDebug("Player choice not required, returning all {Count} available targets", availableTargets.Count);
                    return availableTargets;
                }

                var selectedTargets = await RequestPlayerChoiceAsync(availableTargets, Config.PlayerChoiceOptions, context);

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
        /// <param name="targetId">要检查的目标ID</param>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>目标是否有效</returns>
        private bool IsValidTarget(TargetInfo targetInfo, GameContext context)
        {
            if (targetInfo == null) return false;

            if (targetInfo.Type == TargetType.Card)
            {
                var card = targetInfo.Card;
                if (card == null) return false;
                if ((card.IsFollower || card.IsLeader) && card.CurrentHealth <= 0) return false;
            }
            else if (targetInfo.Type == TargetType.Player)
            {
                var player = targetInfo.Player;
                if (player == null || player.Leader == null || player.Leader.CurrentHealth <= 0) return false;
            }
            return true;
        }

        /// <summary>
        /// 从变量获取目标
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>从变量获取的目标列表</returns>
        private List<TargetInfo> GetTargetsFromVariable(GameContext context)
        {
            _logger?.LogWarning("GetTargetsFromVariable is not fully implemented to return List<TargetInfo>.");
            return [];
        }

        /// <summary>
        /// 从触发事件数据获取目标
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>从触发事件获取的目标列表</returns>
        private List<TargetInfo> GetTargetsFromTriggeringEvent(GameContext context)
        {
            var targets = new List<TargetInfo>();
            if (context.TriggeringEvent?.Source != null)
            {
                targets.Add(context.TriggeringEvent.Source);
            }
            return targets;
        }

        /// <summary>
        /// 获取复合目标
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>复合目标列表</returns>
        private async Task<List<TargetInfo>> GetCompositeTargets(GameContext context)
        {
            _logger?.LogWarning("GetCompositeTargets is not fully implemented to return List<TargetInfo>.");
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
        /// <param name="context">统一的游戏上下文，用于解析和验证目标实体</param>
        /// <returns>目标是否有效</returns>
        public bool AreTargetsValid(GameContext context)
        {
            if (NeedsResolution) return false;
            if (context == null)
            {
                _logger?.LogWarning("AreTargetsValid called with null GameContext. Cannot perform full validation.");
                return ResolvedTargets.Any();
            }
            return ResolvedTargets.All(targetInfo => IsValidTarget(targetInfo, context));
        }

        // IsTargetDestroyed method is removed as its logic is merged into IsValidTarget.

        /// <summary>
        /// 获取玩家选择的可用目标池
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>可用目标列表</returns>
        private async Task<List<TargetInfo>> GetAvailableTargetsForPlayerChoice(GameContext context)
        {
            if (_queryExecutor != null && Config.Query != null && !string.IsNullOrEmpty(Config.Query.SourcePoolExpression))
            {
                try
                {
                    var queryResult = await _queryExecutor.ExecuteQueryAsync(Config.Query, context);
                    if (queryResult.Success)
                    {
                        var targets = new List<TargetInfo>();
                        foreach (var item in queryResult.Results)
                        {
                            if (item is CardInstance cardInstance)
                            {
                                targets.Add(TargetInfo.ForCard(cardInstance));
                            }
                            else if (item is PlayerState playerState)
                            {
                                targets.Add(TargetInfo.ForPlayer(playerState));
                            }
                            else
                            {
                                _logger?.LogWarning("Unhandled item type in player choice query result: {ItemType}", item?.GetType().FullName ?? "null");
                            }
                        }
                        return targets;
                    }
                    _logger?.LogWarning("Query for player choice targets failed: {ErrorMessage}", queryResult.ErrorMessage);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error executing query for player choice targets");
                }
            }
            return GetDefaultTargetPool(context).Select(TargetInfo.ForCard).ToList();
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
        private async Task<List<TargetInfo>> RequestPlayerChoiceAsync(List<TargetInfo> availableTargets, PlayerChoiceConfig options, GameContext context)
        {
            await Task.Delay(10);

            if (!availableTargets.Any())
            {
                _logger?.LogDebug("RequestPlayerChoiceAsync: No available targets to choose from.");
                return [];
            }

            var maxCount = options.MaxCount ?? 1;
            var minCount = options.MinCount;

            minCount = Math.Max(0, Math.Min(minCount, availableTargets.Count));
            maxCount = Math.Max(minCount, Math.Min(maxCount, availableTargets.Count));

            var selectedTargets = new List<TargetInfo>();
            int targetsToSelect = minCount;

            foreach (var targetInfo in availableTargets)
            {
                if (selectedTargets.Count >= targetsToSelect) break;
                if (IsValidTarget(targetInfo, context))
                {
                    selectedTargets.Add(targetInfo);
                }
            }
            _logger?.LogDebug("RequestPlayerChoiceAsync (Placeholder): Selected {SelectedCount} targets from {AvailableCount} available (min: {MinCount}, max: {MaxCount})",
                               selectedTargets.Count, availableTargets.Count, minCount, maxCount);

            if (selectedTargets.Count < minCount && options.IsRequired)
            {
                _logger?.LogWarning("RequestPlayerChoiceAsync (Placeholder): Could not select the minimum required {MinCount} valid targets. Selected {SelectedCount}.",
                                   minCount, selectedTargets.Count);
                if (options.CanCancel) return [];
            }

            return selectedTargets;
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
        public List<TargetInfo> Targets { get; set; } = [];

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
