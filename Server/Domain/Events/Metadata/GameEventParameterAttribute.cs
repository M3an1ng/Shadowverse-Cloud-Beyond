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

        public GameEventParameterAttribute(string displayName, string description)
        {
            DisplayName = displayName;
            Description = description;
        }
    }
}
