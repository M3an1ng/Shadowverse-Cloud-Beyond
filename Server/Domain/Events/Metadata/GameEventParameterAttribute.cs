using System;

namespace ShadowverseBeyond.Server.Domain.Events.Metadata
{
    /// <summary>
    /// Provides UI metadata for the public properties of a game event class.
    /// This allows the editor UI to understand and display the data carried by an event.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class GameEventParameterAttribute : Attribute
    {
        /// <summary>
        /// The user-friendly display name for the parameter in the UI.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// A detailed description of what this parameter represents.
        /// </summary>
        public string Description { get; }

        public GameEventParameterScope Scope { get; set; } = GameEventParameterScope.Input;

        /// <summary>
        /// Controls whether this parameter should appear in trigger timing tooltips.
        /// Hidden parameters remain available to runtime and DSL metadata consumers.
        /// </summary>
        public bool ShowInTriggerTips { get; set; } = true;

        /// <summary>
        /// Optional DSL display type override for UI (e.g., "事件源", "事件目标").
        /// </summary>
        public string? DslDisplayTypeName { get; set; }

        public GameEventParameterAttribute(string displayName, string description)
        {
            DisplayName = displayName;
            Description = description;
        }
    }
}
