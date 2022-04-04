using System;
using System.Collections.Generic;

namespace DarkConfig {
    public enum DocNodeType {
        Invalid,
        Dictionary,
        List,
        Scalar
    }

    public class DocNodeAccessException : Exception {
        public DocNodeAccessException(string message) : base(message) { }
    }

    /// DocNode represents a node of a parsed document. 
    /// DocNode is a union type, requiring no casting but behaving differently 
    /// depending on the underlying value.
    /// DocNode also assumes that all Dictionaries have strings as keys.
    public abstract class DocNode : IEquatable<DocNode> {
        /// Shape of data contained in the node.
        public abstract DocNodeType Type { get; }

        /// Access the node as if it was a List.
        public abstract DocNode this[int index] { get; set; }

        /// Access the node as if it was a Dictionary.
        public abstract DocNode this[string key] { get; set; }

        /// Number of items in the collection
        public abstract int Count { get; }

        /// Value of scalar as a string.
        public abstract string StringValue { get; set; }

        /// Returns true if the key is in the dictionary
        public abstract bool ContainsKey(string key, bool ignoreCase = false);

        /// <summary>
        /// Only valid for dictionaries.
        /// s
        /// Try to get the value for the given key.
        /// </summary>
        /// <param name="key">Key of the value to retrieve</param>
        /// <param name="ignoreCase">if true, does case-insensitive key comparison</param>
        /// <param name="result">Set to the value if it's found, otherwise null</param>
        /// <returns>True if the value was found, false otherwise.</returns>
        public abstract bool TryGetValue(string key, bool ignoreCase, out DocNode result);

        /// Iterates over the values of the list
        public abstract IEnumerable<DocNode> Values { get; }

        /// Iterates over a dictionary 
        public abstract IEnumerable<KeyValuePair<string, DocNode>> Pairs { get; }

        /// String describing the position and context in the source format (e.g. line number).
        public abstract string SourceInformation { get; }
        
        public T As<T>() {
            var result = default(T);
            Configs.Reify(ref result, this);
            return result;
        }

        /// <summary>
        /// Only works on lists.  Checks for the given doc node StringValue in the list.  
        /// </summary>
        /// <param name="item">String to search for</param>
        /// <returns>True if the string exists in this list</returns>
        /// <exception cref="DocNodeAccessException">Thrown if this is not a list</exception>
        public bool Contains(string item) {
            if (Type != DocNodeType.List) {
                throw new DocNodeAccessException("Expected List, is " + Type);
            }

            for (int i = 0; i < Count; i++) {
                if (this[i].StringValue == item) {
                    return true;
                }
            }

            return false;
        }

        public bool Equals(DocNode other) {
            if (other == null) {
                return false;
            }
            
            var self = this;
            if (other.Type != self.Type) {
                return false;
            }

            if (Equals(self, other)) {
                return true;
            }
            
            switch (self.Type) {
                case DocNodeType.Scalar:
                    if (other.StringValue == null || self.StringValue == null) {
                        return self.StringValue == other.StringValue;
                    }
                    return other.StringValue.Equals(self.StringValue);
                case DocNodeType.List:
                    if (other.Count != self.Count) {
                        return false;
                    }
                    for (int i = 0; i < self.Count; i++) {
                        if (!self[i].Equals(other[i])) {
                            return false;
                        }
                    }

                    return true;
                case DocNodeType.Dictionary:
                    if (other.Count != self.Count) return false;
                    
                    using (var iter1 = self.Pairs.GetEnumerator())
                    using (var iter2 = other.Pairs.GetEnumerator()) {
                        while (iter1.MoveNext() && iter2.MoveNext()) {
                            if (iter1.Current.Key != iter2.Current.Key) return false;
                            if (!iter1.Current.Value.Equals(iter2.Current.Value)) return false;
                        }
                    }

                    return true;
                case DocNodeType.Invalid:
                    return true;
            }

            return false;
        }

        /// combines hierarchies.
        /// lists are concatenated, but dicts are recursively DeepMerged. 
        /// favours rhs on any conflict.
        public static DocNode DeepMerge(DocNode lhs, DocNode rhs) {
            if (lhs.Type != rhs.Type) {
                throw new ArgumentException("can not merge different types " + lhs.Type + " " + rhs.Type);
            }

            switch (lhs.Type) {
                case DocNodeType.List:
                    return Configs.CombineList(new List<DocNode> {lhs, rhs});
                case DocNodeType.Dictionary: {
                    var mergedDict = new ComposedDocNode(DocNodeType.Dictionary,
                        sourceInformation: "Merging of: [" + lhs.SourceInformation + ", " + rhs.SourceInformation + "]");
                    foreach (var lhsPair in lhs.Pairs) {
                        mergedDict[lhsPair.Key] = lhsPair.Value;
                    }

                    foreach (var rhsPair in rhs.Pairs) {
                        if (mergedDict.ContainsKey(rhsPair.Key)) {
                            mergedDict[rhsPair.Key] = DeepMerge(mergedDict[rhsPair.Key], rhsPair.Value);
                        } else {
                            mergedDict[rhsPair.Key] = rhsPair.Value;
                        }
                    }

                    return mergedDict;
                }
                case DocNodeType.Scalar:
                    return rhs;
                default:
                    throw new ArgumentException("can not merge doc nodes of type " + lhs.Type);
            }
        }
    }
}