namespace ShadowverseBeyond.Server.Domain.Query
{
    /// <summary>
    /// Defines a service that parses a query DSL string into a structured CardQueryConfig object.
    /// </summary>
    public interface IQueryDslParser
    {
        /// <summary>
        /// Parses the provided query DSL string.
        /// </summary>
        /// <param name="dsl">The query DSL string to parse.</param>
        /// <returns>A CardQueryConfig object representing the parsed query.</returns>
        QueryConfig Parse(string dsl);
    }
}