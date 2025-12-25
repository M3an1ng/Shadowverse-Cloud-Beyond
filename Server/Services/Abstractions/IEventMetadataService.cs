using System.Collections.Generic;
using ShadowverseBeyond.Server.Dtos;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    /// <summary>
    /// Defines the contract for a service that provides metadata for game events.
    /// </summary>
    public interface IEventMetadataService
    {
        /// <summary>
        /// Gets the metadata for all discoverable game events.
        /// </summary>
        /// <returns>A collection of event metadata DTOs.</returns>
        IEnumerable<EventMetadataDto> GetAllEventMetadata();
    }
}