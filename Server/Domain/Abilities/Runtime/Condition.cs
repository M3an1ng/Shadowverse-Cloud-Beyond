using ShadowverseBeyond.Server.Dtos; // Added for GameStateDto
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AutoMapper; // Added for IMapper
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;

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
        /// 条件评估器 (使用通用接口)
        /// </summary>
        private readonly IConditionEvaluator _conditionEvaluator; // Changed type to general interface

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
        /// 是否需要重新评估（事件阶段状态）
        /// </summary>
        public bool NeedsEvaluation { get; private set; } = true;

        /// <summary>
        /// 上次评估绑定的触发事件ID
        /// </summary>
        public Guid? LastEvaluatedEventId { get; private set; }

        /// <summary>
        /// 事件阶段锁定的评估结果
        /// </summary>
        public bool? LastResolvedResult { get; private set; }

        /// <summary>
        /// 评估历史
        /// </summary>
        public List<ConditionEvaluationResult> EvaluationHistory { get; } = new();

        // Constructor now accepts the general IConditionEvaluator and IMapper
        public Condition(ConditionConfig config, IConditionEvaluator conditionEvaluator, IMapper mapper, ILogger<Condition>? logger = null) // Inject IMapper
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator)); // Store the general evaluator
            _logger = logger;
        }

        /// <summary>
        /// 评估条件
        /// </summary>
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>评估结果</returns>
        public bool Evaluate(GameContext context)
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
                // 使用IConditionEvaluator进行实际的条件评估
                result = EvaluateConditionLogic(context, null);
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
        /// 尝试读取事件阶段内已锁定的评估结果。
        /// </summary>
        public bool TryGetResolvedResult(Guid triggeringEventId, out bool result)
        {
            result = false;
            if (triggeringEventId == Guid.Empty)
            {
                return false;
            }

            if (LastEvaluatedEventId != triggeringEventId)
            {
                MarkForEvaluation();
                return false;
            }

            if (NeedsEvaluation || !LastResolvedResult.HasValue)
            {
                return false;
            }

            result = LastResolvedResult.Value;
            return true;
        }

        /// <summary>
        /// 写入事件阶段锁定的评估结果。
        /// </summary>
        public void SetResolvedResult(bool result, Guid triggeringEventId)
        {
            if (triggeringEventId == Guid.Empty)
            {
                return;
            }

            LastResolvedResult = result;
            LastEvaluatedEventId = triggeringEventId;
            NeedsEvaluation = false;
        }

        /// <summary>
        /// 将条件标记为需要重新评估。
        /// </summary>
        public void MarkForEvaluation()
        {
            NeedsEvaluation = true;
            LastEvaluatedEventId = null;
            LastResolvedResult = null;
        }

        /// <summary>
        /// 清理指定触发事件的锁定结果。
        /// </summary>
        public void ClearResolvedForEvent(Guid triggeringEventId)
        {
            if (triggeringEventId == Guid.Empty)
            {
                return;
            }

            if (LastEvaluatedEventId == triggeringEventId)
            {
                MarkForEvaluation();
            }
        }

        /// <summary>
        /// 评估条件（带追踪）
        /// </summary>
        public bool Evaluate(GameContext context, IDslTraceCollector traceCollector)
        {
            if (!IsEnabled)
                return false;

            var startTime = DateTime.UtcNow;
            bool result;

            try
            {
                result = EvaluateConditionLogic(context, traceCollector);
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

            EvaluationCount++;
            LastEvaluationTime = DateTime.UtcNow;
            LastEvaluationResult = result;

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
        /// <param name="context">统一的游戏上下文</param>
        /// <returns>评估结果</returns>
        private bool EvaluateConditionLogic(GameContext context, IDslTraceCollector? traceCollector)
        {
            try
            {
                // 直接使用GameContext，IConditionEvaluator已经更新为使用GameContext
                var result = traceCollector != null && _conditionEvaluator is DslConditionEvaluator dslEvaluator
                    ? dslEvaluator.EvaluateWithTrace(Config, context, traceCollector)
                    : _conditionEvaluator.Evaluate(Config, context);

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
            MarkForEvaluation();
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            LastEvaluationResult = null;
            LastEvaluationTime = null;
            MarkForEvaluation();
        }




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
