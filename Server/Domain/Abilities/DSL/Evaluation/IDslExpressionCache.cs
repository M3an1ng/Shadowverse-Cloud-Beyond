using System;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    /// <summary>
    /// Provides a caching mechanism for compiled DSL expressions.
    /// This service should be registered as a Singleton.
    /// </summary>
    public interface IDslExpressionCache
    {
        /// <summary>
        /// Tries to get a compiled delegate from the cache.
        /// </summary>
        /// <typeparam name="T">The type of the delegate.</typeparam>
        /// <param name="key">The DSL string used as the cache key.</param>
        /// <param name="value">The cached delegate if found.</param>
        /// <returns>True if the item is found in the cache, otherwise false.</returns>
        bool TryGetValue<T>(string key, out T? value);

        /// <summary>
        /// Adds a compiled delegate to the cache.
        /// </summary>
        /// <typeparam name="T">The type of the delegate.</typeparam>
        /// <param name="key">The DSL string used as the cache key.</param>
        /// <param name="value">The compiled delegate to cache.</param>
        void Set<T>(string key, T value);
    }
}