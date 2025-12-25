using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Abilities.JsonConverters
{
    /// <summary>
    /// A custom JSON converter that deserializes a JSON array with a single TargetConfig element
    /// into a Dictionary<string, TargetConfig> with a default key "defaultTarget".
    /// If the JSON is already an object, it deserializes it as a normal dictionary.
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
                if (list != null && list.Count == 1)
                {
                    // If it's an array with one element, create a dictionary with the default key.
                    return new Dictionary<string, TargetConfig>
                    {
                        { DefaultKey, list[0] }
                    };
                }

                // For empty arrays or arrays with more than one element, return an empty dictionary.
                return new Dictionary<string, TargetConfig>();
            }

            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            throw new JsonException($"Expected a JSON object or an array with a single TargetConfig element, but got {reader.TokenType}.");
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, TargetConfig> value, JsonSerializerOptions options)
        {
            // Writing can use the default implementation.
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}