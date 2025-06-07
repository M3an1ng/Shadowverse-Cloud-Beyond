using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // Added for IConditionEvaluator and ConditionContext
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    /// <summary>
    /// 效果处理器实现
    /// 根据M12设计文档实现，负责解析效果配置并调用GameState原子操作
    /// </summary>
    public class EffectHandler : IEffectHandler
    {
        private readonly IGameStateActionDispatcher _gameStateDispatcher;
        private readonly IValueSourceResolver _valueSourceResolver;
        private readonly ILogger<EffectHandler> _logger;
        // private readonly IConditionEvaluator _conditionEvaluator; // Removed dependency temporarily

        public EffectHandler(
            IGameStateActionDispatcher gameStateDispatcher,
            IValueSourceResolver valueSourceResolver,
            // IConditionEvaluator conditionEvaluator, // Removed parameter temporarily
            ILogger<EffectHandler> logger)
        {
            _gameStateDispatcher = gameStateDispatcher;
            _valueSourceResolver = valueSourceResolver;
            // _conditionEvaluator = conditionEvaluator; // Removed assignment temporarily
            _logger = logger;
        }

        /// <summary>
        /// 执行效果链
        /// </summary>
        // TODO: Review if ExecuteEffectChainAsync belongs here or in EventProcessor/AbilityManager
        // This method signature needs update if kept. For now, commenting out as IEffectHandler only defines ExecuteAsync.
        /*
        public async Task<EffectExecutionResult> ExecuteEffectChainAsync(AbilityContext context) // Changed parameter type
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new EffectExecutionResult();

            try
            {
                // 处理重复执行
                for (int repeatIndex = 0; repeatIndex < executionParams.RepeatCount; repeatIndex++)
                {
                    // 获取效果配置列表（这里需要从某个地方获取，可能是从SourceCard的能力配置中）
                    var effectConfigs = GetEffectConfigs(executionParams);

                    // 按执行顺序排序
                    var sortedEffects = effectConfigs.OrderBy(e => e.ExecutionOrder).ToList();

                    foreach (var effectConfig in sortedEffects)
                    {
                        if (!effectConfig.IsEnabled)
                            continue;

                        // 检查执行条件
                        if (!await CheckExecutionConditions(effectConfig, executionParams))
                            continue;

                        var singleResult = await ExecuteSingleEffectAsync(effectConfig, executionParams);

                        // 合并结果
                        result.ExecutedEffectsCount += singleResult.ExecutedEffectsCount;
                        result.FailedEffectsCount += singleResult.FailedEffectsCount;
                        result.ErrorMessages.AddRange(singleResult.ErrorMessages);
                        result.GeneratedEvents.AddRange(singleResult.GeneratedEvents);

                        // 更新变量
                        // Iterate directly over the dictionary (UpdatedVariables is now Dictionary<string, object>)
                        foreach (var variable in singleResult.UpdatedVariables)
                        {
                            // Use dictionary indexer for assignment (Variables is Dictionary<string, object>)
                            executionParams.Variables[variable.Key] = variable.Value;
                            result.UpdatedVariables[variable.Key] = variable.Value;
                        }

                        // 处理失败策略
                        if (!singleResult.Success)
                        {
                            result.Success = false;

                            if (effectConfig.OnFailure == FailureStrategy.Stop)
                            {
                                _logger.LogWarning("Effect execution stopped due to failure: {ErrorMessage}",
                                    string.Join(", ", singleResult.ErrorMessages));
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessages.Add($"Effect chain execution failed: {ex.Message}");
                _logger.LogError(ex, "Error executing effect chain");
            }
            finally
            {
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }
        */ // Added closing comment for ExecuteEffectChainAsync

        /// <summary>
        /// 执行单个效果
        /// </summary>
        // This method might be redundant if IEffectHandler only has ExecuteAsync.
        // Let's assume ExecuteAsync is the main entry point now.
        // public async Task<EffectExecutionResult> ExecuteSingleEffectAsync(EffectConfig effectConfig, AbilityContext context) // Changed parameter type
        // {
        //     var stopwatch = Stopwatch.StartNew();
        // ... rest of method ...
        // }

        // Implementing the required method from IEffectHandler
        /// <summary>
        /// 解析参数 (Moved before ExecuteAsync for debugging CS1061/CS0103)
        /// </summary>
        public async Task<EffectExecutionResult> ExecuteAsync(EffectConfig effectConfig, AbilityContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new EffectExecutionResult();

            try
            {
                // 1. 解析效果类型
                string effectType = effectConfig.Type; // Directly use effectConfig.Type

                // 2. 参数已在 AbilityContext 中由 ValueSourceResolver 解析好，无需再次解析

                // 3. 调用GameState原子操作 (使用新的 DispatchAsync 签名)
                // AbilityContext 包含了 CurrentGameState, SelectedTargets, 和 Parameters
                var actionResult = await _gameStateDispatcher.DispatchAsync(effectType, context);

                // 4. 处理结果
                result.Success = actionResult.Success;
                result.ExecutedEffectsCount = actionResult.Success ? 1 : 0;
                result.FailedEffectsCount = actionResult.Success ? 0 : 1;
                // Assuming GameStateActionResult still has GeneratedEvents and ErrorMessage
                if (actionResult.GeneratedEvents != null) // Check if GeneratedEvents is null
                {
                    result.GeneratedEvents.AddRange(actionResult.GeneratedEvents);
                }

                if (!actionResult.Success && !string.IsNullOrEmpty(actionResult.ErrorMessage)) // Check if ErrorMessage is null or empty
                {
                    result.ErrorMessages.Add(actionResult.ErrorMessage);
                }
                // TODO: Consider how to handle IndividualActionResults if needed here.
                // Maybe log them or aggregate messages?
                if (actionResult.Results != null)
                {
                    foreach (var individualResult in actionResult.Results)
                    {
                        if (!individualResult.Success && !string.IsNullOrEmpty(individualResult.Message))
                        {
                            _logger.LogDebug("Individual action failed for target {TargetId}: {Message}", individualResult.TargetId.Id ?? "Unknown", individualResult.Message);
                            // Optionally add individual messages to the main result's error list
                            // result.ErrorMessages.Add($"Target {individualResult.TargetId?.Id ?? "Unknown"}: {individualResult.Message}");
                        }
                    }
                }


                // 5. 处理子效果
                if (effectConfig.SubEffects != null && effectConfig.SubEffects.Any()) // Check if SubEffects is null
                {
                    // Create context for sub-effect (Use context)
                    // Pass the result of the parent action, sub-effects might need it.
                    var subContext = CreateSubEffectContext(context, actionResult);
                    foreach (var subEffect in effectConfig.SubEffects.OrderBy(e => e.ExecutionOrder))
                    {
                        // Recursively call ExecuteAsync for sub-effect
                        var subResult = await ExecuteAsync(subEffect, subContext);

                        // 合并子效果结果
                        result.ExecutedEffectsCount += subResult.ExecutedEffectsCount;
                        result.FailedEffectsCount += subResult.FailedEffectsCount;
                        result.ErrorMessages.AddRange(subResult.ErrorMessages);
                        result.GeneratedEvents.AddRange(subResult.GeneratedEvents);

                        // Propagate failure upwards if strategy is Stop
                        if (!subResult.Success)
                        {
                            result.Success = false; // Mark parent as failed if any sub-effect fails
                            if (subEffect.OnFailure == FailureStrategy.Stop)
                            {
                                _logger.LogWarning("Sub-effect execution stopped due to failure strategy. Effect: {EffectType}", subEffect.Type);
                                break; // Stop processing further sub-effects
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.FailedEffectsCount = 1;
                result.ErrorMessages.Add($"Effect execution failed: {ex.Message}");
                _logger.LogError(ex, "Error executing single effect: {EffectType}", effectConfig.Type);
            }
            finally
            {
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// 获取效果配置列表
        /// </summary>
        // This method needs AbilityConfig, which isn't directly in AbilityContext.
        // It should likely be called from EventProcessor which has the AbilityConfig.
        // Commenting out for now.
        /*
        private List<EffectConfig> GetEffectConfigs(AbilityContext context) // Changed parameter type
        {
            // Logic needs rework - AbilityContext doesn't have AdditionalParams or ExecutingAbilityConfig directly.
            // This info should come from the caller (EventProcessor).
            if (executionParams.AdditionalParams.TryGetValue("ExecutingAbilityConfig", out var abilityConfigObj) &&
                abilityConfigObj is AbilityConfig executingAbility)
            {
                // 假设 AbilityConfig 有一个名为 Effects 的列表属性
                // 注意：需要确认 AbilityConfig 的实际结构
                // return executingAbility.Effects ?? new List<EffectConfig>();

                // 临时占位符，直到 AbilityConfig 结构确认
                _logger.LogWarning("GetEffectConfigs: Placeholder implementation pending AbilityConfig structure confirmation.");
                // 暂时返回 AbilityConfig 中直接定义的 Effects 列表（如果存在）
                if (executingAbility.Effects != null) // 假设存在 Effects 属性
                {
                    return executingAbility.Effects;
                }
                else
                {
                    _logger.LogWarning("GetEffectConfigs: ExecutingAbilityConfig found but its 'Effects' property is null or missing.");
                    return new List<EffectConfig>();
                }
            }
            else
            {
                _logger.LogWarning("GetEffectConfigs: Could not find 'ExecutingAbilityConfig' in AdditionalParams or it was not of type AbilityConfig. Ability Source: {SourceCardId}", executionParams.SourceCard?.InstanceId);
                // 尝试从 SourceCard 直接获取（如果设计如此）
                // if (executionParams.SourceCard is BattleCard battleCard && battleCard.Definition?.Abilities?.FirstOrDefault()?.Effects != null)
                // {
                //     _logger.LogWarning("GetEffectConfigs: Falling back to first ability effects from SourceCard definition.");
                //     return battleCard.Definition.Abilities.First().Effects;
                // }

                return new List<EffectConfig>(); // 默认返回空列表
            }
        }

        /// <summary>
        /// 检查执行条件
        /// </summary>
        // This should use IConditionEvaluator which takes AbilityContext.
        private async Task<bool> CheckExecutionConditions(EffectConfig effectConfig, AbilityContext context) // Changed parameter type
        {
            // 如果效果本身被禁用，或没有定义执行条件，则认为条件满足
            if (!effectConfig.IsEnabled || effectConfig.ExecutionConditions == null || !effectConfig.ExecutionConditions.Any())
                return true;

            // TODO: Implement actual condition evaluation logic using IConditionEvaluator.
            // The IConditionEvaluator interface needs to be defined and implemented first.
            // Also, resolve the discrepancy between ConditionContext in M10 design and actual code.

            // Create ConditionContext based on the actual definition in Conditions/ConditionConfig.cs
            var conditionContext = new ConditionContext
            {
                GameState = context.CurrentGameState, // Use context
                TriggerEvent = context.TriggeringEvent, // Use context
                Source = context.SourceCard, // Use context
                // 'Target' in actual ConditionContext is object?, not List<TargetId>. Needs clarification.
                // Passing the first target ID for now as a placeholder. Handle struct nullability correctly.
                Target = context.SelectedTargets.FirstOrDefault() != null ? (object)context.SelectedTargets.First().Id : null, // Use context
                Variables = context.Variables, // Use context (Variables is already Dictionary)
                // Parameters might be needed for specific condition types, TBD.
                Parameters = new Dictionary<string, object>() // Initialize Parameters dictionary
            };

            _logger.LogWarning("CheckExecutionConditions: Condition evaluation is temporarily bypassed for EffectType {EffectType}. Returning true. Context: {ContextJson}",
                               effectConfig.Type, System.Text.Json.JsonSerializer.Serialize(conditionContext)); // Log the created context for debugging

            // Temporarily bypass condition evaluation until IConditionEvaluator is implemented
            // and the ConditionContext structure is finalized.
            // Remove the loop that iterates through effectConfig.ExecutionConditions and calls _conditionEvaluator.
            await Task.CompletedTask; // Placeholder for async operation if needed later

            return true; // Temporarily return true
        }

        /// <summary>
        /// 解析效果类型
        /// </summary>
        private string ParseEffectType(EffectConfig effectConfig)
        {
            return effectConfig.Type;
        }

        /// <summary>
        /// 解析参数
        /// </summary>
        // Re-applying the method definition to rule out hidden issues
        // Method ResolveActionParametersAsync moved before ExecuteAsync for debugging CS1061/CS0103

        /// <summary>
        /// 创建值解析上下文
        /// </summary>
        // This method is likely redundant now as ResolveParametersAsync uses AbilityContext directly.
        /*
        private ValueResolutionContext CreateValueResolutionContext(AbilityContext context) // Changed parameter type
        {
             return new ValueResolutionContext
            {
                CurrentGameState = executionParams.CurrentGameState,
                SourceCard = executionParams.SourceCard,
                SourcePlayerId = executionParams.SourcePlayerId,
                Variables = executionParams.Variables,
                TriggeringEvent = executionParams.TriggeringEvent,
                AdditionalParams = executionParams.AdditionalParams
            };
        }
        */ // Added closing comment for CreateValueResolutionContext

        /// <summary>
        /// 创建子效果参数
        /// </summary>
        // This method should now create and return an AbilityContext for sub-effects.
        private AbilityContext CreateSubEffectContext(AbilityContext originalContext, GameStateActionResult actionResult) // Changed parameter and return type
        {
            // Create a new context for the sub-effect.
            // Pass essential information. Sub-effects might need their own target selection later.
            // The Variables dictionary is passed by reference, so changes might reflect, consider cloning if needed.
            return new AbilityContext(
                originalContext.CurrentGameState, // Pass the same game state snapshot
                originalContext.TriggeringEvent,  // Pass the original trigger event
                originalContext.SourceCard)       // Pass the original source card
            {
                // SelectedTargets might need re-evaluation for sub-effects, passing original for now.
                SelectedTargets = originalContext.SelectedTargets,
                // Pass the same Variables dictionary reference. Consider cloning if isolation is needed.
                // Variables = originalContext.Variables // Removed assignment to read-only property
            };
            // Removed assignments to read-only/non-existent properties.
        }
    }
}
