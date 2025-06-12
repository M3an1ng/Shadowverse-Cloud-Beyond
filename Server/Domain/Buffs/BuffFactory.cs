// File: Server/Domain/Buffs/BuffFactory.cs
//
// 相关文档:
// - Buff系统: 文档/05-游戏系统/10-动态效果与Buff系统.md
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities; // For Ability class
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    /// <summary>
    /// Buff工厂接口，负责创建不同类型的Buff实例
    /// </summary>
    public interface IBuffFactory
    {
        /// <summary>
        /// 创建属性修改Buff
        /// </summary>
        /// <param name="target">目标卡牌</param>
        /// <param name="sourceCard">源卡牌</param>
        /// <param name="sourceAbility">源能力</param>
        /// <param name="statType">属性类型</param>
        /// <param name="modificationValue">修改值</param>
        /// <param name="modifierLayer">修改层级</param>
        /// <param name="durationType">持续时间类型</param>
        /// <param name="durationParameter">持续时间参数</param>
        /// <param name="currentTurn">当前回合</param>
        /// <param name="isNonStackable">是否不可叠加</param>
        /// <returns>创建的Buff实例</returns>
        Buff CreateStatBuff(
            CardInstance target,
            CardInstance? sourceCard,
            Ability sourceAbility,
            StatType statType,
            int modificationValue,
            ModifierLayer modifierLayer,
            DurationType durationType,
            object? durationParameter,
            int currentTurn,
            bool isNonStackable = false);

        /// <summary>
        /// 创建关键字Buff
        /// </summary>
        /// <param name="target">目标卡牌</param>
        /// <param name="sourceCard">源卡牌</param>
        /// <param name="sourceAbility">源能力</param>
        /// <param name="keywords">要添加的关键字列表</param>
        /// <param name="durationType">持续时间类型</param>
        /// <param name="durationParameter">持续时间参数</param>
        /// <param name="currentTurn">当前回合</param>
        /// <param name="isNonStackable">是否不可叠加</param>
        /// <returns>创建的Buff实例</returns>
        Buff CreateKeywordBuff(
            CardInstance target,
            CardInstance? sourceCard,
            Ability sourceAbility,
            List<string> keywords,
            DurationType durationType,
            object? durationParameter,
            int currentTurn,
            bool isNonStackable = false);

        /// <summary>
        /// 创建复合Buff（包含多种效果）
        /// </summary>
        /// <param name="target">目标卡牌</param>
        /// <param name="sourceCard">源卡牌</param>
        /// <param name="sourceAbility">源能力</param>
        /// <param name="effects">效果列表</param>
        /// <param name="durationType">持续时间类型</param>
        /// <param name="durationParameter">持续时间参数</param>
        /// <param name="currentTurn">当前回合</param>
        /// <param name="isNonStackable">是否不可叠加</param>
        /// <returns>创建的Buff实例</returns>
        Buff CreateCompositeBuff(
            CardInstance target,
            CardInstance? sourceCard,
            Ability sourceAbility,
            List<IBuffEffect> effects,
            DurationType durationType,
            object? durationParameter,
            int currentTurn,
            bool isNonStackable = false);

        /// <summary>
        /// 根据BuffDefinition创建Buff实例（使用缓存）
        /// </summary>
        /// <param name="buffDefinitionId">BuffDefinition ID</param>
        /// <param name="target">目标卡牌</param>
        /// <param name="sourceCard">源卡牌</param>
        /// <param name="sourceAbility">源能力</param>
        /// <param name="currentTurn">当前回合</param>
        /// <param name="customParameters">自定义参数（可选）</param>
        /// <returns>创建的Buff实例，如果失败则返回null</returns>
        Buff? CreateBuffFromDefinition(
            string buffDefinitionId,
            CardInstance target,
            CardInstance? sourceCard,
            Ability sourceAbility,
            int currentTurn,
            Dictionary<string, object?>? customParameters = null);
    }

    /// <summary>
    /// Buff工厂实现
    /// </summary>
    public class BuffFactory : IBuffFactory
    {
        private readonly ILogger<BuffFactory> _logger;
        private readonly IGameDataProvider? _gameDataProvider;

        public BuffFactory(ILogger<BuffFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gameDataProvider = null;
        }

        public BuffFactory(ILogger<BuffFactory> logger, IGameDataProvider gameDataProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
        }

        /// <inheritdoc />
        public Buff CreateStatBuff(
            CardInstance target,
            CardInstance? sourceCard,
            Ability sourceAbility,
            StatType statType,
            int modificationValue,
            ModifierLayer modifierLayer,
            DurationType durationType,
            object? durationParameter,
            int currentTurn,
            bool isNonStackable = false)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (sourceAbility == null) throw new ArgumentNullException(nameof(sourceAbility));

            var statModification = new StatModification(statType, modificationValue, modifierLayer);

            var statEffect = new StatBuffEffect(new List<StatModification> { statModification });
            var effects = new List<IBuffEffect> { statEffect };

            var buff = new Buff(
                sourceAbility: sourceAbility,
                targetCardInstanceId: target.InstanceId,
                currentTurn: currentTurn,
                effects: effects,
                durationType: durationType,
                durationParameter: durationParameter,
                isNonStackable: isNonStackable
            );

            _logger.LogDebug("Created stat buff {BuffId} for {StatType} modification {Value} on target {TargetId}",
                buff.Id, statType, modificationValue, target.InstanceId);

            return buff;
        }

        /// <inheritdoc />
        public Buff CreateKeywordBuff(
            CardInstance target,
            CardInstance? sourceCard,
            Ability sourceAbility,
            List<string> keywords,
            DurationType durationType,
            object? durationParameter,
            int currentTurn,
            bool isNonStackable = false)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (sourceAbility == null) throw new ArgumentNullException(nameof(sourceAbility));
            if (keywords == null || !keywords.Any()) throw new ArgumentException("Keywords cannot be null or empty", nameof(keywords));

            var keywordEffect = new KeywordBuffEffect(keywords);
            var effects = new List<IBuffEffect> { keywordEffect };

            var buff = new Buff(
                sourceAbility: sourceAbility,
                targetCardInstanceId: target.InstanceId,
                currentTurn: currentTurn,
                effects: effects,
                durationType: durationType,
                durationParameter: durationParameter,
                isNonStackable: isNonStackable
            );

            _logger.LogDebug("Created keyword buff {BuffId} adding keywords [{Keywords}] to target {TargetId}",
                buff.Id, string.Join(", ", keywords), target.InstanceId);

            return buff;
        }

        /// <inheritdoc />
        public Buff CreateCompositeBuff(
            CardInstance target,
            CardInstance? sourceCard,
            Ability sourceAbility,
            List<IBuffEffect> effects,
            DurationType durationType,
            object? durationParameter,
            int currentTurn,
            bool isNonStackable = false)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (sourceAbility == null) throw new ArgumentNullException(nameof(sourceAbility));
            if (effects == null || !effects.Any()) throw new ArgumentException("Effects cannot be null or empty", nameof(effects));

            var buff = new Buff(
                sourceAbility: sourceAbility,
                targetCardInstanceId: target.InstanceId,
                currentTurn: currentTurn,
                effects: effects,
                durationType: durationType,
                durationParameter: durationParameter,
                isNonStackable: isNonStackable
            );

            _logger.LogDebug("Created composite buff {BuffId} with {EffectCount} effects on target {TargetId}",
                buff.Id, effects.Count, target.InstanceId);

            return buff;
        }

        /// <inheritdoc />
        public Buff? CreateBuffFromDefinition(
            string buffDefinitionId,
            CardInstance target,
            CardInstance? sourceCard,
            Ability sourceAbility,
            int currentTurn,
            Dictionary<string, object?>? customParameters = null)
        {
            if (_gameDataProvider == null)
            {
                _logger.LogWarning("GameDataProvider not available. Cannot create buff from definition: {BuffDefinitionId}", buffDefinitionId);
                return null;
            }

            try
            {
                // 从缓存获取BuffDefinition
                var buffDefinition = _gameDataProvider.GetBuffDefinition(buffDefinitionId);
                if (buffDefinition == null)
                {
                    _logger.LogWarning("BuffDefinition not found in cache: {BuffDefinitionId}", buffDefinitionId);
                    return null;
                }

                _logger.LogDebug("Creating buff from definition: {BuffDefinitionId} for target: {TargetId}",
                    buffDefinitionId, target.InstanceId);

                // 解析BuffDefinition的JSON配置
                var buffConfig = ParseBuffDefinitionConfig(buffDefinition);
                if (buffConfig == null)
                {
                    _logger.LogError("Failed to parse BuffDefinition config: {BuffDefinitionId}", buffDefinitionId);
                    return null;
                }

                // 根据BuffDefinition的类型创建相应的Buff
                return CreateBuffFromConfig(buffDefinition, buffConfig, target, sourceCard, sourceAbility, currentTurn, customParameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating buff from definition: {BuffDefinitionId}", buffDefinitionId);
                return null;
            }
        }

        /// <summary>
        /// 解析BuffDefinition的JSON配置
        /// </summary>
        private BuffDefinitionConfig? ParseBuffDefinitionConfig(BuffDefinition buffDefinition)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(buffDefinition.JsonConfig))
                {
                    _logger.LogWarning("BuffDefinition has empty JsonConfig: {BuffDefinitionId}", buffDefinition.Id);
                    return null;
                }

                var config = JsonSerializer.Deserialize<BuffDefinitionConfig>(buffDefinition.JsonConfig);
                return config;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse BuffDefinition JsonConfig: {BuffDefinitionId}", buffDefinition.Id);
                return null;
            }
        }

        /// <summary>
        /// 根据配置创建Buff实例
        /// </summary>
        private Buff? CreateBuffFromConfig(
            BuffDefinition buffDefinition,
            BuffDefinitionConfig config,
            CardInstance target,
            CardInstance? sourceCard,
            Ability sourceAbility,
            int currentTurn,
            Dictionary<string, object?>? customParameters)
        {
            try
            {
                // 确定持续时间
                var durationType = ParseDurationType(config.Duration?.Type ?? buffDefinition.DefaultDurationType);
                var durationParameter = config.Duration?.Parameter ?? buffDefinition.DefaultDurationParameter;

                // 根据BuffDefinition的类型创建相应的效果
                var effects = CreateEffectsFromConfig(config, customParameters);
                if (effects == null || !effects.Any())
                {
                    _logger.LogWarning("No valid effects created for BuffDefinition: {BuffDefinitionId}", buffDefinition.Id);
                    return null;
                }

                // 创建Buff实例
                var buff = new Buff(
                    sourceAbility: sourceAbility,
                    targetCardInstanceId: target.InstanceId,
                    currentTurn: currentTurn,
                    effects: effects,
                    durationType: durationType,
                    durationParameter: durationParameter,
                    isNonStackable: buffDefinition.IsNonStackable
                );

                _logger.LogDebug("Successfully created buff: {BuffId} from definition: {BuffDefinitionId}",
                    buff.Id, buffDefinition.Id);

                return buff;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating buff from config for definition: {BuffDefinitionId}", buffDefinition.Id);
                return null;
            }
        }

        /// <summary>
        /// 解析持续时间类型
        /// </summary>
        private DurationType ParseDurationType(string durationType)
        {
            return durationType.ToLowerInvariant() switch
            {
                "permanent" => DurationType.Permanent,
                "untilendofturn" => DurationType.EndOfTurn,
                "limitedturns" => DurationType.LimitedTurns,
                "untiltriggered" => DurationType.SpecificEvent,
                _ => DurationType.Permanent
            };
        }

        /// <summary>
        /// 根据配置创建效果列表
        /// </summary>
        private List<IBuffEffect>? CreateEffectsFromConfig(BuffDefinitionConfig config, Dictionary<string, object?>? customParameters)
        {
            var effects = new List<IBuffEffect>();

            foreach (var effectConfig in config.Effects ?? new List<BuffEffectConfig>())
            {
                var effect = CreateEffectFromConfig(effectConfig, customParameters);
                if (effect != null)
                {
                    effects.Add(effect);
                }
            }

            return effects.Any() ? effects : null;
        }

        /// <summary>
        /// 根据单个效果配置创建效果实例
        /// </summary>
        private IBuffEffect? CreateEffectFromConfig(BuffEffectConfig effectConfig, Dictionary<string, object?>? customParameters)
        {
            try
            {
                switch (effectConfig.Type.ToLowerInvariant())
                {
                    case "modifystats":
                        return CreateStatEffectFromConfig(effectConfig, customParameters);

                    case "addkeyword":
                        return CreateKeywordEffectFromConfig(effectConfig, customParameters);

                    default:
                        _logger.LogWarning("Unknown effect type: {EffectType}", effectConfig.Type);
                        return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating effect from config: {EffectType}", effectConfig.Type);
                return null;
            }
        }

        /// <summary>
        /// 创建属性修改效果
        /// </summary>
        private IBuffEffect? CreateStatEffectFromConfig(BuffEffectConfig effectConfig, Dictionary<string, object?>? customParameters)
        {
            try
            {
                // 从配置中提取属性修改信息
                var statType = GetParameterValue<string>(effectConfig.Parameters, "statType", customParameters) ?? "Attack";
                var value = GetParameterValue<int?>(effectConfig.Parameters, "value", customParameters) ?? 1;
                var layer = GetParameterValue<string>(effectConfig.Parameters, "layer", customParameters) ?? "Additive";

                // 解析StatType
                if (!Enum.TryParse<StatType>(statType, true, out var parsedStatType))
                {
                    _logger.LogWarning("Invalid StatType: {StatType}, defaulting to Attack", statType);
                    parsedStatType = StatType.Attack;
                }

                // 解析ModifierLayer
                if (!Enum.TryParse<ModifierLayer>(layer, true, out var parsedLayer))
                {
                    _logger.LogWarning("Invalid ModifierLayer: {Layer}, defaulting to Additive", layer);
                    parsedLayer = ModifierLayer.Additive;
                }

                // 创建StatModification
                var statModification = new StatModification(parsedStatType, value, parsedLayer);
                var statEffect = new StatBuffEffect(new List<StatModification> { statModification });

                _logger.LogDebug("Created stat effect: {StatType} {Value} ({Layer})", parsedStatType, value, parsedLayer);
                return statEffect;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating stat effect from config");
                return null;
            }
        }

        /// <summary>
        /// 创建关键字效果
        /// </summary>
        private IBuffEffect? CreateKeywordEffectFromConfig(BuffEffectConfig effectConfig, Dictionary<string, object?>? customParameters)
        {
            try
            {
                // 从配置中提取关键字信息
                var keyword = GetParameterValue<string>(effectConfig.Parameters, "keyword", customParameters);
                var keywords = GetParameterValue<List<string>>(effectConfig.Parameters, "keywords", customParameters);

                var keywordList = new List<string>();

                // 处理单个关键字
                if (!string.IsNullOrEmpty(keyword))
                {
                    keywordList.Add(keyword);
                }

                // 处理关键字列表
                if (keywords != null && keywords.Any())
                {
                    keywordList.AddRange(keywords);
                }

                if (!keywordList.Any())
                {
                    _logger.LogWarning("No keywords specified for keyword effect");
                    return null;
                }

                // 创建KeywordBuffEffect
                var keywordEffect = new KeywordBuffEffect(keywordList);

                _logger.LogDebug("Created keyword effect: [{Keywords}]", string.Join(", ", keywordList));
                return keywordEffect;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating keyword effect from config");
                return null;
            }
        }

        /// <summary>
        /// 从配置参数中获取指定类型的值
        /// </summary>
        private T? GetParameterValue<T>(Dictionary<string, object?> configParameters, string key, Dictionary<string, object?>? customParameters)
        {
            // 优先使用自定义参数
            if (customParameters != null && customParameters.TryGetValue(key, out var customValue))
            {
                if (customValue is T typedCustomValue)
                {
                    return typedCustomValue;
                }

                // 尝试转换类型
                try
                {
                    return (T)Convert.ChangeType(customValue, typeof(T));
                }
                catch
                {
                    _logger.LogWarning("Failed to convert custom parameter {Key} to type {Type}", key, typeof(T).Name);
                }
            }

            // 使用配置参数
            if (configParameters.TryGetValue(key, out var configValue))
            {
                if (configValue is T typedConfigValue)
                {
                    return typedConfigValue;
                }

                // 尝试转换类型
                try
                {
                    return (T)Convert.ChangeType(configValue, typeof(T));
                }
                catch
                {
                    _logger.LogWarning("Failed to convert config parameter {Key} to type {Type}", key, typeof(T).Name);
                }
            }

            return default(T);
        }
    }

    /// <summary>
    /// BuffDefinition配置类，用于反序列化JSON配置
    /// </summary>
    public class BuffDefinitionConfig
    {
        public List<BuffEffectConfig> Effects { get; set; } = new();
        public BuffDurationConfig? Duration { get; set; }
    }

    /// <summary>
    /// Buff效果配置
    /// </summary>
    public class BuffEffectConfig
    {
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, object?> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Buff持续时间配置
    /// </summary>
    public class BuffDurationConfig
    {
        public string Type { get; set; } = "Permanent";
        public int? Parameter { get; set; }
    }
}
