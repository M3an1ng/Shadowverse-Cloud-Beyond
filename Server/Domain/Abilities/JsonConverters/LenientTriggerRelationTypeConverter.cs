using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Abilities.JsonConverters
{
    /// <summary>
    /// Allows legacy/relaxed values for TriggerRelationType.
    /// Accepts "Self" (maps to SourceSelf) and numeric values; defaults to Any.
    /// </summary>
    public sealed class LenientTriggerRelationTypeConverter : JsonConverter<TriggerRelationType>
    {
        public override TriggerRelationType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return TriggerRelationType.Any;
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numeric))
            {
                return Enum.IsDefined(typeof(TriggerRelationType), numeric)
                    ? (TriggerRelationType)numeric
                    : TriggerRelationType.Any;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var text = (reader.GetString() ?? string.Empty).Trim();
                if (string.Equals(text, "Self", StringComparison.OrdinalIgnoreCase))
                {
                    return TriggerRelationType.SourceSelf;
                }

                if (Enum.TryParse<TriggerRelationType>(text, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }
            }

            return TriggerRelationType.Any;
        }

        public override void Write(Utf8JsonWriter writer, TriggerRelationType value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
