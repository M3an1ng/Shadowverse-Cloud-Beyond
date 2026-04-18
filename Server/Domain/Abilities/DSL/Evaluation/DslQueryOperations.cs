using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    public static class DslQueryOperations
    {
        private sealed record DistinctCandidate<T>(T Item, int Index, object? RawKey, IReadOnlyList<object?> ExpandedKeys);

        private sealed class DistinctAtomicKeyComparer : IEqualityComparer<object?>
        {
            public static DistinctAtomicKeyComparer Instance { get; } = new();

            public new bool Equals(object? x, object? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is string leftString && y is string rightString)
                {
                    return StringComparer.Ordinal.Equals(leftString, rightString);
                }

                return object.Equals(x, y);
            }

            public int GetHashCode(object? obj)
            {
                if (obj is string stringValue)
                {
                    return StringComparer.Ordinal.GetHashCode(stringValue);
                }

                return obj?.GetHashCode() ?? 0;
            }
        }

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

        public static IEnumerable<T> DistinctBy<T>(
            IEnumerable<T>? source,
            Func<T, object?> keySelector,
            DistinctKeySemantics keySemantics = DistinctKeySemantics.Auto,
            GameContext? context = null)
        {
            if (source == null)
            {
                return Enumerable.Empty<T>();
            }

            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return DistinctByIterator(source, keySelector, keySemantics, context);
        }

        public static IEnumerable<T> TakeFirst<T>(IEnumerable<T>? source, int count)
        {
            if (!TryPrepareSelection(source, count, out var list, out var effectiveCount))
            {
                return Enumerable.Empty<T>();
            }

            return list.Take(effectiveCount);
        }

        public static IEnumerable<T> TakeLast<T>(IEnumerable<T>? source, int count)
        {
            if (!TryPrepareSelection(source, count, out var list, out var effectiveCount))
            {
                return Enumerable.Empty<T>();
            }

            return list.Reverse().Take(effectiveCount);
        }

        public static IEnumerable<T> TakeRandom<T>(IEnumerable<T>? source, int count)
        {
            if (!TryPrepareSelection(source, count, out var list, out var effectiveCount))
            {
                return Enumerable.Empty<T>();
            }

            var rng = RandomOverride ?? Random.Shared;
            return list.OrderBy(_ => rng.Next()).Take(effectiveCount);
        }

        private static bool TryPrepareSelection<T>(IEnumerable<T>? source, int count, out IList<T> list, out int effectiveCount)
        {
            if (source == null || count <= 0)
            {
                list = Array.Empty<T>();
                effectiveCount = 0;
                return false;
            }

            list = source as IList<T> ?? source.ToList();
            effectiveCount = Math.Min(count, list.Count);
            return effectiveCount > 0;
        }

        private static IEnumerable<T> DistinctByIterator<T>(
            IEnumerable<T> source,
            Func<T, object?> keySelector,
            DistinctKeySemantics keySemantics,
            GameContext? context)
        {
            var materialized = source.Select((item, index) => new
            {
                Item = item,
                Index = index,
                Key = keySelector(item)
            }).ToList();

            var resolvedSemantics = ResolveKeySemantics(materialized.Select(entry => entry.Key), keySemantics);
            var candidates = materialized
                .Select(entry => new DistinctCandidate<T>(
                    entry.Item,
                    entry.Index,
                    entry.Key,
                    resolvedSemantics == DistinctKeySemantics.Scalar
                        ? Array.Empty<object?>()
                        : ExpandAtomicKeys(entry.Key, resolvedSemantics, context)))
                .ToList();

            if (resolvedSemantics == DistinctKeySemantics.Scalar)
            {
                foreach (var item in DistinctScalar(candidates))
                {
                    yield return item;
                }

                yield break;
            }

            foreach (var item in DistinctExpanded(candidates))
            {
                yield return item;
            }
        }

        private static DistinctKeySemantics ResolveKeySemantics(IEnumerable<object?> keys, DistinctKeySemantics declaredSemantics)
        {
            if (declaredSemantics != DistinctKeySemantics.Auto)
            {
                return declaredSemantics;
            }

            var sawScalar = false;
            var sawList = false;

            foreach (var key in keys)
            {
                if (key == null)
                {
                    continue;
                }

                if (IsAllowedDistinctKey(key))
                {
                    sawScalar = true;
                    continue;
                }

                if (key is IEnumerable && key is not string)
                {
                    sawList = true;
                    continue;
                }

                throw new InvalidOperationException(
                    $"Distinct key type '{DescribeType(key)}' is not supported. Use a scalar value or a list of scalar values.");
            }

            if (sawScalar && sawList)
            {
                throw new InvalidOperationException(
                    "Distinct key must resolve consistently to either scalar values or list values.");
            }

            return sawList
                ? DistinctKeySemantics.ListExpanded
                : DistinctKeySemantics.Scalar;
        }

        private static IEnumerable<T> DistinctScalar<T>(IReadOnlyList<DistinctCandidate<T>> source)
        {
            var seen = new HashSet<object?>();
            foreach (var entry in source)
            {
                var key = entry.RawKey;
                if (!IsAllowedDistinctKey(key))
                {
                    throw new InvalidOperationException("Distinct key must be a non-entity value type or string.");
                }

                if (seen.Add(key))
                {
                    yield return entry.Item;
                }
            }
        }

        private static IEnumerable<T> DistinctExpanded<T>(IReadOnlyList<DistinctCandidate<T>> candidates)
        {
            if (candidates.Count == 0)
            {
                return Enumerable.Empty<T>();
            }

            var totalMatches = ComputeMaximumMatchingCount(
                candidates,
                0,
                new HashSet<object?>(DistinctAtomicKeyComparer.Instance));

            if (totalMatches == 0)
            {
                return Enumerable.Empty<T>();
            }

            var selected = new List<T>();
            var unavailableKeys = new HashSet<object?>(DistinctAtomicKeyComparer.Instance);
            var selectedCount = 0;

            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate.ExpandedKeys.Count == 0)
                {
                    continue;
                }

                foreach (var atomicKey in candidate.ExpandedKeys)
                {
                    if (unavailableKeys.Contains(atomicKey))
                    {
                        continue;
                    }

                    var nextUnavailable = new HashSet<object?>(unavailableKeys, DistinctAtomicKeyComparer.Instance)
                    {
                        atomicKey
                    };

                    var remainingMatches = ComputeMaximumMatchingCount(candidates, index + 1, nextUnavailable);
                    if (selectedCount + 1 + remainingMatches != totalMatches)
                    {
                        continue;
                    }

                    unavailableKeys.Add(atomicKey);
                    selected.Add(candidate.Item);
                    selectedCount += 1;
                    break;
                }

                if (selectedCount == totalMatches)
                {
                    break;
                }
            }

            return selected;
        }

        private static int ComputeMaximumMatchingCount<T>(
            IReadOnlyList<DistinctCandidate<T>> candidates,
            int startIndex,
            HashSet<object?> unavailableKeys)
        {
            if (startIndex >= candidates.Count)
            {
                return 0;
            }

            var keyAssignments = new Dictionary<object?, int>(DistinctAtomicKeyComparer.Instance);
            var matchCount = 0;

            for (var localIndex = startIndex; localIndex < candidates.Count; localIndex++)
            {
                var visited = new HashSet<object?>(DistinctAtomicKeyComparer.Instance);
                if (TryAssign(localIndex, candidates, unavailableKeys, keyAssignments, visited))
                {
                    matchCount += 1;
                }
            }

            return matchCount;
        }

        private static bool TryAssign<T>(
            int candidateIndex,
            IReadOnlyList<DistinctCandidate<T>> candidates,
            HashSet<object?> unavailableKeys,
            Dictionary<object?, int> keyAssignments,
            HashSet<object?> visitedKeys)
        {
            foreach (var atomicKey in candidates[candidateIndex].ExpandedKeys)
            {
                if (unavailableKeys.Contains(atomicKey) || !visitedKeys.Add(atomicKey))
                {
                    continue;
                }

                if (!keyAssignments.TryGetValue(atomicKey, out var assignedCandidateIndex)
                    || TryAssign(assignedCandidateIndex, candidates, unavailableKeys, keyAssignments, visitedKeys))
                {
                    keyAssignments[atomicKey] = candidateIndex;
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<object?> ExpandAtomicKeys(object? rawKey, DistinctKeySemantics semantics, GameContext? context)
        {
            if (rawKey == null)
            {
                return Array.Empty<object?>();
            }

            if (semantics == DistinctKeySemantics.TribeExpanded)
            {
                return ExpandTribeKeys(rawKey, context);
            }

            if (rawKey is string || rawKey is not IEnumerable sequence)
            {
                throw new InvalidOperationException(
                    $"Distinct list key must evaluate to a collection of scalar values, but got '{DescribeType(rawKey)}'.");
            }

            var expanded = new List<object?>();
            var seen = new HashSet<object?>(DistinctAtomicKeyComparer.Instance);
            foreach (var value in sequence)
            {
                var normalized = NormalizeAtomicValue(value);
                if (normalized == null || !seen.Add(normalized))
                {
                    continue;
                }

                expanded.Add(normalized);
            }

            return expanded;
        }

        private static IReadOnlyList<object?> ExpandTribeKeys(object rawKey, GameContext? context)
        {
            if (rawKey is string || rawKey is not IEnumerable sequence)
            {
                throw new InvalidOperationException(
                    $"Distinct tribe key must evaluate to a tribe collection, but got '{DescribeType(rawKey)}'.");
            }

            var tribeValues = sequence.Cast<object?>()
                .Select(value => value?.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var semantics = context?.ServiceProvider.GetService<ITribeSemantics>();
            var expandedTribes = semantics?.Expand(tribeValues) ?? tribeValues;
            var expandedSet = new HashSet<string>(expandedTribes, StringComparer.OrdinalIgnoreCase);
            var ordered = new List<object?>();

            foreach (var tribeValue in tribeValues)
            {
                if (!expandedSet.Contains(tribeValue))
                {
                    continue;
                }

                if (!ordered.Contains(tribeValue, DistinctAtomicKeyComparer.Instance))
                {
                    ordered.Add(tribeValue);
                }
            }

            foreach (var tribeValue in expandedTribes
                .OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!ordered.Contains(tribeValue, DistinctAtomicKeyComparer.Instance))
                {
                    ordered.Add(tribeValue);
                }
            }

            return ordered;
        }

        private static object? NormalizeAtomicValue(object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string stringValue)
            {
                var trimmed = stringValue.Trim();
                return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
            }

            if (value.GetType().IsValueType)
            {
                return value;
            }

            throw new InvalidOperationException(
                $"Distinct list key contains unsupported atomic value type '{DescribeType(value)}'.");
        }

        private static string DescribeType(object? value)
        {
            return value?.GetType().Name ?? "null";
        }
    }
}
