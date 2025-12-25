using System;

namespace ShadowverseBeyond.Server.Domain.Query.Metadata
{
    /// <summary>
    /// An attribute to provide UI metadata for IQueryOperation implementations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class QueryOperationMetadataAttribute : Attribute
    {
        /// <summary>
        /// The user-friendly name of the query operation to be displayed in the UI.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// A detailed description of what the query operation does.
        /// </summary>
        public string Description { get; }

        public QueryOperationMetadataAttribute(string displayName, string description)
        {
            DisplayName = displayName;
            Description = description;
        }
    }
}