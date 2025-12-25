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
            var jsonObject = JsonNode.Parse(ref reader)?.AsObject();
            if (jsonObject == null)
            {
                return null;
            }

            // Use "$type" for polymorphism, which is a more standard convention.
            if (!jsonObject.TryGetPropertyValue("$type", out var typeNode) || typeNode is null)
            {
                // If no type is specified, assume it's a static value for backward compatibility.
                return jsonObject.Deserialize<StaticValueConfig>(options);
            }

            jsonObject.Remove("$type");

            var typeValue = typeNode.GetValue<string>();

            return typeValue switch
            {
                nameof(StaticValueConfig) => jsonObject.Deserialize<StaticValueConfig>(options),
                nameof(FormulaValueConfig) => jsonObject.Deserialize<FormulaValueConfig>(options),
                _ => throw new JsonException($"Unsupported ValueConfig type: {typeValue}")
            };
        }

        public override void Write(Utf8JsonWriter writer, ValueConfig value, JsonSerializerOptions options)
        {
            // Use the actual type of the object for serialization.
            // This ensures that all properties of the derived class are included.
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}