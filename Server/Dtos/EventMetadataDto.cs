using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Metadata;

namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// DTO for exposing the metadata of a single game event to the UI.
    /// </summary>
    public class EventMetadataDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public string CategoryLabel { get; set; } = string.Empty;
        public EventMetadataVisibility Visibility { get; set; } = EventMetadataVisibility.Visible;
        public EventType EventType { get; set; }
        public List<EventParameterMetadataDto> Parameters { get; set; } = new();
    }

    /// <summary>
    /// DTO for exposing the metadata of a single game event parameter.
    /// </summary>
    public class EventParameterMetadataDto
    {
        public string PropertyName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public GameEventParameterScope Scope { get; set; } = GameEventParameterScope.Both;
    }
}
