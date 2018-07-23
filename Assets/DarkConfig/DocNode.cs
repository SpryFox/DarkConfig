using System;
using System.Collections.Generic;

namespace DarkConfig {

    public enum DocNodeType {
        Invalid,
        Dictionary,
        List,
        Scalar
    }

    /// <summary>
    /// DocNode represents a node of a parsed document. 
    /// DocNode is a union type, requiring no casting but behaving differently 
    /// depending on the underlying value.
    /// DocNode also assumes that all Dictionaries have strings as keys.
    /// </summary>
    public abstract class DocNode : IEquatable<DocNode> {
        /// <summary>
        /// Shape of data contained in the node.
        /// </summary>
        public abstract DocNodeType Type { get; }

        /// <summary>
        /// Access the node as if it was a List.
        /// </summary>
        public abstract DocNode this[int index] { get; set; }

        /// <summary>
        /// Access the node as if it was a Dictionary.
        /// </summary>
        public abstract DocNode this[string key] { get; set; }

        /// <summary>
        /// Number of items in the collection
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// Value of scalar as a string.
        /// </summary>
        public abstract string StringValue { get; set; }

        /// <summary>
        /// Returns true if the key is in the dictionary
        /// </summary>
        public abstract bool ContainsKey(string key);

        /// <summary>
        /// Iterates over the values of the list
        /// </summary>
        public abstract IEnumerable<DocNode> Values { get; }

        /// <summary>
        /// Iterates over a dictionary 
        /// </summary>
        public abstract IEnumerable<KeyValuePair<string, DocNode>> Pairs { get; }

        /// <summary>
        /// String describing the position and context in the source format (e.g. line number).
        /// </summary>
        public abstract string SourceInformation { get; }

        public bool Equals(DocNode d) {
            var self = this;
            if(d.Type != self.Type) return false;
            if(object.Equals(self, d)) return true;
            switch(self.Type){
                case DocNodeType.Scalar:
                    if(d.StringValue == null || self.StringValue == null) return self.StringValue == d.StringValue;
                    return d.StringValue.Equals(self.StringValue);
                case DocNodeType.List:
                    if(d.Count != self.Count) return false;
                    for(int i = 0; i < self.Count; i++) {
                        if(!self[i].Equals(d[i])) {
                            return false;
                        }
                    }
                    return true;
                case DocNodeType.Dictionary:
                    if(d.Count != self.Count) return false;
                    var iter1 = self.Pairs.GetEnumerator();
                    var iter2 = d.Pairs.GetEnumerator();
                    while(iter1.MoveNext() && iter2.MoveNext()) {
                        if(iter1.Current.Key != iter2.Current.Key) return false;
                        if(!iter1.Current.Value.Equals(iter2.Current.Value)) return false;
                    }
                    return true;
                case DocNodeType.Invalid:
                    return true;
            }
            return false;
        }

        // combines hierarchies.
        // lists are concatenated, but dicts are recursively DeepMerged. 
        // favours second node on any conflict.
        public static DocNode DeepMerge(DocNode lhs, DocNode rhs) {
            if (lhs.Type != rhs.Type) {
                throw new ArgumentException("can not merge different types " + lhs.Type + " " + rhs.Type);
            }

            if (lhs.Type == DocNodeType.List) {
                return Config.CombineList(new List<DocNode>{ lhs, rhs });
                
            } else if (lhs.Type == DocNodeType.Dictionary) {
                var mergedDict = new ComposedDocNode(DocNodeType.Dictionary, 
                    sourceInformation: "Merging of: [" + lhs.SourceInformation + ", " + rhs.SourceInformation + "]");
                foreach(var lhsPair in lhs.Pairs) {
                    mergedDict[lhsPair.Key] = lhsPair.Value;
                }
                foreach(var rhsPair in rhs.Pairs) {
                    if (mergedDict.ContainsKey(rhsPair.Key)) {
                        mergedDict[rhsPair.Key] = DeepMerge(mergedDict[rhsPair.Key], rhsPair.Value);
                    } else {
                        mergedDict[rhsPair.Key] = rhsPair.Value;
                    }
                }
                return mergedDict;
                
            } else if (lhs.Type == DocNodeType.Scalar) {
                return rhs;
            } else {
                throw new ArgumentException("can not merge doc nodes of type " + lhs.Type);
            }
        }
    }

    public class DocNodeAccessException : System.Exception {
        public DocNodeAccessException(string message)
            : base(message) {
        }
    }

}
