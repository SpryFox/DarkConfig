using System.Collections;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace DarkConfig {

    /// <summary>
    /// YamlDocNode is a node from a parsed YAML document.
    /// </summary>
    public class YamlDocNode : DocNode {
        public YamlDocNode(YamlNode node) {
            m_node = node;
        }

        public override DocNodeType Type {
            get {
                if (m_node == null) {
                    return DocNodeType.Invalid;
                }
                if (m_node is YamlMappingNode) {
                    return DocNodeType.Dictionary;
                }
                if (m_node is YamlSequenceNode) {
                    return DocNodeType.List;
                }
                if (m_node is YamlScalarNode) {
                    return DocNodeType.Scalar;
                }
                return DocNodeType.Invalid;
            }
        }

        public void AssertTypeIs(DocNodeType type) {
            if (Type != type) {
                ThrowAccessException(type.ToString(), Type.ToString());
            }
        }

        static System.Text.StringBuilder s_exceptionBuilder = new System.Text.StringBuilder(500);
        void ThrowAccessException(string expectedType, string actualType) {
            s_exceptionBuilder.Length = 0;
            s_exceptionBuilder.Append("Accessing YamlDocNode as ");
            s_exceptionBuilder.Append(expectedType);
            s_exceptionBuilder.Append(" but is ");
            s_exceptionBuilder.Append(actualType);
            s_exceptionBuilder.Append(". ");
            s_exceptionBuilder.Append(SourceInformation);
            throw new DocNodeAccessException(s_exceptionBuilder.ToString());
        }

        static System.Text.StringBuilder s_sourceBuilder = new System.Text.StringBuilder(500);
        public override string SourceInformation {
            get {
                s_sourceBuilder.Length = 0;
                s_sourceBuilder.Append(m_node.Start.ToString());
                // NOTE: the End never appears to differ from the Start, so let's just not even show it
                //s_sourceBuilder.Append(" - ");
                //s_sourceBuilder.Append(m_node.End.ToString());
                return s_sourceBuilder.ToString();
            }
        }

        // access the node as if it was a list
        public override DocNode this[int index] {
            get {
                AssertTypeIs(DocNodeType.List);
                var seqNode = (YamlSequenceNode)m_node;
                return new YamlDocNode(seqNode.Children[index]);
            }
            set {
                throw new System.NotSupportedException();
            }
        }

        // access the node as if it was a Dictionary
        public override DocNode this[string key] {
            get {
                AssertTypeIs(DocNodeType.Dictionary);
                var mapNode = (YamlMappingNode)m_node;
                var scalarAccessor = new YamlScalarNode(key);
                return new YamlDocNode(mapNode.Children[scalarAccessor]);
            }
            set {
                throw new System.NotSupportedException();
            }
        }

        public override int Count {
            get {
                if (Type != DocNodeType.Dictionary && Type != DocNodeType.List) {
                    ThrowAccessException("Countable (Dictionary or List)", Type.ToString());
                }
                if (Type == DocNodeType.Dictionary) {
                    return ((YamlMappingNode)m_node).Children.Count;
                }
                if (Type == DocNodeType.List) {
                    return ((YamlSequenceNode)m_node).Children.Count;
                }
                throw new System.NotImplementedException();
            }
        }

        public override bool ContainsKey(string key) {
            AssertTypeIs(DocNodeType.Dictionary);
            return ((YamlMappingNode)m_node).Children.ContainsKey(new YamlScalarNode(key));
        }

        public struct ValuesIterator : IEnumerable<DocNode> {
            internal ValuesIterator(YamlNode node) {
                m_node = node;
            }
            public IEnumerator<DocNode> GetEnumerator() {
                var container = ((YamlSequenceNode)m_node).Children;
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
                return new ValuesIterator(m_node);
            }
        }

        public struct PairsIterator : IEnumerable<KeyValuePair<string, DocNode>> {
            internal PairsIterator(YamlNode node) {
                m_node = node;
            }
            public IEnumerator<KeyValuePair<string, DocNode>> GetEnumerator() {
                var container = ((YamlMappingNode)m_node).Children;
                foreach (KeyValuePair<YamlNode, YamlNode> entry in container) {
                    string k = ((YamlScalarNode)entry.Key).Value;
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
                return new PairsIterator(m_node);
            }
        }

        public override string StringValue {
            get {
                AssertTypeIs(DocNodeType.Scalar);
                return ((YamlScalarNode)m_node).Value;
            }
            set {
                throw new System.NotSupportedException();
            }
        }

        ////////////////////////////////////////////
        YamlNode m_node;
    }
}