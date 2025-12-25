using System;

namespace ShadowverseBeyond.Server.Domain.Events.Metadata
{
    /// <summary>
    /// Provides UI and system metadata for a specific game event class.
    /// This attribute links a concrete event class to its corresponding EventType enum
    /// and provides user-friendly information for the editor UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GameEventMetadataAttribute : Attribute
    {
        /// <summary>
        /// The user-friendly display name for the event in the UI (e.g., "当卡牌进入战场时").
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// A detailed description of when this event triggers and what data it carries.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// The core EventType enum value that this event class represents.
        /// This is crucial for linking the metadata to the game's trigger system.
        /// </summary>
        public EventType EventType { get; }

        public string? CategoryId { get; }

        public string? CategoryLabel { get; }

        public EventMetadataVisibility Visibility { get; }

        public GameEventMetadataAttribute(
            string displayName,
            string description,
            EventType eventType,
            string? categoryId = null,
            string? categoryLabel = null,
            EventMetadataVisibility visibility = EventMetadataVisibility.Visible)
        {
            DisplayName = displayName;
            Description = description;
            EventType = eventType;
            CategoryId = categoryId;
            CategoryLabel = categoryLabel;
            Visibility = visibility;
        }
    }
}
