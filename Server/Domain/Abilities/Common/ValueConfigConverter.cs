using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Common
{
    public class ValueConfigConverter : JsonConverter<ValueConfig>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(ValueConfig).IsAssignableFrom(typeToConvert);
        }

        public override ValueConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                using var document = JsonDocument.ParseValue(ref reader);
                var element = document.RootElement;
                return new StaticValueConfig
                {
                    Value = element.ValueKind == JsonValueKind.Undefined ? null : element.Clone()
                };
            }

            var jsonObject = JsonNode.Parse(ref reader)?.AsObject();
            if (jsonObject == null)
            {
                return null;
            }

            // Use "$type" for polymorphism, which is a more standard convention.
            var typeNode = FindPropertyCaseInsensitive(jsonObject, "$type");
            var typeValue = typeNode is JsonValue typeValueNode
                ? typeValueNode.TryGetValue<string>(out var typeString) ? typeString : typeValueNode.ToString()
                : typeNode?.ToString();

            var valueNode = FindPropertyCaseInsensitive(jsonObject, "Value");
            var formulaNode = FindPropertyCaseInsensitive(jsonObject, "Formula");

            if (!string.IsNullOrWhiteSpace(typeValue))
            {
                if (string.Equals(typeValue, nameof(StaticValueConfig), StringComparison.OrdinalIgnoreCase))
                {
                    return new StaticValueConfig
                    {
                        Value = valueNode != null ? ConvertJsonNode(valueNode) : null
                    };
                }

                if (string.Equals(typeValue, nameof(FormulaValueConfig), StringComparison.OrdinalIgnoreCase))
                {
                    return new FormulaValueConfig
                    {
                        Formula = ReadString(formulaNode) ?? string.Empty
                    };
                }
            }

            if (formulaNode != null && valueNode == null)
            {
                return new FormulaValueConfig
                {
                    Formula = ReadString(formulaNode) ?? string.Empty
                };
            }

            if (valueNode != null)
            {
                return new StaticValueConfig
                {
                    Value = ConvertJsonNode(valueNode)
                };
            }

            // If no type is specified, assume it's a static value for backward compatibility.
            var fallbackOptions = new JsonSerializerOptions(options)
            {
                PropertyNameCaseInsensitive = true
            };
            return jsonObject.Deserialize<StaticValueConfig>(fallbackOptions);

        }

        private static JsonNode? FindPropertyCaseInsensitive(JsonObject jsonObject, string name)
        {
            foreach (var entry in jsonObject)
            {
                if (string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }
            return null;
        }

        private static object? ConvertJsonNode(JsonNode node)
        {
            using var document = JsonDocument.Parse(node.ToJsonString());
            return document.RootElement.Clone();
        }

        private static string? ReadString(JsonNode? node)
        {
            if (node == null)
            {
                return null;
            }

            if (node is JsonValue jsonValue)
            {
                if (jsonValue.TryGetValue<string>(out var text))
                {
                    return text;
                }
            }

            return node.ToString();
        }

        public override void Write(Utf8JsonWriter writer, ValueConfig value, JsonSerializerOptions options)
        {
            // Use the actual type of the object for serialization.
            // This ensures that all properties of the derived class are included.
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
