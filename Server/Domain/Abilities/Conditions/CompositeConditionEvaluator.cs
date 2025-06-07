using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.Conditions
{
    /// <summary>
    /// 复合条件评估器
    /// 支持多个条件的逻辑组合（AND、OR、NOT等）
    /// </summary>
    public class CompositeConditionEvaluator
    {
        private readonly IConditionEvaluator _conditionEvaluator;
        private readonly ILogger<CompositeConditionEvaluator> _logger;

        public CompositeConditionEvaluator(IConditionEvaluator conditionEvaluator, ILogger<CompositeConditionEvaluator> logger)
        {
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 评估条件组
        /// </summary>
        /// <param name="conditionGroup">条件组配置</param>
        /// <param name="context">能力上下文</param>
        /// <returns>评估结果</returns>
        public async Task<bool> EvaluateConditionGroupAsync(ConditionGroupConfig conditionGroup, AbilityContext context)
        {
            if (conditionGroup == null)
                throw new ArgumentNullException(nameof(conditionGroup));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                // 检查条件组是否启用
                if (!conditionGroup.IsEnabled)
                {
                    _logger.LogDebug("Condition group {GroupId} is disabled, returning false", conditionGroup.Id);
                    return false;
                }

                // 如果没有条件，默认返回true
                if (conditionGroup.Conditions == null || !conditionGroup.Conditions.Any())
                {
                    _logger.LogDebug("Condition group {GroupId} has no conditions, returning true", conditionGroup.Id);
                    return true;
                }

                // 评估所有条件
                var results = new List<bool>();
                foreach (var condition in conditionGroup.Conditions)
                {
                    var result = await _conditionEvaluator.EvaluateAsync(condition, context);
                    results.Add(result);

                    _logger.LogTrace("Condition {ConditionId} in group {GroupId} evaluated to {Result}",
                        condition.Id, conditionGroup.Id, result);
                }

                // 根据逻辑操作符组合结果
                var finalResult = CombineResults(results, conditionGroup.InternalLogic);

                _logger.LogDebug("Condition group {GroupId} with {ConditionCount} conditions evaluated to {Result} using {Logic} logic",
                    conditionGroup.Id, results.Count, finalResult, conditionGroup.InternalLogic);

                return finalResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating condition group {GroupId}", conditionGroup.Id);
                return false;
            }
        }

        /// <summary>
        /// 评估多个条件组
        /// </summary>
        /// <param name="conditionGroups">条件组列表</param>
        /// <param name="context">能力上下文</param>
        /// <returns>评估结果</returns>
        public async Task<bool> EvaluateConditionGroupsAsync(List<ConditionGroupConfig> conditionGroups, AbilityContext context)
        {
            if (conditionGroups == null || !conditionGroups.Any())
            {
                _logger.LogDebug("No condition groups provided, returning true");
                return true;
            }

            try
            {
                // 按优先级排序
                var sortedGroups = conditionGroups
                    .Where(g => g.IsEnabled)
                    .OrderBy(g => g.Priority)
                    .ToList();

                if (!sortedGroups.Any())
                {
                    _logger.LogDebug("No enabled condition groups, returning true");
                    return true;
                }

                // 评估第一个组
                var firstGroupResult = await EvaluateConditionGroupAsync(sortedGroups[0], context);

                if (sortedGroups.Count == 1)
                    return firstGroupResult;

                // 逐步组合其他组的结果
                var combinedResult = firstGroupResult;
                for (int i = 1; i < sortedGroups.Count; i++)
                {
                    var groupResult = await EvaluateConditionGroupAsync(sortedGroups[i], context);
                    combinedResult = CombineResults(new List<bool> { combinedResult, groupResult }, sortedGroups[i].ExternalLogic);

                    _logger.LogTrace("Combined result after group {GroupId}: {Result}", sortedGroups[i].Id, combinedResult);
                }

                _logger.LogDebug("Final result for {GroupCount} condition groups: {Result}", sortedGroups.Count, combinedResult);
                return combinedResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating condition groups");
                return false;
            }
        }

        /// <summary>
        /// 评估条件列表
        /// </summary>
        /// <param name="conditions">条件列表</param>
        /// <param name="context">能力上下文</param>
        /// <param name="logic">逻辑操作符</param>
        /// <returns>评估结果</returns>
        public async Task<bool> EvaluateConditionsAsync(List<ConditionConfig> conditions, AbilityContext context, LogicOperator logic = LogicOperator.And)
        {
            if (conditions == null || !conditions.Any())
            {
                _logger.LogDebug("No conditions provided, returning true");
                return true;
            }

            try
            {
                var results = new List<bool>();
                foreach (var condition in conditions.Where(c => c.IsEnabled))
                {
                    var result = await _conditionEvaluator.EvaluateAsync(condition, context);
                    results.Add(result);

                    _logger.LogTrace("Condition {ConditionId} evaluated to {Result}", condition.Id, result);
                }

                if (!results.Any())
                {
                    _logger.LogDebug("No enabled conditions, returning true");
                    return true;
                }

                var finalResult = CombineResults(results, logic);
                _logger.LogDebug("Evaluated {ConditionCount} conditions with {Logic} logic: {Result}",
                    results.Count, logic, finalResult);

                return finalResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating conditions list");
                return false;
            }
        }

        /// <summary>
        /// 根据逻辑操作符组合结果
        /// </summary>
        /// <param name="results">结果列表</param>
        /// <param name="logic">逻辑操作符</param>
        /// <returns>组合后的结果</returns>
        private bool CombineResults(List<bool> results, LogicOperator logic)
        {
            if (!results.Any())
                return true;

            return logic switch
            {
                LogicOperator.And => results.All(r => r),
                LogicOperator.Or => results.Any(r => r),
                LogicOperator.Not => !results.First(), // NOT只对第一个结果取反
                LogicOperator.Xor => results.Aggregate((a, b) => a ^ b), // 异或操作
                _ => throw new ArgumentException($"Unsupported logic operator: {logic}")
            };
        }
    }
}
