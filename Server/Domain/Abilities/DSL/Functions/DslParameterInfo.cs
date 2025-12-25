using System;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// Provides structured metadata for a single parameter of a DSL function.
    /// This class is essential for the visual editor to dynamically generate UI fields.
    /// </summary>
    public class DslParameterInfo
    {
        /// <summary>
        /// The programmatic name of the parameter (e.g., "condition", "collection").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The expected C# type of the parameter's value.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// A user-friendly description of what the parameter does, used for tooltips in the editor.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Indicates whether this parameter is optional.
        /// </summary>
        public bool IsOptional { get; }

        /// <summary>
        /// The default value to be used if an optional parameter is not provided.
        /// </summary>
        public object? DefaultValue { get; }

        public DslParameterInfo(string name, Type type, string description, bool isOptional = false, object? defaultValue = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Parameter name cannot be null or whitespace.", nameof(name));

            if (isOptional && defaultValue == null && type.IsValueType && Nullable.GetUnderlyingType(type) == null)
            {
                // Assign default value for non-nullable value types if no default is provided
                DefaultValue = Activator.CreateInstance(type);
            }
            else
            {
                DefaultValue = defaultValue;
            }

            Name = name;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Description = description ?? string.Empty;
            IsOptional = isOptional;
        }
    }
}