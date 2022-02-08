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
        /// <param name="filename">Used for error reporting</param>
        public YamlDocNode(YamlNode node, string filename) {
            this.node = node;
            this.filename = filename;
        }

        public override DocNodeType Type {
            get {
                switch (node) {
                    case null:
                        return DocNodeType.Invalid;
                    case YamlMappingNode _:
                        return DocNodeType.Dictionary;
                    case YamlSequenceNode _:
                        return DocNodeType.List;
                    case YamlScalarNode _:
                        return DocNodeType.Scalar;
                    default:
                        return DocNodeType.Invalid;
                }
            }
        }

        public override string SourceInformation => $"File: {filename}, {node.Start}";

        /// <summary>
        /// Access the node as if it was a list
        /// </summary>
        /// <param name="index"></param>
        /// <exception cref="NotSupportedException"></exception>
        public override DocNode this[int index] {
            get {
                AssertTypeIs(DocNodeType.List);
                var seqNode = (YamlSequenceNode) node;
                return new YamlDocNode(seqNode.Children[index], filename);
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
                var mapNode = (YamlMappingNode) node;
                var scalarAccessor = new YamlScalarNode(key);
                return new YamlDocNode(mapNode.Children[scalarAccessor], filename);
            }
            set => throw new NotSupportedException();
        }

        public override int Count {
            get {
                switch (Type) {
                    case DocNodeType.Dictionary:
                        return ((YamlMappingNode)node).Children.Count;
                    case DocNodeType.List:
                        return ((YamlSequenceNode)node).Children.Count;
                    case DocNodeType.Invalid:
                    case DocNodeType.Scalar:
                    default:
                        throw new DocNodeAccessException(GenerateAccessExceptionMessage("Countable (Dictionary or List)", Type.ToString()));
                }
            }
        }

        public override bool ContainsKey(string key, bool ignoreCase = false) {
            AssertTypeIs(DocNodeType.Dictionary);
            
            var children = ((YamlMappingNode)node).Children;
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
            
            var children = ((YamlMappingNode)node).Children;
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal; 
            foreach (var kvp in children) {
                if (kvp.Key is YamlScalarNode scalarKey && string.Equals(scalarKey.Value, key, comparison)) {
                    result = new YamlDocNode(kvp.Value, filename);
                    return true;
                }
            }

            result = null;
            return false;
            // return ((YamlMappingNode) node).Children.ContainsKey(new YamlScalarNode(key));
        }

        readonly struct ValuesIterator : IEnumerable<DocNode> {
            internal ValuesIterator(YamlNode node, string filename) {
                this.node = node;
                this.filename = filename;
            }

            public IEnumerator<DocNode> GetEnumerator() {
                var container = ((YamlSequenceNode) node).Children;
                foreach (YamlNode entry in container) {
                    yield return new YamlDocNode(entry, filename);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return this.GetEnumerator();
            }

            readonly YamlNode node;
            readonly string filename;
        }

        public override IEnumerable<DocNode> Values {
            get {
                AssertTypeIs(DocNodeType.List);
                return new ValuesIterator(node, filename);
            }
        }

        readonly struct PairsIterator : IEnumerable<KeyValuePair<string, DocNode>> {
            internal PairsIterator(YamlNode node, string filename) {
                this.node = node;
                this.filename = filename;
            }

            public IEnumerator<KeyValuePair<string, DocNode>> GetEnumerator() {
                var container = ((YamlMappingNode) node).Children;
                foreach (KeyValuePair<YamlNode, YamlNode> entry in container) {
                    string k = ((YamlScalarNode) entry.Key).Value;
                    YamlDocNode v = new YamlDocNode(entry.Value, filename);
                    yield return new KeyValuePair<string, DocNode>(k, v);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            readonly YamlNode node;
            readonly string filename;
        }

        public override IEnumerable<KeyValuePair<string, DocNode>> Pairs {
            get {
                AssertTypeIs(DocNodeType.Dictionary);
                return new PairsIterator(node, filename);
            }
        }

        public override string StringValue {
            get {
                AssertTypeIs(DocNodeType.Scalar);
                return ((YamlScalarNode) node).Value;
            }
            set => throw new NotSupportedException();
        }

        ////////////////////////////////////////////
        
        readonly YamlNode node;
        readonly string filename;

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