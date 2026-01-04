using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShadowverseBeyond.Server.Domain.Abilities.JsonConverters
{
    /// <summary>
    /// Converts TargetSelector from either a string (legacy) or an array of strings.
    /// String values are split by '|', ',', ';' into a list of keys.
    /// </summary>
    public class TargetSelectorListConverter : JsonConverter<List<string>>
    {
        public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return new List<string>();
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var raw = reader.GetString() ?? string.Empty;
                return SplitKeys(raw);
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = JsonSerializer.Deserialize<List<string>>(ref reader, options) ?? new List<string>();
                return list
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .ToList();
            }

            throw new JsonException($"Expected a string or array for TargetSelector, but got {reader.TokenType}.");
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value ?? new List<string>(), options);
        }

        private static List<string> SplitKeys(string raw)
        {
            return raw
                .Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
    }
}
