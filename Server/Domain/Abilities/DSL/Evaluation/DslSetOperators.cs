using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    public static class DslSetOperators
    {
        public static IEnumerable<T> Union<T>(IEnumerable<T>? left, IEnumerable<T>? right)
        {
            if (left == null && right == null)
            {
                return Enumerable.Empty<T>();
            }
            if (left == null)
            {
                return right ?? Enumerable.Empty<T>();
            }
            if (right == null)
            {
                return left;
            }

            return left.Union(right, GetComparer<T>());
        }

        public static IEnumerable<T> Intersect<T>(IEnumerable<T>? left, IEnumerable<T>? right)
        {
            if (left == null || right == null)
            {
                return Enumerable.Empty<T>();
            }

            return left.Intersect(right, GetComparer<T>());
        }

        public static IEnumerable<T> Except<T>(IEnumerable<T>? left, IEnumerable<T>? right)
        {
            if (left == null)
            {
                return Enumerable.Empty<T>();
            }
            if (right == null)
            {
                return left;
            }

            return left.Except(right, GetComparer<T>());
        }

        private static IEqualityComparer<T> GetComparer<T>()
        {
            return InstanceIdComparerCache<T>.Comparer ?? EqualityComparer<T>.Default;
        }

        private static class InstanceIdComparerCache<T>
        {
            public static readonly IEqualityComparer<T>? Comparer = InstanceIdComparer<T>.TryCreate();
        }

        private sealed class InstanceIdComparer<T> : IEqualityComparer<T>
        {
            private readonly Func<T, Guid?> _getInstanceId;

            private InstanceIdComparer(Func<T, Guid?> getInstanceId)
            {
                _getInstanceId = getInstanceId;
            }

            public bool Equals(T? x, T? y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;

                var leftId = _getInstanceId(x);
                var rightId = _getInstanceId(y);
                if (leftId.HasValue && rightId.HasValue)
                {
                    return leftId.Value == rightId.Value;
                }

                return EqualityComparer<T>.Default.Equals(x, y);
            }

            public int GetHashCode(T obj)
            {
                if (obj == null) return 0;
                var id = _getInstanceId(obj);
                return id?.GetHashCode() ?? EqualityComparer<T>.Default.GetHashCode(obj);
            }

            public static IEqualityComparer<T>? TryCreate()
            {
                var property = typeof(T).GetProperty("InstanceId", BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    return null;
                }

                if (property.PropertyType != typeof(Guid) && property.PropertyType != typeof(Guid?))
                {
                    return null;
                }

                var param = Expression.Parameter(typeof(T), "item");
                Expression body = Expression.Property(param, property);
                if (property.PropertyType == typeof(Guid))
                {
                    body = Expression.Convert(body, typeof(Guid?));
                }
                var lambda = Expression.Lambda<Func<T, Guid?>>(body, param);
                return new InstanceIdComparer<T>(lambda.Compile());
            }
        }
    }
}
