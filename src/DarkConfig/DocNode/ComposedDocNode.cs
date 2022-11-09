using System;
using System.Collections.Generic;

namespace DarkConfig {
    /// ComposedDocNode is a mutable DocNode implementation, intended to be used to
    /// help compiling multiple source documents into one meta-document.
    public class ComposedDocNode : DocNode {
        public ComposedDocNode(DocNodeType type, int size = -1, string sourceInformation = null) {
            Type = type;
            sourceInfo = sourceInformation;
            switch (type) {
                case DocNodeType.Dictionary:
                    dictionary = size > 0 ? new Dictionary<string, DocNode>(size) : new Dictionary<string, DocNode>();
                    break;
                case DocNodeType.List:
                    list = size > 0 ? new List<DocNode>(size) : new List<DocNode>();
                    break;
                case DocNodeType.Scalar:
                    scalar = "";
                    break;
                default:
                    throw new Exception($"Can't make a ComposedDocNode instance with Type {type} at {sourceInformation}");
            }
        }

        #region DocNode Methods 
        public override DocNodeType Type { get; }

        /// access the node as if it was a list
        public override DocNode this[int index] {
            get {
                CheckTypeIs(DocNodeType.List);
                return list[index];
            }
            set {
                CheckTypeIs(DocNodeType.List);
                list[index] = value;
            }
        }

        /// access the node as if it was a Dictionary
        public override DocNode this[string key] {
            get {
                CheckTypeIs(DocNodeType.Dictionary);
                return dictionary[key];
            }
            set {
                CheckTypeIs(DocNodeType.Dictionary);
                dictionary[key] = value;
            }
        }

        public override int Count {
            get {
                switch (Type) {
                    case DocNodeType.Dictionary: return dictionary.Count;
                    case DocNodeType.List: return list.Count;
                    default:
                        throw new DocNodeAccessException(GenerateAccessExceptionMessage("Countable (Dictionary or List)"));
                }
            }
        }

        public override bool ContainsKey(string key, bool ignoreCase = false) {
            CheckTypeIs(DocNodeType.Dictionary);
            foreach (string dictKey in dictionary.Keys) {
                if (string.Equals(dictKey, key, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                    return true;
                }
            }
            return false;
        }

        public override bool TryGetValue(string key, bool ignoreCase, out DocNode result) {
            CheckTypeIs(DocNodeType.Dictionary);
            foreach (var kvp in dictionary) {
                if (string.Equals(kvp.Key, key, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                    result = kvp.Value;
                    return true;
                }
            }

            result = null;
            return false;
        }

        public override IEnumerable<DocNode> Values {
            get {
                CheckTypeIs(DocNodeType.List);
                return list;
            }
        }

        public override IEnumerable<KeyValuePair<string, DocNode>> Pairs {
            get {
                CheckTypeIs(DocNodeType.Dictionary);
                return dictionary;
            }
        }

        public override string StringValue {
            get {
                CheckTypeIs(DocNodeType.Scalar);
                return scalar;
            }
            set {
                CheckTypeIs(DocNodeType.Scalar);
                scalar = value;
            }
        }

        public override string SourceInformation => sourceInfo ?? "ComposedDocNode " + Type;

        public override string ToString() {
            return $"ComposedDocNode({Type}, {(Type == DocNodeType.Scalar ? scalar : Count.ToString())})";
        }
        #endregion

        public void Add(DocNode d) {
            CheckTypeIs(DocNodeType.List);
            list.Add(d);
        }

        public void Add(string key, DocNode value) {
            CheckTypeIs(DocNodeType.Dictionary);
            dictionary.Add(key, value);
        }

        public void Remove(DocNode d) {
            CheckTypeIs(DocNodeType.List);
            list.Remove(d);
        }

        public void RemoveAt(int index) {
            CheckTypeIs(DocNodeType.List);
            list.RemoveAt(index);
        }

        public void RemoveKey(string key) {
            CheckTypeIs(DocNodeType.Dictionary);
            dictionary.Remove(key);
        }

        /////////////////////////////////////////////////

        readonly string sourceInfo;
        
        readonly Dictionary<string, DocNode> dictionary;
        readonly List<DocNode> list;
        string scalar;

        /////////////////////////////////////////////////

        public static ComposedDocNode MakeMutable(DocNode doc, bool recursive = true, bool force = false) {
            if (!force && doc is ComposedDocNode cdn) {
                return cdn;
            }

            switch (doc.Type) {
                case DocNodeType.Scalar: {
                    var newDoc = new ComposedDocNode(doc.Type, -1, doc.SourceInformation);
                    newDoc.StringValue = doc.StringValue;
                    return newDoc;
                }

                case DocNodeType.List: {
                    var newDoc = new ComposedDocNode(doc.Type, doc.Count, doc.SourceInformation);
                    foreach (var elem in doc.Values) {
                        var newElem = recursive ? MakeMutable(elem, recursive: recursive, force: force) : elem;
                        newDoc.Add(newElem);
                    }
                    return newDoc;
                }

                case DocNodeType.Dictionary: {
                    var newDoc = new ComposedDocNode(doc.Type, doc.Count, doc.SourceInformation);
                    foreach (var kv in doc.Pairs) {
                        var newValue = recursive ? MakeMutable(kv.Value, recursive: recursive, force: force) : kv.Value;
                        newDoc.Add(kv.Key, newValue);
                    }
                    return newDoc;
                }

                default:
                    throw new System.NotImplementedException($"Unknown DocNode type {doc.Type} to make ComposedDocNode from at: {doc.SourceInformation}");
            }
        }

        public static ComposedDocNode MakeMutableRef(ref DocNode doc, bool recursive = true) {
            if (doc is ComposedDocNode cdn) {
                return cdn;
            }

            var mutableDoc = MakeMutable(doc, recursive);
            doc = mutableDoc;
            return mutableDoc;
        }

        public static ComposedDocNode DeepClone(DocNode doc) {
            return MakeMutable(doc, recursive: true, force: true);
        }

        /////////////////////////////////////////////////

        void CheckTypeIs(DocNodeType requiredType) {
            if (Type != requiredType) {
                throw new DocNodeAccessException(GenerateAccessExceptionMessage(requiredType.ToString()));
            }
        }
        
        string GenerateAccessExceptionMessage(string expectedType) {
            return string.Concat("Accessing ComposedDocNode as ", expectedType, " but is ", Type.ToString(), ". ");
        }
    }
}
