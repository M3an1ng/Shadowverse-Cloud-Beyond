using System;
using Microsoft.Extensions.Caching.Memory;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    /// <summary>
    /// Implements the DSL expression cache using IMemoryCache.
    /// </summary>
    public class DslExpressionCache : IDslExpressionCache
    {
        private readonly IMemoryCache _cache;

        public DslExpressionCache(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public bool TryGetValue<T>(string key, out T? value)
        {
            return _cache.TryGetValue(key, out value);
        }

        public void Set<T>(string key, T value)
        {
            // For DSL expressions, the compiled delegate is static and will not change
            // unless the source DSL string changes. Therefore, we can cache it indefinitely.
            // A sliding expiration could be used if we ever expect DSL function definitions
            // to change at runtime, but that is not currently a feature.
            if (value != null)
            {
                _cache.Set(key, value);
            }
        }
    }
}