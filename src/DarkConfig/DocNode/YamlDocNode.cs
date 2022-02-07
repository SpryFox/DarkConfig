using System;
using System.Collections;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace DarkConfig {
    /// YamlDocNode is a node from a parsed YAML document.
    public class YamlDocNode : DocNode {
        public YamlDocNode(YamlNode node) {
            this.node = node;
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

        public override string SourceInformation => node.Start.ToString();

        // access the node as if it was a list
        public override DocNode this[int index] {
            get {
                AssertTypeIs(DocNodeType.List);
                var seqNode = (YamlSequenceNode) node;
                return new YamlDocNode(seqNode.Children[index]);
            }
            set => throw new NotSupportedException();
        }

        // access the node as if it was a Dictionary
        public override DocNode this[string key] {
            get {
                AssertTypeIs(DocNodeType.Dictionary);
                var mapNode = (YamlMappingNode) node;
                var scalarAccessor = new YamlScalarNode(key);
                return new YamlDocNode(mapNode.Children[scalarAccessor]);
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
                    result = new YamlDocNode(kvp.Value);
                    return true;
                }
            }

            result = null;
            return false;
            // return ((YamlMappingNode) node).Children.ContainsKey(new YamlScalarNode(key));
        }

        public struct ValuesIterator : IEnumerable<DocNode> {
            internal ValuesIterator(YamlNode node) {
                m_node = node;
            }

            public IEnumerator<DocNode> GetEnumerator() {
                var container = ((YamlSequenceNode) m_node).Children;
                foreach (YamlNode entry in container) {
                    yield return new YamlDocNode(entry);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return this.GetEnumerator();
            }

            YamlNode m_node;
        }

        public override IEnumerable<DocNode> Values {
            get {
                AssertTypeIs(DocNodeType.List);
                return new ValuesIterator(node);
            }
        }

        public struct PairsIterator : IEnumerable<KeyValuePair<string, DocNode>> {
            internal PairsIterator(YamlNode node) {
                m_node = node;
            }

            public IEnumerator<KeyValuePair<string, DocNode>> GetEnumerator() {
                var container = ((YamlMappingNode) m_node).Children;
                foreach (KeyValuePair<YamlNode, YamlNode> entry in container) {
                    string k = ((YamlScalarNode) entry.Key).Value;
                    YamlDocNode v = new YamlDocNode(entry.Value);
                    yield return new KeyValuePair<string, DocNode>(k, v);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return this.GetEnumerator();
            }

            YamlNode m_node;
        }

        public override IEnumerable<KeyValuePair<string, DocNode>> Pairs {
            get {
                AssertTypeIs(DocNodeType.Dictionary);
                return new PairsIterator(node);
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