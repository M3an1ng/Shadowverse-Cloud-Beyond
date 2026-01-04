using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    [DslExposable("Counters", Description = "System and temporary counters attached to a card.")]
    public sealed class CardCounters : IReadOnlyDictionary<string, object?>
    {
        private readonly Dictionary<string, int> _values;
        private readonly HashSet<string> _systemKeys;

        public CardCounters(IEnumerable<string> systemKeys)
        {
            _values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _systemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in systemKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                _systemKeys.Add(key);
                _values[key] = 0;
            }
        }

        public int Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return 0;
            }

            if (_values.TryGetValue(key, out var value))
            {
                return value;
            }

            _values[key] = 0;
            return 0;
        }

        public int Set(string key, int value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return 0;
            }

            var normalized = Math.Max(0, value);
            _values[key] = normalized;
            return normalized;
        }

        public int Add(string key, int delta)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return 0;
            }

            var next = Get(key) + delta;
            return Set(key, next);
        }

        public bool TryConsume(string key, int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            var current = Get(key);
            if (current < amount)
            {
                return false;
            }

            Set(key, current - amount);
            return true;
        }

        public void Reset()
        {
            _values.Clear();
            foreach (var key in _systemKeys)
            {
                _values[key] = 0;
            }
        }

        public void CopyFrom(CardCounters source)
        {
            if (source == null)
            {
                return;
            }

            _values.Clear();
            foreach (var pair in source._values)
            {
                _values[pair.Key] = pair.Value;
            }

            foreach (var key in _systemKeys)
            {
                if (!_values.ContainsKey(key))
                {
                    _values[key] = 0;
                }
            }
        }

        [DslExposable("Spellboost", Description = "spellboost counter value.")]
        public int Spellboost => Get(CardInstance.SpellboostCounterKey);

        [DslExposable("Earthsigil", Description = "earthsigil counter value.")]
        public int Earthsigil => Get(CardInstance.EarthsigilCounterKey);

        public object? this[string key] => Get(key);

        public IEnumerable<string> Keys => _values.Keys;

        public IEnumerable<object?> Values => _values.Values.Cast<object?>();

        public int Count => _values.Count;

        public bool ContainsKey(string key)
        {
            return _values.ContainsKey(key);
        }

        public bool TryGetValue(string key, out object? value)
        {
            value = Get(key);
            return true;
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            foreach (var pair in _values)
            {
                yield return new KeyValuePair<string, object?>(pair.Key, pair.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
