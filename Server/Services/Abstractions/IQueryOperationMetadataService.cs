using ShadowverseBeyond.Server.Dtos.Query;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    /// <summary>
    /// Service for discovering and providing metadata for IQueryOperation implementations.
    /// </summary>
    public interface IQueryOperationMetadataService
    {
        /// <summary>
        /// Gets the cached metadata for all discoverable query operations.
        /// </summary>
        /// <returns>A list of DTOs representing the query operation metadata.</returns>
        IEnumerable<QueryOperationMetadataDto> GetQueryOperationMetadata();
    }
}