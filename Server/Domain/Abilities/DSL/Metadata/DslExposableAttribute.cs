using System;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata
{
    /// <summary>
    /// Marks a class, property, or method as exposable to the visual DSL editor.
    /// This attribute provides the necessary UI metadata, such as a user-friendly display name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class DslExposableAttribute : Attribute
    {
        /// <summary>
        /// The user-friendly name to be displayed in the DSL editor UI.
        /// For example, "玩家状态" for the PlayerState class.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// An optional detailed description for the item, which can be used as a tooltip in the editor.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        public DslExposableAttribute(string displayName)
        {
            DisplayName = displayName;
        }
    }
}