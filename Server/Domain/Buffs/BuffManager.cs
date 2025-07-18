using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common; // For DurationType
using ShadowverseBeyond.Server.Domain.Abilities; // For Ability class
using ShadowverseBeyond.Server.Domain.Events; // For IEventProcessor
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // For ConditionConfig
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetConfig
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For DomainOperationResult
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
        private readonly Dictionary<Guid, List<BuffConfig>> _buffsByCardInstanceId = new();
        private readonly ILogger<BuffManager> _logger;
        private readonly BuffStackingEngine _stackingEngine;
        private readonly AbilityManager _abilityManager;

        // Callback to get CardInstance by ID (injected by GameService or other container)
        private Func<Guid, CardInstance?>? _getCardInstanceCallback;

        public BuffManager(ILogger<BuffManager> logger, BuffStackingEngine stackingEngine, AbilityManager abilityManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stackingEngine = stackingEngine ?? throw new ArgumentNullException(nameof(stackingEngine));
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
        public DomainOperationResult RegisterBuff(BuffConfig buff)
        {
            var result = new DomainOperationResult();
            if (buff == null)
            {
                _logger.LogWarning("Attempted to register a null buff.");
                result.Success = false;
                result.ErrorMessage = "Buff cannot be null.";
                return result;
            }

            var cardId = buff.TargetCardInstanceId;
            if (!_buffsByCardInstanceId.TryGetValue(cardId, out var buffList))
            {
                buffList = new List<BuffConfig>();
                _buffsByCardInstanceId[cardId] = buffList;
            }

            var stackingResult = _stackingEngine.EvaluateStacking(buff, buffList);

            if (!stackingResult.AllowAdd)
            {
                _logger.LogInformation("Buff {BuffId} registration rejected: {Reason}", buff.Id, stackingResult.Reason);
                result.Success = false;
                result.ErrorMessage = stackingResult.Reason;
                return result;
            }

            // Remove buffs that need to be replaced
            foreach (var buffToRemove in stackingResult.BuffsToRemove)
            {
                buffList.Remove(buffToRemove);
                HandleAbilityBuffRemoval(buffToRemove);
                if (buffToRemove.SourceAbility?.SourceCard != null)
                {
                    var sourceCard = GetCardInstance(buffToRemove.SourceAbility.SourceCard.InstanceId);
                    if (sourceCard != null)
                    {
                        result.GeneratedEvents.Add(new BuffRemovedEvent(buffToRemove, sourceCard, "replaced"));
                    }
                }
                _logger.LogDebug("Removed non-stackable Buff {BuffId} from Card {CardInstanceId} to make room for new buff", buffToRemove.Id, cardId);
            }

            // Add the new buff
            buffList.Add(buff);
            HandleAbilityBuffRegistration(buff);
            if (buff.SourceAbility?.SourceCard != null)
            {
                var sourceCardApplied = GetCardInstance(buff.SourceAbility.SourceCard.InstanceId);
                if (sourceCardApplied != null)
                {
                    result.GeneratedEvents.Add(new BuffAppliedEvent(buff, sourceCardApplied));
                }
            }
            _logger.LogDebug("Registered Buff {BuffId} for Card {CardInstanceId}. Replaced: {ReplacedCount}.", buff.Id, cardId, stackingResult.BuffsToRemove.Count);

            result.Success = true;
            return result;
        }

        /// <inheritdoc />
        public DomainOperationResult UnregisterBuff(Guid buffId)
        {
            var result = new DomainOperationResult();
            if (buffId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to unregister a buff with an empty ID.");
                result.Success = false;
                result.ErrorMessage = "Buff ID cannot be empty.";
                return result;
            }

            foreach (var kvp in _buffsByCardInstanceId)
            {
                var buffList = kvp.Value;
                var buffToRemove = buffList.FirstOrDefault(b => b.Id == buffId);
                if (buffToRemove != null)
                {
                    HandleAbilityBuffRemoval(buffToRemove);
                    buffList.Remove(buffToRemove);
                    _logger.LogDebug("Unregistered Buff {BuffId} from Card {CardInstanceId}.", buffId, kvp.Key);

                    if (buffToRemove.SourceAbility?.SourceCard != null)
                    {
                        var sourceCardRemoved = GetCardInstance(buffToRemove.SourceAbility.SourceCard.InstanceId);
                        if (sourceCardRemoved != null)
                        {
                            result.GeneratedEvents.Add(new BuffRemovedEvent(buffToRemove, sourceCardRemoved, "manual"));
                        }
                    }

                    if (buffList.Count == 0)
                    {
                        _buffsByCardInstanceId.Remove(kvp.Key);
                    }
                    result.Success = true;
                    return result;
                }
            }

            _logger.LogWarning("Buff {BuffId} not found for unregistration.", buffId);
            result.Success = false;
            result.ErrorMessage = $"Buff with ID {buffId} not found.";
            return result;
        }

        /// <inheritdoc />
        public List<BuffConfig> GetBuffsForCard(Guid cardInstanceId)
        {
            if (cardInstanceId == Guid.Empty)
            {
                return new List<BuffConfig>(); // Return empty list for invalid ID
            }

            if (_buffsByCardInstanceId.TryGetValue(cardInstanceId, out var buffList))
            {
                // Return a copy to prevent external modification of the internal list
                return buffList.ToList();
            }

            return new List<BuffConfig>(); // No buffs found for this card
        }

        /// <inheritdoc />
        public DomainOperationResult RemoveBuffsFromSource(string sourceAbilityId)
        {
            var result = new DomainOperationResult { Success = true };
            var keysToRemove = new List<Guid>();

            foreach (var kvp in _buffsByCardInstanceId)
            {
                var cardId = kvp.Key;
                var buffList = kvp.Value;

                var buffsFromSource = buffList.Where(b => b.SourceAbility.DefinitionId.ToString() == sourceAbilityId).ToList();

                if (buffsFromSource.Any())
                {
                    _logger.LogDebug("Removing {Count} buffs from Card {CardInstanceId} originating from SourceAbility DefinitionId {SourceAbilityDefId}.",
                        buffsFromSource.Count, cardId, sourceAbilityId);

                    foreach (var buffToRemove in buffsFromSource)
                    {
                        HandleAbilityBuffRemoval(buffToRemove);
                        buffList.Remove(buffToRemove);
                        if (buffToRemove.SourceAbility?.SourceCard != null)
                        {
                            var sourceCardSourceRemoved = GetCardInstance(buffToRemove.SourceAbility.SourceCard.InstanceId);
                            if (sourceCardSourceRemoved != null)
                            {
                                result.GeneratedEvents.Add(new BuffRemovedEvent(buffToRemove, sourceCardSourceRemoved, "source_removed"));
                            }
                        }
                    }

                    if (buffList.Count == 0)
                    {
                        keysToRemove.Add(cardId);
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                _buffsByCardInstanceId.Remove(key);
            }

            return result;
        }

        /// <inheritdoc />
        public DomainOperationResult RemoveAllBuffs(Guid cardInstanceId, Func<BuffConfig, bool>? predicate = null)
        {
            var result = new DomainOperationResult { Success = true };
            if (cardInstanceId == Guid.Empty)
            {
                result.Success = false;
                result.ErrorMessage = "CardInstanceId cannot be empty.";
                return result;
            }

            if (_buffsByCardInstanceId.TryGetValue(cardInstanceId, out var buffList))
            {
                var buffsToRemove = predicate == null ? buffList.ToList() : buffList.Where(predicate).ToList();

                if (buffsToRemove.Any())
                {
                    foreach (var buffToRemove in buffsToRemove)
                    {
                        HandleAbilityBuffRemoval(buffToRemove);
                        buffList.Remove(buffToRemove);
                        if (buffToRemove.SourceAbility?.SourceCard != null)
                        {
                            var sourceCardAllRemoved = GetCardInstance(buffToRemove.SourceAbility.SourceCard.InstanceId);
                            if (sourceCardAllRemoved != null)
                            {
                                result.GeneratedEvents.Add(new BuffRemovedEvent(buffToRemove, sourceCardAllRemoved, "removed_all"));
                            }
                        }
                    }
                    _logger.LogDebug("Removed {Count} buffs from Card {CardInstanceId}.", buffsToRemove.Count, cardInstanceId);
                }

                if (buffList.Count == 0)
                {
                    _buffsByCardInstanceId.Remove(cardInstanceId);
                }
            }
            else
            {
                _logger.LogDebug("No buffs found to remove for Card {CardInstanceId}.", cardInstanceId);
            }

            return result;
        }

        // All old methods are removed from here.
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
        private void HandleAbilityBuffRegistration(BuffConfig buff)
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
        private void HandleAbilityBuffRemoval(BuffConfig buff)
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
                        var abilitiesToRemove = new List<IAbility>();
                        foreach (var execAbility in cardAbilities)
                        {
                            if (execAbility is Ability abilityInstance) // Safely cast to Ability
                            {
                                // Now compare AbilityConfig.Id (string) with abilityConfig.Id (string)
                                // and also check if it's a temporary ability from the buff.
                                if (abilityInstance.Config.Id == abilityConfig.Id &&
                                    IsTemporaryAbilityFromBuff(abilityInstance, buff.Id.ToString()))
                                {
                                    abilitiesToRemove.Add(abilityInstance);
                                }
                            }
                        }

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
        public void ActivateBuffAbilities(BuffConfig buff)
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
        public void DeactivateBuffAbilities(BuffConfig buff)
        {
            HandleAbilityBuffRemoval(buff);
        }
    }
}