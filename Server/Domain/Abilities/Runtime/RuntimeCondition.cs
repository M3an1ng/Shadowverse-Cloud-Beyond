using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ShadowverseBeyond.Server.Domain.Abilities.Runtime
{
    /// <summary>
    /// 运行时条件包装类
    /// 包装ConditionConfig并添加评估上下文
    /// </summary>
    public class Condition
    {
        /// <summary>
        /// 静态条件配置
        /// </summary>
        public ConditionConfig Config { get; }

        /// <summary>
        /// DSL条件评估器
        /// </summary>
        private readonly IDslConditionEvaluator _dslConditionEvaluator;

        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<Condition>? _logger;

        /// <summary>
        /// 评估次数
        /// </summary>
        public int EvaluationCount { get; private set; }

        /// <summary>
        /// 上次评估时间
        /// </summary>
        public DateTime? LastEvaluationTime { get; private set; }

        /// <summary>
        /// 上次评估结果
        /// </summary>
        public bool? LastEvaluationResult { get; private set; }

        /// <summary>
        /// 是否启用缓存
        /// </summary>
        public bool CacheEnabled { get; set; } = false;

        /// <summary>
        /// 缓存有效期
        /// </summary>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 评估历史
        /// </summary>
        public List<ConditionEvaluationResult> EvaluationHistory { get; } = new();

        public Condition(ConditionConfig config, IDslConditionEvaluator dslConditionEvaluator, ILogger<Condition>? logger = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _dslConditionEvaluator = dslConditionEvaluator ?? throw new ArgumentNullException(nameof(dslConditionEvaluator));
            _logger = logger;
        }

        /// <summary>
        /// 评估条件
        /// </summary>
        /// <param name="context">评估上下文</param>
        /// <returns>评估结果</returns>
        public async Task<bool> EvaluateAsync(ConditionEvaluationContext context)
        {
            if (!IsEnabled)
                return false;

            // 检查缓存
            if (CacheEnabled && LastEvaluationResult.HasValue && LastEvaluationTime.HasValue)
            {
                var cacheAge = DateTime.UtcNow - LastEvaluationTime.Value;
                if (cacheAge < CacheDuration)
                {
                    return LastEvaluationResult.Value;
                }
            }

            var startTime = DateTime.UtcNow;
            bool result;

            try
            {
                // TODO: 实现实际的条件评估逻辑
                // 这里应该根据Config.Expression或其他配置来评估条件
                result = await EvaluateConditionLogic(context);
            }
            catch (Exception ex)
            {
                result = false;
                RecordEvaluationResult(new ConditionEvaluationResult
                {
                    Success = false,
                    Result = false,
                    ErrorMessage = ex.Message,
                    EvaluationTime = startTime,
                    Duration = DateTime.UtcNow - startTime
                });
                throw;
            }

            // 更新状态
            EvaluationCount++;
            LastEvaluationTime = DateTime.UtcNow;
            LastEvaluationResult = result;

            // 记录评估结果
            RecordEvaluationResult(new ConditionEvaluationResult
            {
                Success = true,
                Result = result,
                EvaluationTime = startTime,
                Duration = DateTime.UtcNow - startTime
            });

            return result;
        }

        /// <summary>
        /// 实际的条件评估逻辑
        /// </summary>
        /// <param name="context">评估上下文</param>
        /// <returns>评估结果</returns>
        private async Task<bool> EvaluateConditionLogic(ConditionEvaluationContext context)
        {
            try
            {
                // 使用DSL条件评估器评估表达式
                var result = await _dslConditionEvaluator.EvaluateDslExpressionAsync(Config.Expression, context);

                _logger?.LogDebug("Condition {ConditionId} evaluated to {Result}", Config.Id, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error evaluating condition {ConditionId} with expression: {Expression}",
                    Config.Id, Config.Expression);
                return false;
            }
        }

        /// <summary>
        /// 记录评估结果
        /// </summary>
        /// <param name="result">评估结果</param>
        private void RecordEvaluationResult(ConditionEvaluationResult result)
        {
            EvaluationHistory.Add(result);

            // 保留最近50次评估历史
            if (EvaluationHistory.Count > 50)
            {
                EvaluationHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// 重置评估状态
        /// </summary>
        public void Reset()
        {
            EvaluationCount = 0;
            LastEvaluationTime = null;
            LastEvaluationResult = null;
            EvaluationHistory.Clear();
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            LastEvaluationResult = null;
            LastEvaluationTime = null;
        }




    }

    /// <summary>
    /// 条件评估上下文
    /// </summary>
    public class ConditionEvaluationContext
    {
        /// <summary>
        /// 条件ID
        /// </summary>
        public string ConditionId { get; set; } = string.Empty;

        /// <summary>
        /// 请求玩家ID
        /// </summary>
        public int RequestingPlayerId { get; set; }

        /// <summary>
        /// 源卡牌
        /// </summary>
        public CardInstance? SourceCard { get; set; }

        /// <summary>
        /// 触发事件
        /// </summary>
        public IGameEvent? TriggeringEvent { get; set; }

        /// <summary>
        /// 游戏状态
        /// </summary>
        public GameState? GameState { get; set; }

        /// <summary>
        /// 动态变量
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new();

        /// <summary>
        /// 评估时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 条件评估结果
    /// </summary>
    public class ConditionEvaluationResult
    {
        /// <summary>
        /// 是否成功评估
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 评估结果
        /// </summary>
        public bool Result { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 评估时间
        /// </summary>
        public DateTime EvaluationTime { get; set; }

        /// <summary>
        /// 评估持续时间
        /// </summary>
        public TimeSpan Duration { get; set; }
    }
}
