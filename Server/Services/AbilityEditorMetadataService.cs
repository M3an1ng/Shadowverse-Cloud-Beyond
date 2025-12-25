using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Services.Abstractions;
using System.ComponentModel;
using System.Reflection;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using System.Linq;

namespace ShadowverseBeyond.Server.Services
{
    public class AbilityEditorMetadataService : IAbilityEditorMetadataService
    {
        private readonly IEffectUIMetadataService _effectMetadataService;
        private readonly IEventMetadataService _eventMetadataService;
        private readonly IDslMetadataService _dslMetadataService;
        private readonly IQueryOperationMetadataService _queryOperationMetadataService;

        public AbilityEditorMetadataService(
            IEffectUIMetadataService effectMetadataService,
            IEventMetadataService eventMetadataService,
            IDslMetadataService dslMetadataService,
            IQueryOperationMetadataService queryOperationMetadataService)
        {
            _effectMetadataService = effectMetadataService;
            _eventMetadataService = eventMetadataService;
            _dslMetadataService = dslMetadataService;
            _queryOperationMetadataService = queryOperationMetadataService;
        }

        public AbilityEditorMetadataDto GetAbilityEditorMetadata()
        {
            var effects = _effectMetadataService.GetAllEffectUIMetadata().ToList();

            var enumOptionSources = effects
                .SelectMany(e => e.Parameters ?? [])
                .Select(p => p.EnumName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct()
                .ToList()!;

            var optionsProviderKeys = effects
                .SelectMany(e => e.Parameters ?? [])
                .Select(p => p.OptionsProviderKey)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k!)
                .Distinct()
                .ToList()!;

            return new AbilityEditorMetadataDto
            {
                Effects = effects,
                Events = _eventMetadataService.GetAllEventMetadata().ToList(),
                Dsl = _dslMetadataService.GetDslMetadata(),
                QueryOperations = _queryOperationMetadataService.GetQueryOperationMetadata().ToList(),
                RelationTypes = GetTriggerRelationTypeMetadata(),
                EnumOptionSources = enumOptionSources,
                OptionsProviderKeys = optionsProviderKeys
            };
        }

        /// <summary>
        /// 获取触发关系类型元数据
        /// </summary>
        private List<EnumMetadataDto> GetTriggerRelationTypeMetadata()
        {
            var relationTypes = new List<EnumMetadataDto>();
            var enumValues = Enum.GetValues<TriggerRelationType>();

            foreach (var enumValue in enumValues)
            {
                var fieldInfo = typeof(TriggerRelationType).GetField(enumValue.ToString());
                var descriptionAttribute = fieldInfo?.GetCustomAttribute<DescriptionAttribute>();

                relationTypes.Add(new EnumMetadataDto
                {
                    Value = enumValue.ToString(),
                    DisplayName = descriptionAttribute?.Description ?? enumValue.ToString(),
                    Description = $"触发关系类型：{descriptionAttribute?.Description ?? enumValue.ToString()}"
                });
            }

            return relationTypes;
        }
    }
}
