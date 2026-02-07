using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Query
{
    /// <summary>
    /// The concrete implementation for parsing query DSL strings.
    /// </summary>
    public class QueryDslParser : IQueryDslParser
    {
        /// <inheritdoc />
        public QueryConfig Parse(string dsl)
        {
            if (string.IsNullOrWhiteSpace(dsl))
            {
                throw new ArgumentException("DSL string cannot be null or whitespace.", nameof(dsl));
            }

            return new QueryConfig
            {
                // Parsing is delegated to the unified DSL compiler path.
                SourcePoolExpression = dsl.Trim(),
                Operations = new List<IQueryOperation>()
            };
        }
    }
}
