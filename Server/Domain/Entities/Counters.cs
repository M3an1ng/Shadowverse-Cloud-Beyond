using System;
using System.Collections;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    [DslExposable("Counters", Description = "System and temporary counters attached to a card.")]
    public sealed class Counters : IReadOnlyDictionary<string, int>
    {
        private readonly Dictionary<string, int> _values;
        private readonly Dictionary<string, int> _defaults;
        private readonly HashSet<string> _systemKeys;

        public Counters(IEnumerable<string> systemKeys, IReadOnlyDictionary<string, int>? initialValues = null)
        {
            _values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _defaults = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _systemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (initialValues != null)
            {
                foreach (var pair in initialValues)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    _defaults[pair.Key] = Math.Max(0, pair.Value);
                }
            }

            foreach (var key in systemKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                _systemKeys.Add(key);
                if (!_defaults.ContainsKey(key))
                {
                    _defaults[key] = 0;
                }
            }

            Reset();
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

            if (_defaults.TryGetValue(key, out var defaultValue))
            {
                _values[key] = defaultValue;
                return defaultValue;
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

        public bool TryInitialize(string key, int value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (_defaults.ContainsKey(key) || _values.ContainsKey(key))
            {
                return false;
            }

            var normalized = Math.Max(0, value);
            _defaults[key] = normalized;
            _values[key] = normalized;
            return true;
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
            foreach (var pair in _defaults)
            {
                _values[pair.Key] = pair.Value;
            }
        }

        public void CopyFrom(Counters source)
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

            foreach (var pair in _defaults)
            {
                if (!_values.ContainsKey(pair.Key))
                {
                    _values[pair.Key] = pair.Value;
                }
            }
        }

        [DslExposable("Spellboost", Description = "spellboost counter value.")]
        public int Spellboost => Get(CardInstance.SpellboostCounterKey);

        [DslExposable("Earthsigil", Description = "earthsigil counter value.")]
        public int Earthsigil => Get(CardInstance.EarthsigilCounterKey);

        public int this[string key] => Get(key);

        public IEnumerable<string> Keys => _values.Keys;

        public IEnumerable<int> Values => _values.Values;

        public int Count => _values.Count;

        public bool ContainsKey(string key)
        {
            return _values.ContainsKey(key);
        }

        public bool TryGetValue(string key, out int value)
        {
            value = Get(key);
            return true;
        }

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            foreach (var pair in _values)
            {
                yield return pair;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
