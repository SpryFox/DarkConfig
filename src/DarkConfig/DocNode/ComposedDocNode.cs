using System;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace DarkConfig {
    /// ComposedDocNode is a mutable DocNode implementation, intended to be used to
    /// help compiling multiple source documents into one meta-document.
    public class ComposedDocNode : DocNode {
        public ComposedDocNode(DocNodeType type, int size = -1, string sourceInformation = null, DocNode sourceDocNode = null) {
            Type = type;
            sourceInfo = sourceInformation ?? sourceDocNode?.SourceInformation;
            SourceFile = sourceDocNode?.SourceFile;
            SourceNode = sourceDocNode?.SourceNode;
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
                case DocNodeType.Invalid:
                default:
                    throw new Exception($"Can't make a ComposedDocNode instance with Type {type} at {sourceInformation}");
            }
        }

        #region DocNode
        public override DocNodeType Type { get; }

        /// access the node as if it was a list
        public override DocNode this[int index] {
            get {
                AssertTypeIs(DocNodeType.List);
                return list[index];
            }
            set {
                AssertTypeIs(DocNodeType.List);
                list[index] = value;
            }
        }

        /// access the node as if it was a Dictionary
        public override DocNode this[string key] {
            get {
                AssertTypeIs(DocNodeType.Dictionary);
                return dictionary[key];
            }
            set {
                AssertTypeIs(DocNodeType.Dictionary);
                dictionary[key] = value;
            }
        }

        public override int Count =>
            Type switch {
                DocNodeType.Dictionary => dictionary.Count,
                DocNodeType.List => list.Count,
                _ => throw new DocNodeAccessException(GenerateAccessExceptionMessage("Countable (Dictionary or List)", Type.ToString()))
            };

        public override bool ContainsKey(string key, bool ignoreCase = false) {
            AssertTypeIs(DocNodeType.Dictionary);
            foreach (string dictKey in dictionary.Keys) {
                if (string.Equals(dictKey, key, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                    return true;
                }
            }
            return false;
        }

        public override bool TryGetValue(string key, bool ignoreCase, out DocNode result) {
            AssertTypeIs(DocNodeType.Dictionary);
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
                AssertTypeIs(DocNodeType.List);
                return list;
            }
        }

        public override IEnumerable<KeyValuePair<string, DocNode>> Pairs {
            get {
                AssertTypeIs(DocNodeType.Dictionary);
                return dictionary;
            }
        }

        public override string StringValue {
            get {
                AssertTypeIs(DocNodeType.Scalar);
                return scalar;
            }
            set {
                AssertTypeIs(DocNodeType.Scalar);
                scalar = value;
            }
        }

        public override string SourceInformation => sourceInfo ?? "ComposedDocNode " + Type;
        public override string SourceFile { get; }
        public override YamlNode SourceNode { get; }

        public override string ToString() {
            return $"ComposedDocNode({Type}, {(Type == DocNodeType.Scalar ? scalar : Count.ToString())})";
        }
        #endregion

        public void Add(DocNode d) {
            AssertTypeIs(DocNodeType.List);
            list.Add(d);
        }

        public void Add(string key, DocNode value) {
            AssertTypeIs(DocNodeType.Dictionary);
            dictionary.Add(key, value);
        }

        public void InsertAt(int index, DocNode value) {
            AssertTypeIs(DocNodeType.List);
            list.Insert(index, value);
        }

        public void Remove(DocNode d) {
            AssertTypeIs(DocNodeType.List);
            list.Remove(d);
        }

        public void RemoveAt(int index) {
            AssertTypeIs(DocNodeType.List);
            list.RemoveAt(index);
        }

        public void RemoveKey(string key) {
            AssertTypeIs(DocNodeType.Dictionary);
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
                    return new ComposedDocNode(doc.Type, sourceDocNode: doc) {
                        StringValue = doc.StringValue
                    };
                }

                case DocNodeType.List: {
                    var newDoc = new ComposedDocNode(doc.Type, doc.Count, sourceDocNode: doc);
                    foreach (var value in doc.Values) {
                        newDoc.Add(recursive ? MakeMutable(value, recursive: true, force: force) : value);
                    }
                    return newDoc;
                }

                case DocNodeType.Dictionary: {
                    var newDoc = new ComposedDocNode(doc.Type, doc.Count, sourceDocNode: doc);
                    foreach ((string key, var value) in doc.Pairs) {
                        newDoc.Add(key, recursive ? MakeMutable(value, recursive: true, force: force) : value);
                    }
                    return newDoc;
                }

                case DocNodeType.Invalid:
                default:
                    throw new ArgumentOutOfRangeException($"Unknown DocNode type {doc.Type} to make ComposedDocNode from at: {doc.SourceInformation}");
            }
        }

        public static ComposedDocNode MakeMutableRef(ref DocNode doc, bool recursive = true) {
            if (doc is ComposedDocNode composedDocNode) {
                return composedDocNode;
            }

            var mutableDoc = MakeMutable(doc, recursive);
            doc = mutableDoc;
            return mutableDoc;
        }

        public static ComposedDocNode DeepClone(DocNode doc) {
            return MakeMutable(doc, recursive: true, force: true);
        }
    }
}
