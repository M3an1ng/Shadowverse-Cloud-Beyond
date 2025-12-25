using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Abilities.JsonConverters
{
    public sealed class LenientEffectKeywordConverter : JsonConverter<EffectKeyword>
    {
        public override EffectKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return EffectKeyword.None;
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                // Legacy compatibility: some payloads used `abilityTag: ["Fanfare"]`.
                EffectKeyword first = EffectKeyword.None;

                if (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    first = reader.TokenType switch
                    {
                        JsonTokenType.String => Keywords.ParseEffectKeywordOrDefault(reader.GetString(), defaultValue: EffectKeyword.None),
                        JsonTokenType.Number when reader.TryGetInt32(out var arrayNumeric) && Enum.IsDefined(typeof(EffectKeyword), arrayNumeric) => (EffectKeyword)arrayNumeric,
                        _ => EffectKeyword.None
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
                return Enum.IsDefined(typeof(EffectKeyword), numeric) ? (EffectKeyword)numeric : EffectKeyword.None;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString();
                return Keywords.ParseEffectKeywordOrDefault(text, defaultValue: EffectKeyword.None);
            }

            return EffectKeyword.None;
        }

        public override void Write(Utf8JsonWriter writer, EffectKeyword value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
