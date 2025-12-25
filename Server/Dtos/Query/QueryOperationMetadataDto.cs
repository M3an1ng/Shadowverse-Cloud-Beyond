namespace ShadowverseBeyond.Server.Dtos.Query
{
    /// <summary>
    /// DTO for transferring query operation metadata to the UI.
    /// </summary>
    public class QueryOperationMetadataDto
    {
        /// <summary>
        /// The full type name of the IQueryOperation implementation.
        /// This will be used by the frontend to construct the JSON for the specific operation.
        /// </summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// The user-friendly name of the query operation.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// A detailed description of what the query operation does.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}