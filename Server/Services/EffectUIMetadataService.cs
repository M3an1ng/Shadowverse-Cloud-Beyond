using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// Scans assemblies to discover and cache UI metadata for card effects.
    /// </summary>
    public class EffectUIMetadataService : IEffectUIMetadataService
    {
        private readonly IReadOnlyDictionary<EffectType, EffectUIMetadataDto> _metadataCache;
        private readonly IEffectHandlerRegistry _effectHandlerRegistry;
        private static readonly IReadOnlyDictionary<EffectType, string> EffectCategoryMap = new Dictionary<EffectType, string>
        {
            { EffectType.Attack, AbilityEditorCategories.BaseActionId },
            { EffectType.Damage, AbilityEditorCategories.BaseActionId },
            { EffectType.Heal, AbilityEditorCategories.BaseActionId },
            { EffectType.Evolve, AbilityEditorCategories.BaseActionId },
            { EffectType.Play, AbilityEditorCategories.BaseActionId },
            { EffectType.Fusion, AbilityEditorCategories.BaseActionId },

            { EffectType.Summon, AbilityEditorCategories.PositionChangeId },
            { EffectType.GetToken, AbilityEditorCategories.PositionChangeId },
            { EffectType.Draw, AbilityEditorCategories.PositionChangeId },
            { EffectType.Discard, AbilityEditorCategories.PositionChangeId },
            { EffectType.Return, AbilityEditorCategories.PositionChangeId },
            { EffectType.Vanish, AbilityEditorCategories.PositionChangeId },
            { EffectType.Destroy, AbilityEditorCategories.PositionChangeId },
            { EffectType.Transform, AbilityEditorCategories.PositionChangeId },
            { EffectType.Reveal, AbilityEditorCategories.PositionChangeId },

            { EffectType.AddBuff, AbilityEditorCategories.TempStateId },
            { EffectType.RemoveBuff, AbilityEditorCategories.TempStateId },
            { EffectType.SetKeyword, AbilityEditorCategories.TempStateId },

            { EffectType.SetPP, AbilityEditorCategories.ResourceValueId },
            { EffectType.Ramp, AbilityEditorCategories.ResourceValueId },
            { EffectType.SetEP, AbilityEditorCategories.ResourceValueId },
            { EffectType.SetEPMax, AbilityEditorCategories.ResourceValueId },
            { EffectType.SetStatus, AbilityEditorCategories.ResourceValueId },

            { EffectType.TurnStart, AbilityEditorCategories.TurnFlowId },
            { EffectType.TurnEnd, AbilityEditorCategories.TurnFlowId },
            { EffectType.Mulligan, AbilityEditorCategories.TurnFlowId }
        };

        public EffectUIMetadataService(IEffectHandlerRegistry effectHandlerRegistry)
        {
            _effectHandlerRegistry = effectHandlerRegistry ?? throw new ArgumentNullException(nameof(effectHandlerRegistry));
            _metadataCache = DiscoverAndCacheMetadata();
        }

        public EffectUIMetadataDto? GetEffectUIMetadata(EffectType effectType)
        {
            _metadataCache.TryGetValue(effectType, out var metadata);
            return metadata;
        }

        public IEnumerable<EffectUIMetadataDto> GetAllEffectUIMetadata()
        {
            return _metadataCache.Values;
        }

        private Dictionary<EffectType, EffectUIMetadataDto> DiscoverAndCacheMetadata()
        {
            var cache = new Dictionary<EffectType, EffectUIMetadataDto>();

            // 获取所有EffectType枚举值
            var allEffectTypes = Enum.GetValues<EffectType>();

            foreach (var effectType in allEffectTypes)
            {
                // 使用EffectHandlerRegistry获取对应的参数类型
                if (!_effectHandlerRegistry.TryGetHandler(effectType, out var registration) || !registration.HasValue)
                {
                    continue; // 没有找到对应的Handler，跳过
                }

                var (handlerType, paramsType) = registration.Value;

                // 检查参数类型是否有EffectMetadata特性
                var effectMeta = paramsType.GetCustomAttribute<EffectMetadataAttribute>();
                if (effectMeta == null)
                {
                    continue; // 没有UI元数据，跳过
                }

                var normalizedCategoryId = AbilityEditorCategories.NormalizeId(effectMeta.CategoryId);
                if (normalizedCategoryId == AbilityEditorCategories.UncategorizedId &&
                    EffectCategoryMap.TryGetValue(effectType, out var mappedCategoryId))
                {
                    normalizedCategoryId = mappedCategoryId;
                }

                var uiMetadata = new EffectUIMetadataDto
                {
                    EffectTypeName = effectType.ToString(),
                    DisplayName = effectMeta.DisplayName,
                    Description = effectMeta.Description,
                    CategoryId = normalizedCategoryId,
                    CategoryLabel = AbilityEditorCategories.GetLabelOrDefault(normalizedCategoryId, effectMeta.CategoryLabel),
                    Parameters = []
                };

                var properties = paramsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<ParameterMetadataAttribute>() != null);

                foreach (var prop in properties)
                {
                    var paramMeta = prop.GetCustomAttribute<ParameterMetadataAttribute>();
                    if (paramMeta == null) continue;

                    var typeName = TypeNameHelper.GetCleanTypeName(prop.PropertyType);
                    var inferredEnumName = TypeNameHelper.TryGetEnumName(prop.PropertyType);
                    var enumName = paramMeta.EnumName ?? inferredEnumName;
                    var optionsProviderKey = paramMeta.OptionsProviderKey;

                    // If the control is not a dropdown, keep option source fields empty to reduce noise.
                    if (paramMeta.ControlType != UiControlType.Dropdown)
                    {
                        enumName = null;
                        optionsProviderKey = null;
                    }

                    uiMetadata.Parameters.Add(new EffectParameterMetadataDto
                    {
                        PropertyName = prop.Name,
                        DisplayName = paramMeta.DisplayName,
                        TypeName = typeName,
                        ControlType = paramMeta.ControlType,
                        EnumName = enumName,
                        OptionsProviderKey = optionsProviderKey,
                        DefaultValue = paramMeta.DefaultValue,
                        Description = paramMeta.Description,
                        Min = paramMeta.Min,
                        Max = paramMeta.Max
                    });
                }

                cache[effectType] = uiMetadata;
            }

            return cache;
        }
    }
}
