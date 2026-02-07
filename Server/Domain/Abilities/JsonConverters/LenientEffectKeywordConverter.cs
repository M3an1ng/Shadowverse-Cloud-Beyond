using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Abilities.JsonConverters
{
    public sealed class LenientEffectKeywordConverter : JsonConverter<AbilityKeyword>
    {
        public override AbilityKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return AbilityKeyword.None;
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                // Legacy compatibility: some payloads used `abilityTag: ["Fanfare"]`.
                AbilityKeyword first = AbilityKeyword.None;

                if (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    first = reader.TokenType switch
                    {
                        JsonTokenType.String => Keywords.ParseEffectKeywordOrDefault(reader.GetString(), defaultValue: AbilityKeyword.None),
                        JsonTokenType.Number when reader.TryGetInt32(out var arrayNumeric) && Enum.IsDefined(typeof(AbilityKeyword), arrayNumeric) => (AbilityKeyword)arrayNumeric,
                        _ => AbilityKeyword.None
                    };

                    // Consume remaining items until EndArray
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                    }
                }

                return first;
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numeric))
            {
                return Enum.IsDefined(typeof(AbilityKeyword), numeric) ? (AbilityKeyword)numeric : AbilityKeyword.None;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString();
                return Keywords.ParseEffectKeywordOrDefault(text, defaultValue: AbilityKeyword.None);
            }

            return AbilityKeyword.None;
        }

        public override void Write(Utf8JsonWriter writer, AbilityKeyword value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
