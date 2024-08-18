#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace DarkConfig.Internal {
    /// <summary>
    /// Dictionary that supports both case-sensitive and case-insensitive lookups while retaining the case of the key
    /// </summary>
    /// <typeparam name="TValueType">Type of values in the dictionary (keys are strings)</typeparam>
    internal class MultiCaseDictionary<TValueType> : IEnumerable<(string, TValueType)> {
        readonly Dictionary<string, (string Key, TValueType Value)> _dictionary;

        public MultiCaseDictionary() : this(0) { }

        public MultiCaseDictionary(int capacity) {
            _dictionary = new Dictionary<string, (string Key, TValueType Value)>(capacity, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetValue(string key, out TValueType? value, bool ignoreCase) {
            if (_dictionary.TryGetValue(key, out var match)) {
                if (ignoreCase) {
                    value = match.Value;
                    return true;
                }

                if (key.Equals(match.Key)) {
                    value = match.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public void Add(string key, TValueType value) {
            _dictionary.Add(key, (key, value));
        }

        public bool TryAdd(string key, TValueType value) {
            return _dictionary.TryAdd(key, (key, value));
        }

        public IEnumerator<(string, TValueType)> GetEnumerator() {
            return _dictionary.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable) _dictionary.Values).GetEnumerator();
        }
    }
}
