using System;
using System.Collections;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace DarkConfig {
    /// YamlDocNode is a node from a parsed YAML document.
    public class YamlDocNode : DocNode {
        /// <summary>
        /// </summary>
        /// <param name="node">The YAMLDotNet node</param>
        /// <param name="sourceFile">Used for error reporting</param>
        public YamlDocNode(YamlNode node, string sourceFile) {
            SourceNode = node;
            SourceFile = sourceFile;
        }

        public override DocNodeType Type {
            get {
                return SourceNode switch {
                    null => DocNodeType.Invalid,
                    YamlMappingNode _ => DocNodeType.Dictionary,
                    YamlSequenceNode _ => DocNodeType.List,
                    YamlScalarNode _ => DocNodeType.Scalar,
                    _ => DocNodeType.Invalid
                };
            }
        }

        public override string SourceInformation => $"File: {SourceFile}, Line: {SourceNode.Start.Line}, Col: {SourceNode.Start.Column}";

        /// <summary>
        /// Access the node as if it was a list
        /// </summary>
        /// <param name="index"></param>
        /// <exception cref="NotSupportedException"></exception>
        public override DocNode this[int index] {
            get {
                AssertTypeIs(DocNodeType.List);
                var seqNode = (YamlSequenceNode) SourceNode;
                return new YamlDocNode(seqNode.Children[index], SourceFile);
            }
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Access the node as if it was a Dictionary
        /// </summary>
        /// <param name="key"></param>
        /// <exception cref="NotSupportedException"></exception>
        public override DocNode this[string key] {
            get {
                AssertTypeIs(DocNodeType.Dictionary);
                var mapNode = (YamlMappingNode) SourceNode;
                var scalarAccessor = new YamlScalarNode(key);
                return new YamlDocNode(mapNode.Children[scalarAccessor], SourceFile);
            }
            set => throw new NotSupportedException();
        }

        public override int Count {
            get {
                return Type switch {
                    DocNodeType.Dictionary => ((YamlMappingNode) SourceNode).Children.Count,
                    DocNodeType.List => ((YamlSequenceNode) SourceNode).Children.Count,
                    _ => throw new DocNodeAccessException(GenerateAccessExceptionMessage("Countable (Dictionary or List)", Type.ToString()))
                };
            }
        }

        public override bool ContainsKey(string key, bool ignoreCase = false) {
            AssertTypeIs(DocNodeType.Dictionary);

            var children = ((YamlMappingNode) SourceNode).Children;
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var yamlKey in children.Keys) {
                if (yamlKey is YamlScalarNode scalarKey && string.Equals(scalarKey.Value, key, comparison)) {
                    return true;
                }
            }

            return false;
        }

        public override bool TryGetValue(string key, bool ignoreCase, out DocNode result) {
            AssertTypeIs(DocNodeType.Dictionary);

            var children = ((YamlMappingNode) SourceNode).Children;
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var kvp in children) {
                if (kvp.Key is YamlScalarNode scalarKey && string.Equals(scalarKey.Value, key, comparison)) {
                    result = new YamlDocNode(kvp.Value, SourceFile);
                    return true;
                }
            }

            result = null;
            return false;
        }

        readonly struct ValuesIterator : IEnumerable<DocNode> {
            internal ValuesIterator(YamlNode node, string filename) {
                this.node = node;
                this.filename = filename;
            }

            public IEnumerator<DocNode> GetEnumerator() {
                foreach (var entry in ((YamlSequenceNode) node).Children) {
                    yield return new YamlDocNode(entry, filename);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            readonly YamlNode node;
            readonly string filename;
        }

        public override IEnumerable<DocNode> Values {
            get {
                AssertTypeIs(DocNodeType.List);
                return new ValuesIterator(SourceNode, SourceFile);
            }
        }

        readonly struct PairsIterator : IEnumerable<KeyValuePair<string, DocNode>> {
            internal PairsIterator(YamlNode node, string filename) {
                this.node = node;
                this.filename = filename;
            }

            public IEnumerator<KeyValuePair<string, DocNode>> GetEnumerator() {
                foreach (var entry in ((YamlMappingNode) node).Children) {
                    yield return new KeyValuePair<string, DocNode>(
                        ((YamlScalarNode) entry.Key).Value,
                        new YamlDocNode(entry.Value, filename));
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            readonly YamlNode node;
            readonly string filename;
        }

        public override IEnumerable<KeyValuePair<string, DocNode>> Pairs {
            get {
                AssertTypeIs(DocNodeType.Dictionary);
                return new PairsIterator(SourceNode, SourceFile);
            }
        }

        public override string StringValue {
            get {
                AssertTypeIs(DocNodeType.Scalar);
                return ((YamlScalarNode) SourceNode).Value;
            }
            set => throw new NotSupportedException();
        }

        ////////////////////////////////////////////

        public override YamlNode SourceNode  { get; }
        public override string SourceFile { get; }

        ////////////////////////////////////////////

        void AssertTypeIs(DocNodeType type) {
            if (Type != type) {
                throw new DocNodeAccessException(GenerateAccessExceptionMessage(type.ToString(), Type.ToString()));
            }
        }

        string GenerateAccessExceptionMessage(string expectedType, string actualType) {
            return $"Accessing YamlDocNode as {expectedType} but is {actualType}. {SourceInformation}";
        }
    }
}
