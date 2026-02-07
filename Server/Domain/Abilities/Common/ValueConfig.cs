using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Common
{

    /// <summary>
    /// Abstract base class for configuring how a value is obtained.
    /// It uses a custom JsonConverter to handle polymorphic deserialization based on the 'Type' property.
    /// </summary>
    [JsonConverter(typeof(ValueConfigConverter))]
    public abstract class ValueConfig
    {
        /// <summary>
        /// Gets the type of the value configuration, used by the JsonConverter for deserialization.
        /// </summary>
        public abstract ValueType Type { get; }

        /// <summary>
        /// Resolves the configured value within a given game context.
        /// </summary>
        /// <param name="context">The current game context.</param>
        /// <param name="resolver">The resolver service, which can evaluate formulas.</param>
        /// <returns>The resolved value.</returns>
        public abstract object? Resolve(GameContext context, IValueSourceResolver resolver);
    }

    /// <summary>
    /// Represents a configuration for a fixed, static value.
    /// </summary>
    public class StaticValueConfig : ValueConfig
    {
        public override ValueType Type => ValueType.Static;

        /// <summary>
        /// The static value.
        /// </summary>
        public object? Value { get; set; }

        public override object? Resolve(GameContext context, IValueSourceResolver resolver)
        {
            if (this.Value is JsonElement jsonElement)
            {
                return ConvertJsonElement(jsonElement);
            }
            return this.Value;
        }

        private object? ConvertJsonElement(JsonElement jsonElement)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Number:
                    return jsonElement.TryGetInt32(out int intValue) ? intValue : jsonElement.GetDouble();
                case JsonValueKind.String:
                    return jsonElement.GetString();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Array:
                    return jsonElement;
                case JsonValueKind.Null:
                    return null;
                default:
                    // Preserve complex structures for downstream typed deserialization.
                    return jsonElement;
            }
        }
    }

    /// <summary>
    /// Represents a configuration for a value that is calculated via a DSL formula.
    /// </summary>
    public class FormulaValueConfig : ValueConfig
    {
        public override ValueType Type => ValueType.Formula;

        /// <summary>
        /// The DSL formula to be evaluated.
        /// </summary>
        public string Formula { get; set; } = string.Empty;

        public override object? Resolve(GameContext context, IValueSourceResolver resolver)
        {
            // Delegate formula evaluation to the resolver service.
            return resolver.ResolveFormulaValue(this.Formula, context);
        }
    }
}
