using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Abilities.JsonConverters
{
    /// <summary>
    /// A custom JSON converter that deserializes target configs from either:
    /// - Object: a normal dictionary of key -> TargetConfig
    /// - Array: a list of TargetConfig mapped to stable keys (target1, target2, ...)
    /// </summary>
    public class SingleTargetConfigToDictionaryConverter : JsonConverter<Dictionary<string, TargetConfig>>
    {
        private const string DefaultKey = "default";

        public override Dictionary<string, TargetConfig>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                // If it's an object, deserialize it as a normal dictionary.
                return JsonSerializer.Deserialize<Dictionary<string, TargetConfig>>(ref reader, options);
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = JsonSerializer.Deserialize<List<TargetConfig>>(ref reader, options);
                if (list == null || list.Count == 0)
                {
                    return new Dictionary<string, TargetConfig>();
                }

                if (list.Count == 1)
                {
                    var single = list[0];
                    if (single != null && single.Order == null)
                    {
                        single.Order = 0;
                    }
                    return new Dictionary<string, TargetConfig>
                    {
                        { DefaultKey, single }
                    };
                }

                var result = new Dictionary<string, TargetConfig>(list.Count);
                for (var index = 0; index < list.Count; index++)
                {
                    var target = list[index];
                    if (target == null)
                    {
                        continue;
                    }

                    if (target.Order == null)
                    {
                        target.Order = index;
                    }

                    var key = $"target{index + 1}";
                    result[key] = target;
                }

                return result;
            }

            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            throw new JsonException($"Expected a JSON object or array for TargetConfig, but got {reader.TokenType}.");
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, TargetConfig> value, JsonSerializerOptions options)
        {
            // Writing can use the default implementation.
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
