using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common; // For DurationType
using ShadowverseBeyond.Server.Domain.Abilities; // For Ability class
using ShadowverseBeyond.Server.Domain.Events; // For IEventProcessor
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // For ConditionConfig
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetConfig
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For EffectConfig
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    /// <summary>
    /// Manages Buff instances within a game session.
    /// Stores buffs associated with card instances and provides methods for registration, removal, and querying.
    /// Note: This is a basic in-memory implementation. For persistence across sessions or complex scenarios,
    /// a more robust storage mechanism might be needed.
    /// </summary>
    public class BuffManager : IBuffManager
    {
        // Internal storage: Dictionary mapping CardInstanceId to a list of Buffs applied to it.
        // The List preserves the order of application.
        private readonly Dictionary<Guid, List<Buff>> _buffsByCardInstanceId = new();
        private readonly ILogger<BuffManager> _logger;
        private readonly IBuffFactory _buffFactory;
        private readonly BuffStackingEngine _stackingEngine;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly AbilityManager _abilityManager;

        // Callback to get CardInstance by ID (injected by GameService or other container)
        private Func<Guid, CardInstance?>? _getCardInstanceCallback;

        public BuffManager(ILogger<BuffManager> logger, IBuffFactory buffFactory, BuffStackingEngine stackingEngine, IServiceScopeFactory serviceScopeFactory, AbilityManager abilityManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _buffFactory = buffFactory ?? throw new ArgumentNullException(nameof(buffFactory));
            _stackingEngine = stackingEngine ?? throw new ArgumentNullException(nameof(stackingEngine));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _abilityManager = abilityManager ?? throw new ArgumentNullException(nameof(abilityManager));
        }

        /// <summary>
        /// 设置CardInstance获取回调函数
        /// </summary>
        /// <param name="getCardInstanceCallback">获取CardInstance的回调函数</param>
        public void SetCardInstanceCallback(Func<Guid, CardInstance?> getCardInstanceCallback)
        {
            _getCardInstanceCallback = getCardInstanceCallback;
        }

        /// <inheritdoc />
        public Task RegisterBuffAsync(Buff buff)
        {
            if (buff == null)
            {
                _logger.LogWarning("Attempted to register a null buff.");
                return Task.CompletedTask;
            }

            var cardId = buff.TargetCardInstanceId;
            if (!_buffsByCardInstanceId.TryGetValue(cardId, out var buffList))
            {
                buffList = new List<Buff>();
                _buffsByCardInstanceId[cardId] = buffList;
            }

            // Simplified additive stacking with non-stackable buff replacement
            var stackingResult = _stackingEngine.EvaluateStacking(buff, buffList);

            if (!stackingResult.AllowAdd)
            {
                _logger.LogInformation("Buff {BuffId} registration rejected: {Reason}", buff.Id, stackingResult.Reason);
                return Task.CompletedTask;
            }

            // Remove buffs that need to be replaced (for non-stackable buffs)
            foreach (var buffToRemove in stackingResult.BuffsToRemove)
            {
                buffList.Remove(buffToRemove);
                _logger.LogDebug("Removed non-stackable Buff {BuffId} from Card {CardInstanceId} to make room for new buff",
                    buffToRemove.Id, cardId);

                // Handle AbilityBuffEffect removal from event system
                HandleAbilityBuffRemoval(buffToRemove);
            }

            // Add the new buff
            buffList.Add(buff);
            _logger.LogDebug("Registered Buff {BuffId} for Card {CardInstanceId}. Source: {SourceAbilityId}. Replaced: {ReplacedCount}. System: Additive Stacking with Non-Stackable Replacement",
                buff.Id, cardId, buff.SourceAbility.AbilityId, stackingResult.BuffsToRemove.Count);

            // Handle AbilityBuffEffect registration to event system
            HandleAbilityBuffRegistration(buff);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<bool> UnregisterBuffAsync(Guid buffId)
        {
            if (buffId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to unregister a buff with null or empty ID.");
                return false;
            }

            foreach (var kvp in _buffsByCardInstanceId)
            {
                var buffList = kvp.Value;
                var buffToRemove = buffList.FirstOrDefault(b => b.Id == buffId);
                if (buffToRemove != null)
                {
                    // Handle AbilityBuffEffect removal before removing the buff
                    HandleAbilityBuffRemoval(buffToRemove);

                    buffList.Remove(buffToRemove);
                    _logger.LogDebug("Unregistered Buff {BuffId} from Card {CardInstanceId}.", buffId, kvp.Key);

                    // Generate BuffRemovedEvent and propagate to event system
                    var buffRemovedEvent = new BuffRemovedEvent(buffToRemove, "manual");
                    // Note: ProcessEventAsync requires GameContext, but we don't have it here
                    // This should be handled by the calling system that has access to GameContext
                    // await _eventProcessor.ProcessEventAsync(buffRemovedEvent, gameContext);
                    _logger.LogDebug("Propagated BuffRemovedEvent for Buff {BuffId} to event system", buffId);

                    // Clean up empty list entry
                    if (buffList.Count == 0)
                    {
                        _buffsByCardInstanceId.Remove(kvp.Key);
                    }
                    return true;
                }
            }

            _logger.LogWarning("Buff {BuffId} not found for unregistration.", buffId);
            return false; // Buff not found
        }

        /// <inheritdoc />
        public Task<List<Buff>> GetBuffsForCardAsync(Guid cardInstanceId)
        {
            if (cardInstanceId == Guid.Empty)
            {
                return Task.FromResult(new List<Buff>()); // Return empty list for invalid ID
            }

            if (_buffsByCardInstanceId.TryGetValue(cardInstanceId, out var buffList))
            {
                // Return a copy to prevent external modification of the internal list
                return Task.FromResult(buffList.ToList());
            }

            return Task.FromResult(new List<Buff>()); // No buffs found for this card
        }

        /// <inheritdoc />
        public Task RemoveBuffsFromSourceAsync(string sourceAbilityId)
        {
            List<Guid> buffsToRemove = new List<Guid>();
            List<Guid> keysToRemove = new List<Guid>();

            foreach (var kvp in _buffsByCardInstanceId)
            {
                var cardId = kvp.Key;
                var buffList = kvp.Value;

                // Find buffs from the specific source
                var buffsFromSource = buffList.Where(b => b.SourceAbility.AbilityId.ToString() == sourceAbilityId).ToList();

                if (buffsFromSource.Count > 0)
                {
                    _logger.LogDebug("Removing {Count} buffs from Card {CardInstanceId} originating from SourceAbilityId {SourceAbilityId}.",
                        buffsFromSource.Count, cardId, sourceAbilityId);

                    // Handle AbilityBuffEffect removal for each buff before removing
                    foreach (var buffToRemove in buffsFromSource)
                    {
                        HandleAbilityBuffRemoval(buffToRemove);
                    }

                    // Remove them from the list
                    buffList.RemoveAll(b => b.SourceAbility.AbilityId.ToString() == sourceAbilityId);

                    // Mark the cardId for potential removal if its list becomes empty
                    if (buffList.Count == 0)
                    {
                        keysToRemove.Add(cardId);
                    }
                }
            }

            // Clean up empty list entries
            foreach (var key in keysToRemove)
            {
                _buffsByCardInstanceId.Remove(key);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RemoveAllBuffsAsync(Guid cardInstanceId)
        {
            if (cardInstanceId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to remove all buffs for an empty cardInstanceId.");
                return Task.CompletedTask;
            }

            if (_buffsByCardInstanceId.Remove(cardInstanceId))
            {
                _logger.LogDebug("Removed all buffs for Card {CardInstanceId}.", cardInstanceId);
            }
            else
            {
                _logger.LogDebug("No buffs found to remove for Card {CardInstanceId}.", cardInstanceId);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<BuffApplicationResult> ApplyBuffAsync(
            CardInstance target,
            string buffDefinitionId,
            CardInstance? sourceCard,
            int? duration,
            Dictionary<string, object?> parameters)
        {
            if (target == null)
            {
                _logger.LogWarning("ApplyBuffAsync: Target card instance is null.");
                return new BuffApplicationResult { Success = false, ErrorMessage = "Target cannot be null." };
            }
            if (string.IsNullOrEmpty(buffDefinitionId))
            {
                _logger.LogWarning("ApplyBuffAsync: Buff definition ID is null or empty.");
                return new BuffApplicationResult { Success = false, ErrorMessage = "Buff definition ID cannot be null or empty." };
            }

            _logger.LogDebug("Attempting to apply Buff '{BuffId}' to target {TargetId} from source {SourceCardId}",
                             buffDefinitionId, target.InstanceId, sourceCard?.InstanceId.ToString() ?? "None"); // Ensure Guid? is converted to string? before ??

            try
            {
                // This method is a placeholder for compatibility with GameStateActionDispatcher
                // The actual Buff creation should be done through CreateAndApplyBuffAsync method
                // which has access to the full context (source Ability, current turn)

                _logger.LogWarning("ApplyBuffAsync called without full context. Use CreateAndApplyBuffAsync instead for proper Buff creation.");

                return new BuffApplicationResult
                {
                    Success = false,
                    ErrorMessage = "Insufficient context for Buff creation. Use CreateAndApplyBuffAsync method instead."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying Buff '{BuffId}' to target {TargetId}.", buffDefinitionId, target.InstanceId);
                return new BuffApplicationResult
                {
                    Success = false,
                    ErrorMessage = $"Internal error applying buff: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 创建并应用Buff到目标卡牌（完整上下文版本）
        /// </summary>
        /// <param name="target">目标卡牌</param>
        /// <param name="sourceCard">源卡牌</param>
        /// <param name="sourceAbility">源能力</param>
        /// <param name="buffType">Buff类型</param>
        /// <param name="parameters">Buff参数</param>
        /// <param name="durationType">持续时间类型</param>
        /// <param name="durationParameter">持续时间参数</param>
        /// <param name="currentTurn">当前回合</param>
        /// <returns>Buff应用结果</returns>
        public async Task<BuffApplicationResult> CreateAndApplyBuffAsync(
            CardInstance target,
            CardInstance? sourceCard,
            Ability sourceAbility,
            string buffType,
            Dictionary<string, object?> parameters,
            DurationType durationType,
            object? durationParameter,
            int currentTurn)
        {
            if (target == null)
            {
                _logger.LogWarning("CreateAndApplyBuffAsync: Target card instance is null.");
                return new BuffApplicationResult { Success = false, ErrorMessage = "Target cannot be null." };
            }
            if (sourceAbility == null)
            {
                _logger.LogWarning("CreateAndApplyBuffAsync: Source ability is null.");
                return new BuffApplicationResult { Success = false, ErrorMessage = "Source ability cannot be null." };
            }

            try
            {
                Buff? createdBuff = null;

                // Create appropriate Buff based on type
                switch (buffType.ToLowerInvariant())
                {
                    case "modifyattack":
                    case "modifystats":
                        createdBuff = await CreateStatModificationBuffAsync(target, sourceCard, sourceAbility, parameters, durationType, durationParameter, currentTurn);
                        break;

                    case "addkeyword":
                        createdBuff = await CreateKeywordBuffAsync(target, sourceCard, sourceAbility, parameters, durationType, durationParameter, currentTurn);
                        break;

                    default:
                        _logger.LogWarning("Unknown buff type: {BuffType}", buffType);
                        return new BuffApplicationResult { Success = false, ErrorMessage = $"Unknown buff type: {buffType}" };
                }

                if (createdBuff == null)
                {
                    return new BuffApplicationResult { Success = false, ErrorMessage = "Failed to create buff instance." };
                }

                // Register the created buff
                await RegisterBuffAsync(createdBuff);

                _logger.LogInformation("Successfully created and applied Buff {BuffId} of type {BuffType} to target {TargetId}",
                    createdBuff.Id, buffType, target.InstanceId);

                // Generate BuffAppliedEvent
                var buffAppliedEvent = new Events.BuffAppliedEvent(createdBuff);
                var generatedEvents = new List<IGameEvent> { buffAppliedEvent };

                return new BuffApplicationResult
                {
                    Success = true,
                    AppliedBuff = createdBuff,
                    GeneratedEvents = generatedEvents
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating and applying Buff of type {BuffType} to target {TargetId}.", buffType, target.InstanceId);
                return new BuffApplicationResult
                {
                    Success = false,
                    ErrorMessage = $"Internal error creating buff: {ex.Message}"
                };
            }
        }
        /// <summary>
        /// 清理过期的Buff实例
        /// </summary>
        /// <param name="currentTurn">当前回合数</param>
        /// <returns>被移除的Buff数量</returns>
        public Task<int> CleanupExpiredBuffsAsync(int currentTurn)
        {
            var removedCount = 0;
            var cardsToCleanup = new List<Guid>();

            foreach (var kvp in _buffsByCardInstanceId)
            {
                var cardId = kvp.Key;
                var buffList = kvp.Value;
                var buffsToRemove = new List<Buff>();

                foreach (var buff in buffList)
                {
                    if (IsBuffExpired(buff, currentTurn))
                    {
                        buffsToRemove.Add(buff);
                    }
                }

                // Remove expired buffs
                foreach (var expiredBuff in buffsToRemove)
                {
                    // Handle AbilityBuffEffect removal before removing the buff
                    HandleAbilityBuffRemoval(expiredBuff);

                    buffList.Remove(expiredBuff);
                    removedCount++;
                    _logger.LogDebug("Removed expired Buff {BuffId} from Card {CardInstanceId}",
                        expiredBuff.Id, cardId);

                    // Generate BuffRemovedEvent for expired buff
                    var buffRemovedEvent = new Events.BuffRemovedEvent(expiredBuff, "expired");
                    // Note: Event propagation is handled by the calling system (GameService/EventProcessor)
                    // The generated events are collected and returned for processing by the event system
                }

                // Mark cards with empty buff lists for cleanup
                if (buffList.Count == 0)
                {
                    cardsToCleanup.Add(cardId);
                }
            }

            // Clean up empty entries
            foreach (var cardId in cardsToCleanup)
            {
                _buffsByCardInstanceId.Remove(cardId);
            }

            if (removedCount > 0)
            {
                _logger.LogInformation("Cleaned up {RemovedCount} expired buffs", removedCount);
            }

            return Task.FromResult(removedCount);
        }

        /// <summary>
        /// 检查Buff是否已过期
        /// </summary>
        /// <param name="buff">要检查的Buff</param>
        /// <param name="currentTurn">当前回合数</param>
        /// <returns>如果Buff已过期则返回true</returns>
        private bool IsBuffExpired(Buff buff, int currentTurn)
        {
            if (!buff.IsActive)
                return true; // Inactive buffs are considered expired

            switch (buff.DurationType)
            {
                case DurationType.Permanent:
                    return false; // Permanent buffs never expire

                case DurationType.EndOfTurn:
                    // Expires at the end of the turn it was applied
                    return currentTurn > buff.AppliedTurn;

                case DurationType.StartOfNextTurn:
                    // Expires at the start of the next turn
                    return currentTurn > buff.AppliedTurn;

                case DurationType.LimitedTurns:
                    // Expires after a specific number of turns
                    if (buff.DurationParameter is int turnLimit)
                    {
                        return (currentTurn - buff.AppliedTurn) >= turnLimit;
                    }
                    _logger.LogWarning("LimitedTurns buff {BuffId} has invalid DurationParameter", buff.Id);
                    return true; // Invalid parameter, consider expired

                case DurationType.SpecificEvent:
                    // Check if the specific event has occurred
                    if (buff.DurationParameter is string eventName)
                    {
                        // For now, implement basic event-based expiration
                        // This can be expanded when more sophisticated event tracking is needed
                        return HasEventOccurred(eventName, buff, currentTurn);
                    }
                    _logger.LogWarning("SpecificEvent buff {BuffId} has invalid DurationParameter", buff.Id);
                    return false; // Invalid parameter, don't expire

                case DurationType.ConditionalRemoval:
                    // Check if removal condition is met
                    if (buff.RemovalCondition != null)
                    {
                        // For now, implement basic condition checking
                        // This can be expanded when full condition evaluator is available
                        return IsRemovalConditionMet(buff, currentTurn);
                    }
                    return false; // No removal condition, don't expire

                default:
                    _logger.LogWarning("Unknown DurationType {DurationType} for buff {BuffId}",
                        buff.DurationType, buff.Id);
                    return false; // Unknown type, don't expire
            }
        }

        /// <summary>
        /// 检查特定事件是否已发生（用于事件驱动的Buff过期）
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="buff">相关的Buff</param>
        /// <param name="currentTurn">当前回合数</param>
        /// <returns>如果事件已发生则返回true</returns>
        private static bool HasEventOccurred(string eventName, Buff buff, int currentTurn)
        {
            // Basic event-based expiration logic
            // This can be expanded to integrate with a more sophisticated event tracking system
            return eventName switch
            {
                "turn_start" => currentTurn > buff.AppliedTurn,
                "turn_end" => currentTurn > buff.AppliedTurn,
                "card_played" => false, // Would need event tracking system
                "card_destroyed" => false, // Would need event tracking system
                "damage_taken" => false, // Would need event tracking system
                "ability_triggered" => false, // Would need event tracking system
                _ => false // Unknown event, don't expire
            };
        }

        /// <summary>
        /// 检查移除条件是否满足（用于条件驱动的Buff过期）
        /// </summary>
        /// <param name="buff">要检查的Buff</param>
        /// <param name="currentTurn">当前回合数</param>
        /// <returns>如果移除条件满足则返回true</returns>
        private static bool IsRemovalConditionMet(Buff buff, int currentTurn)
        {
            if (buff.RemovalCondition == null || string.IsNullOrEmpty(buff.RemovalCondition.Expression))
                return false;

            // Basic condition checking based on expression content
            // This can be expanded when full condition evaluator is available
            var expression = buff.RemovalCondition.Expression.ToLowerInvariant();

            // Simple pattern matching for common removal conditions
            if (expression.Contains("turn") && expression.Contains(">="))
            {
                return CheckTurnBasedExpression(expression, buff, currentTurn);
            }

            if (expression.Contains("health") || expression.Contains("damage"))
            {
                // Would need game state access for health/damage conditions
                return false;
            }

            if (expression.Contains("card_count") || expression.Contains("hand.count"))
            {
                // Would need game state access for card count conditions
                return false;
            }

            // For now, don't expire on unknown conditions
            return false;
        }

        /// <summary>
        /// 检查基于回合的表达式条件
        /// </summary>
        /// <param name="expression">条件表达式</param>
        /// <param name="buff">要检查的Buff</param>
        /// <param name="currentTurn">当前回合数</param>
        /// <returns>如果条件满足则返回true</returns>
        private static bool CheckTurnBasedExpression(string expression, Buff buff, int currentTurn)
        {
            // Simple parsing for turn-based expressions
            // Examples: "turn >= 5", "turn_count >= 3", "duration >= 2"

            if (expression.Contains("turn >="))
            {
                var parts = expression.Split(">=");
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var targetTurn))
                {
                    return currentTurn >= targetTurn;
                }
            }

            if (expression.Contains("duration >=") || expression.Contains("turn_count >="))
            {
                var parts = expression.Split(">=");
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var maxDuration))
                {
                    return (currentTurn - buff.AppliedTurn) >= maxDuration;
                }
            }

            return false;
        }

        /// <summary>
        /// 在回合开始时调用，清理过期的Buff
        /// </summary>
        /// <param name="currentTurn">当前回合数</param>
        /// <returns>清理操作的任务</returns>
        public async Task OnTurnStartAsync(int currentTurn)
        {
            await CleanupExpiredBuffsAsync(currentTurn);
        }

        /// <summary>
        /// 在回合结束时调用，清理过期的Buff
        /// </summary>
        /// <param name="currentTurn">当前回合数</param>
        /// <returns>清理操作的任务</returns>
        public async Task OnTurnEndAsync(int currentTurn)
        {
            await CleanupExpiredBuffsAsync(currentTurn);
        }

        /// <summary>
        /// 创建属性修改Buff
        /// </summary>
        private Task<Buff?> CreateStatModificationBuffAsync(
            CardInstance target,
            CardInstance? sourceCard,
            Ability sourceAbility,
            Dictionary<string, object?> parameters,
            DurationType durationType,
            object? durationParameter,
            int currentTurn)
        {
            try
            {
                // Extract IsNonStackable parameter
                bool isNonStackable = false;
                if (parameters.TryGetValue("IsNonStackable", out var isNonStackableObj) && isNonStackableObj is bool nonStackable)
                {
                    isNonStackable = nonStackable;
                }

                // Extract stat modification parameters
                var attackMod = ExtractIntParameter(parameters, "ModifyAttackAmount", 0);
                var healthMod = ExtractIntParameter(parameters, "ModifyHealthAmount", 0);
                var costMod = ExtractIntParameter(parameters, "ModifyCostAmount", 0);

                var effects = new List<IBuffEffect>();

                // Create stat modifications
                if (attackMod != 0)
                {
                    var statBuff = _buffFactory.CreateStatBuff(
                        target, sourceCard, sourceAbility,
                        StatType.Attack, attackMod, ModifierLayer.Additive,
                        durationType, durationParameter, currentTurn, isNonStackable);
                    return Task.FromResult<Buff?>(statBuff);
                }

                if (healthMod != 0)
                {
                    var statBuff = _buffFactory.CreateStatBuff(
                        target, sourceCard, sourceAbility,
                        StatType.Defense, healthMod, ModifierLayer.Additive,
                        durationType, durationParameter, currentTurn, isNonStackable);
                    return Task.FromResult<Buff?>(statBuff);
                }

                if (costMod != 0)
                {
                    var statBuff = _buffFactory.CreateStatBuff(
                        target, sourceCard, sourceAbility,
                        StatType.Cost, costMod, ModifierLayer.Additive,
                        durationType, durationParameter, currentTurn, isNonStackable);
                    return Task.FromResult<Buff?>(statBuff);
                }

                _logger.LogWarning("No valid stat modifications found in parameters for stat buff creation");
                return Task.FromResult<Buff?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating stat modification buff");
                return Task.FromResult<Buff?>(null);
            }
        }

        /// <summary>
        /// 创建关键字Buff
        /// </summary>
        private Task<Buff?> CreateKeywordBuffAsync(
            CardInstance target,
            CardInstance? sourceCard,
            Ability sourceAbility,
            Dictionary<string, object?> parameters,
            DurationType durationType,
            object? durationParameter,
            int currentTurn)
        {
            try
            {
                // Extract IsNonStackable parameter
                bool isNonStackable = false;
                if (parameters.TryGetValue("IsNonStackable", out var isNonStackableObj) && isNonStackableObj is bool nonStackable)
                {
                    isNonStackable = nonStackable;
                }

                // Extract keyword parameter
                if (!parameters.TryGetValue("Keyword", out var keywordObj) || !(keywordObj is string keyword) || string.IsNullOrEmpty(keyword))
                {
                    _logger.LogWarning("No valid keyword found in parameters for keyword buff creation");
                    return Task.FromResult<Buff?>(null);
                }

                var keywords = new List<string> { keyword };
                var keywordBuff = _buffFactory.CreateKeywordBuff(
                    target, sourceCard, sourceAbility,
                    keywords, durationType, durationParameter, currentTurn, isNonStackable);

                return Task.FromResult<Buff?>(keywordBuff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating keyword buff");
                return Task.FromResult<Buff?>(null);
            }
        }

        /// <summary>
        /// 从参数字典中提取整数参数
        /// </summary>
        private static int ExtractIntParameter(Dictionary<string, object?> parameters, string key, int defaultValue)
        {
            if (parameters.TryGetValue(key, out var value))
            {
                if (value is int intValue)
                    return intValue;
                if (value is string strValue && int.TryParse(strValue, out var parsedValue))
                    return parsedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 处理AbilityBuffEffect的注册到事件系统
        /// </summary>
        /// <param name="buff">包含AbilityBuffEffect的Buff</param>
        private void HandleAbilityBuffRegistration(Buff buff)
        {
            try
            {
                var abilityEffects = buff.Effects.OfType<AbilityBuffEffect>().ToList();
                if (abilityEffects.Count == 0)
                    return;

                // 获取目标CardInstance
                var targetCardInstance = GetCardInstance(buff.TargetCardInstanceId);
                if (targetCardInstance == null)
                {
                    _logger.LogWarning("Cannot register temporary abilities for Buff {BuffId}: CardInstance {CardId} not found",
                        buff.Id, buff.TargetCardInstanceId);
                    return;
                }

                foreach (var abilityEffect in abilityEffects)
                {
                    var abilityConfigs = abilityEffect.GrantedAbilityConfigs;
                    foreach (var abilityConfig in abilityConfigs)
                    {
                        // 标记为临时能力并添加Buff来源信息
                        var temporaryAbilityConfig = CreateTemporaryAbilityConfig(abilityConfig, buff.Id);

                        // 使用AbilityManager创建并注册临时能力
                        var temporaryAbility = _abilityManager.CreateAbilityFromConfig(temporaryAbilityConfig, targetCardInstance);
                        if (temporaryAbility != null)
                        {
                            // 注册到AbilityManager（这会自动触发双向绑定更新）
                            _abilityManager.RegisterAbility(temporaryAbility);

                            _logger.LogDebug("Registered temporary ability {AbilityTag} from Buff {BuffId} for card {CardId}",
                                temporaryAbility.AbilityTag, buff.Id, targetCardInstance.InstanceId);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to create temporary ability from config {AbilityId} for Buff {BuffId}",
                                abilityConfig.Id, buff.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling AbilityBuffEffect registration for Buff {BuffId}", buff.Id);
            }
        }

        /// <summary>
        /// 获取CardInstance实例
        /// </summary>
        /// <param name="cardInstanceId">卡牌实例ID</param>
        /// <returns>CardInstance实例，如果未找到则返回null</returns>
        private CardInstance? GetCardInstance(Guid cardInstanceId)
        {
            return _getCardInstanceCallback?.Invoke(cardInstanceId);
        }

        /// <summary>
        /// 创建临时能力配置，添加Buff来源标识
        /// </summary>
        /// <param name="originalConfig">原始能力配置</param>
        /// <param name="sourceBuffId">来源Buff ID</param>
        /// <returns>标记为临时能力的配置</returns>
        private AbilityConfig CreateTemporaryAbilityConfig(AbilityConfig originalConfig, Guid sourceBuffId)
        {
            // 创建配置副本并添加临时能力标识
            var temporaryConfig = new AbilityConfig
            {
                Id = originalConfig.Id,
                CardId = originalConfig.CardId,
                Name = originalConfig.Name,
                Description = originalConfig.Description,
                AbilityTag = originalConfig.AbilityTag,
                Type = originalConfig.Type,
                Trigger = originalConfig.Trigger,
                PreConditions = new List<ConditionConfig>(originalConfig.PreConditions),
                Conditions = new List<ConditionConfig>(originalConfig.Conditions),
                Targets = new List<TargetConfig>(originalConfig.Targets),
                Effects = new List<EffectConfig>(originalConfig.Effects),
                IsActive = originalConfig.IsActive,
                Version = originalConfig.Version,
                DslSource = originalConfig.DslSource,
                Metadata = new Dictionary<string, object>(originalConfig.Metadata)
            };

            // 添加临时能力标识
            temporaryConfig.Metadata["IsTemporary"] = true;
            temporaryConfig.Metadata["SourceBuffId"] = sourceBuffId;
            temporaryConfig.Metadata["RegistrationTime"] = DateTime.UtcNow;

            return temporaryConfig;
        }

        /// <summary>
        /// 处理AbilityBuffEffect从事件系统的移除
        /// </summary>
        /// <param name="buff">包含AbilityBuffEffect的Buff</param>
        private void HandleAbilityBuffRemoval(Buff buff)
        {
            try
            {
                var abilityEffects = buff.Effects.OfType<AbilityBuffEffect>().ToList();
                if (abilityEffects.Count == 0)
                    return;

                // 获取目标CardInstance
                var targetCardInstance = GetCardInstance(buff.TargetCardInstanceId);
                if (targetCardInstance == null)
                {
                    _logger.LogWarning("Cannot remove temporary abilities for Buff {BuffId}: CardInstance {CardId} not found",
                        buff.Id, buff.TargetCardInstanceId);
                    return;
                }

                foreach (var abilityEffect in abilityEffects)
                {
                    var abilityConfigs = abilityEffect.GrantedAbilityConfigs;
                    foreach (var abilityConfig in abilityConfigs)
                    {
                        // 使用更精确的查找逻辑：根据SourceBuffId查找临时能力
                        var cardAbilities = _abilityManager.GetAbilitiesForCard(buff.TargetCardInstanceId);
                        var abilitiesToRemove = cardAbilities.Where(a =>
                            a.Config.Id == abilityConfig.Id &&
                            IsTemporaryAbilityFromBuff(a, buff.Id.ToString())).ToList();

                        foreach (var abilityToRemove in abilitiesToRemove)
                        {
                            _abilityManager.UnregisterAbility(abilityToRemove);
                            _logger.LogDebug("Unregistered temporary ability {AbilityTag} (ID: {AbilityId}) from Buff {BuffId} for card {CardId}",
                                abilityToRemove.AbilityTag, abilityConfig.Id, buff.Id, targetCardInstance.InstanceId);
                        }

                        if (abilitiesToRemove.Count == 0)
                        {
                            _logger.LogDebug("No matching temporary abilities found to remove for AbilityConfig {AbilityId} in Buff {BuffId}",
                                abilityConfig.Id, buff.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling AbilityBuffEffect removal for Buff {BuffId}", buff.Id);
            }
        }

        /// <summary>
        /// 检查能力是否是来自指定Buff的临时能力
        /// </summary>
        /// <param name="ability">要检查的能力</param>
        /// <param name="buffId">Buff ID</param>
        /// <returns>是否是来自指定Buff的临时能力</returns>
        private bool IsTemporaryAbilityFromBuff(Ability ability, string buffId)
        {
            if (ability.Config.Metadata == null)
                return false;

            // 检查是否标记为临时能力
            if (!ability.Config.Metadata.TryGetValue("IsTemporary", out var isTemporaryObj) ||
                !(isTemporaryObj is bool isTemporary) || !isTemporary)
                return false;

            // 检查是否来自指定的Buff
            if (!ability.Config.Metadata.TryGetValue("SourceBuffId", out var sourceBuffIdObj) ||
                !(sourceBuffIdObj is string sourceBuffId) || sourceBuffId != buffId)
                return false;

            return true;
        }

        /// <summary>
        /// 激活Buff中的所有能力
        /// </summary>
        /// <param name="buff">要激活的Buff</param>
        public void ActivateBuffAbilities(Buff buff)
        {
            if (!buff.IsActive)
            {
                _logger.LogWarning("Attempted to activate abilities for inactive Buff {BuffId}", buff.Id);
                return;
            }

            HandleAbilityBuffRegistration(buff);
        }

        /// <summary>
        /// 停用Buff中的所有能力
        /// </summary>
        /// <param name="buff">要停用的Buff</param>
        public void DeactivateBuffAbilities(Buff buff)
        {
            HandleAbilityBuffRemoval(buff);
        }

        /// <summary>
        /// 对目标卡牌应用沉默效果
        /// </summary>
        /// <param name="target">目标卡牌实例</param>
        /// <param name="sourceCard">施放沉默的卡牌实例（可选）</param>
        /// <param name="sourceAbility">施放沉默的能力实例（可选）</param>
        /// <returns>沉默应用结果</returns>
        public Task<SilenceApplicationResult> ApplySilenceAsync(
            CardInstance target,
            CardInstance? sourceCard = null,
            Ability? sourceAbility = null)
        {
            if (target == null)
            {
                return Task.FromResult(new SilenceApplicationResult
                {
                    Success = false,
                    ErrorMessage = "Target cannot be null"
                });
            }

            _logger.LogInformation("Applying silence to card {CardInstanceId}", target.InstanceId);

            var result = new SilenceApplicationResult { Success = true };
            var generatedEvents = new List<IGameEvent>();

            try
            {
                // 1. Set silence status
                target.IsSilenced = true;

                // 2. Remove keyword and ability buffs (but keep stat buffs)
                var buffsToRemove = new List<Buff>();
                if (_buffsByCardInstanceId.TryGetValue(target.InstanceId, out var buffList))
                {
                    foreach (var buff in buffList.ToList()) // ToList to avoid modification during iteration
                    {
                        if (ShouldRemoveBuffOnSilence(buff))
                        {
                            buffsToRemove.Add(buff);
                            buffList.Remove(buff);

                            // Handle AbilityBuffEffect removal
                            HandleAbilityBuffRemoval(buff);

                            // Generate BuffRemovedEvent
                            var buffRemovedEvent = new Events.BuffRemovedEvent(buff, "silenced");
                            generatedEvents.Add(buffRemovedEvent);
                        }
                    }

                    // Clean up empty list
                    if (buffList.Count == 0)
                    {
                        _buffsByCardInstanceId.Remove(target.InstanceId);
                    }
                }

                result.RemovedBuffs = buffsToRemove;

                // 3. Generate SilenceAppliedEvent
                var silenceAppliedEvent = new Events.SilenceAppliedEvent(target, sourceCard, sourceAbility);
                generatedEvents.Add(silenceAppliedEvent);

                result.GeneratedEvents = generatedEvents;

                _logger.LogInformation("Successfully applied silence to card {CardInstanceId}. Removed {BuffCount} buffs.",
                    target.InstanceId, buffsToRemove.Count);

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying silence to card {CardInstanceId}", target.InstanceId);
                return Task.FromResult(new SilenceApplicationResult
                {
                    Success = false,
                    ErrorMessage = $"Error applying silence: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 移除目标卡牌的沉默效果
        /// </summary>
        /// <param name="target">目标卡牌实例</param>
        /// <returns>沉默移除结果</returns>
        public Task<SilenceRemovalResult> RemoveSilenceAsync(CardInstance target)
        {
            if (target == null)
            {
                return Task.FromResult(new SilenceRemovalResult
                {
                    Success = false,
                    ErrorMessage = "Target cannot be null"
                });
            }

            _logger.LogInformation("Removing silence from card {CardInstanceId}", target.InstanceId);

            try
            {
                // Set silence status to false
                target.IsSilenced = false;

                // Generate SilenceRemovedEvent
                var silenceRemovedEvent = new Events.SilenceRemovedEvent(target);
                var generatedEvents = new List<object> { silenceRemovedEvent };

                _logger.LogInformation("Successfully removed silence from card {CardInstanceId}", target.InstanceId);

                return Task.FromResult(new SilenceRemovalResult
                {
                    Success = true,
                    GeneratedEvents = generatedEvents
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing silence from card {CardInstanceId}", target.InstanceId);
                return Task.FromResult(new SilenceRemovalResult
                {
                    Success = false,
                    ErrorMessage = $"Error removing silence: {ex.Message}"
                });
            }
        }



        /// <summary>
        /// 判断Buff是否应该在沉默时被移除
        /// </summary>
        /// <param name="buff">要检查的Buff</param>
        /// <returns>是否应该移除</returns>
        private static bool ShouldRemoveBuffOnSilence(Buff buff)
        {
            if (buff.Effects == null) return false;

            foreach (var effect in buff.Effects)
            {
                switch (effect)
                {
                    case KeywordBuffEffect:
                    case AbilityBuffEffect:
                        return true; // Remove keyword and ability buffs
                    case StatBuffEffect:
                        return false; // Keep stat buffs
                    default:
                        return false; // Unknown effect type, keep it
                }
            }

            return false;
        }
    }
}