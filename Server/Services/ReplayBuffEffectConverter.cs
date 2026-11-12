using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Services;

internal sealed class ReplayBuffEffectConverter : JsonConverter<IBuffEffect>
{
    public override IBuffEffect Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var row = document.RootElement;
        return row.GetProperty("$effect").GetString() switch {
            nameof(StatBuffEffect) => row.Deserialize<StatBuffEffect>(options)!,
            nameof(KeywordBuffEffect) => row.Deserialize<KeywordBuffEffect>(options)!,
            nameof(AbilityBuffEffect) => row.Deserialize<AbilityBuffEffect>(options)!,
            nameof(AbilityKeywordBuffEffect) => row.Deserialize<AbilityKeywordBuffEffect>(options)!,
            nameof(ActionUsageLimitBuffEffect) => row.Deserialize<ActionUsageLimitBuffEffect>(options)!,
            nameof(TribeBuffEffect) => new TribeBuffEffect(row.GetProperty("addedTribes").Deserialize<string[]>(options)!),
            nameof(RemoveKeywordBuffEffect) => new RemoveKeywordBuffEffect(row.GetProperty("keywordsToRemove").Deserialize<CardKeyword[]>(options)!),
            nameof(AbilitySuppressionBuffEffect) => new AbilitySuppressionBuffEffect(row.GetProperty("removeMode").Deserialize<RemoveAbilityMode>(options),
                row.GetProperty("abilityDefinitionIds").Deserialize<long[]>(options),row.GetProperty("abilityKeyword").Deserialize<AbilityKeyword>(options),
                row.GetProperty("sourceAbilityRuntimeId").Deserialize<Guid?>(options)),
            _ => throw new JsonException("unsupported_resume_buff_effect")
        };
    }

    public override void Write(Utf8JsonWriter writer,IBuffEffect value,JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("$effect",value.GetType().Name);
        foreach (var property in JsonSerializer.SerializeToElement(value,value.GetType(),options).EnumerateObject()) property.WriteTo(writer);
        writer.WriteEndObject();
    }
}
