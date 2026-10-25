using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.JsonConverters;

namespace ShadowverseBeyond.Server.Domain.Abilities;

public static class AbilityConfigJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new LenientEffectKeywordConverter(), new LenientTriggerRelationTypeConverter(), new JsonStringEnumConverter() }
    };

    public static List<AbilityKeywordConfig> ReadKeywords(string? jsonConfig)
    {
        if (string.IsNullOrWhiteSpace(jsonConfig))
        {
            return new List<AbilityKeywordConfig>();
        }

        // 空配置保留既有契约；有内容却无法解析时必须让调用边界报告错误。
        var config = JsonSerializer.Deserialize<AbilityConfig>(jsonConfig, Options);
        if (config == null)
        {
            return new List<AbilityKeywordConfig>();
        }

        if (config.AbilityKeywords == null)
        {
            throw new JsonException("abilityKeywords must not be null.");
        }

        config.NormalizeKeywords();
        return config.AbilityKeywords;
    }
}
