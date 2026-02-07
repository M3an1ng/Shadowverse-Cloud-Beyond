using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    public static class DslQueryOperations
    {
        public static Random? RandomOverride { get; set; }

        public static bool IsAllowedDistinctKey(object? key)
        {
            if (key == null)
            {
                return true;
            }

            if (key is string)
            {
                return true;
            }

            return key.GetType().IsValueType;
        }

        public static IEnumerable<T> DistinctBy<T>(IEnumerable<T>? source, Func<T, object?> keySelector)
        {
            if (source == null)
            {
                return Enumerable.Empty<T>();
            }

            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return DistinctByIterator(source, keySelector);
        }

        public static IEnumerable<T> TakeFirst<T>(IEnumerable<T>? source, int count)
        {
            if (source == null || count <= 0)
            {
                return Enumerable.Empty<T>();
            }

            return source.Take(count);
        }

        public static IEnumerable<T> TakeLast<T>(IEnumerable<T>? source, int count)
        {
            if (source == null || count <= 0)
            {
                return Enumerable.Empty<T>();
            }

            var list = source as IList<T> ?? source.ToList();
            if (count >= list.Count)
            {
                return list;
            }

            return list.Skip(list.Count - count);
        }

        public static IEnumerable<T> TakeRandom<T>(IEnumerable<T>? source, int count)
        {
            if (source == null || count <= 0)
            {
                return Enumerable.Empty<T>();
            }

            var list = source as IList<T> ?? source.ToList();
            if (count >= list.Count)
            {
                return list;
            }

            var rng = RandomOverride ?? Random.Shared;
            return list.OrderBy(_ => rng.Next()).Take(count);
        }

        private static IEnumerable<T> DistinctByIterator<T>(IEnumerable<T> source, Func<T, object?> keySelector)
        {
            var seen = new HashSet<object?>();
            foreach (var item in source)
            {
                var key = keySelector(item);
                if (!IsAllowedDistinctKey(key))
                {
                    throw new InvalidOperationException("Distinct key must be a non-entity value type or string.");
                }

                if (seen.Add(key))
                {
                    yield return item;
                }
            }
        }
    }
}
