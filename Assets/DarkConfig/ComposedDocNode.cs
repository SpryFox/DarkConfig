using System.Collections.Generic;

namespace DarkConfig {

    /// <summary>
    /// ComposedDocNode is a mutable DocNode implementation, intended to be used to
    /// help compiling multiple source documents into one meta-document.
    /// </summary>
    public class ComposedDocNode : DocNode {
        //////////////////////////////////////////////////////////////////////
        // DocNode Methods
        //////////////////////////////////////////////////////////////////////
        public ComposedDocNode(DocNodeType type, int size = -1, string sourceInformation = null) {
            m_type = type;
            m_sourceInfo = sourceInformation;
            switch(type) {
                case DocNodeType.Invalid:
                    break;
                case DocNodeType.Dictionary:
                    if(size > 0) m_dictionary = new Dictionary<string, DocNode>(size);
                    else m_dictionary = new Dictionary<string, DocNode>();
                    break;
                case DocNodeType.List:
                    if(size > 0) m_list = new List<DocNode>(size);
                    else m_list = new List<DocNode>();
                    break;
                case DocNodeType.Scalar:
                    m_scalar = "";
                    break;
            }
        }

        public override DocNodeType Type {
            get {
                return m_type;
            }
        }

        void AssertTypeIs(DocNodeType type) {
            if (Type != type) {
                ThrowAccessException(type.ToString(), Type.ToString());
            }
        }

        static System.Text.StringBuilder s_exceptionBuilder = new System.Text.StringBuilder(500);
        void ThrowAccessException(string expectedType, string actualType) {
            s_exceptionBuilder.Length = 0;
            s_exceptionBuilder.Append("Accessing ComposedDocNode as ");
            s_exceptionBuilder.Append(expectedType);
            s_exceptionBuilder.Append(" but is ");
            s_exceptionBuilder.Append(actualType);
            s_exceptionBuilder.Append(". ");
            throw new DocNodeAccessException(s_exceptionBuilder.ToString());
        }

        // access the node as if it was a list
        public override DocNode this[int index] {
            get {
                AssertTypeIs(DocNodeType.List);
                return m_list[index];
            }
            set {
                throw new System.NotImplementedException();
            }
        }

        // access the node as if it was a Dictionary
        public override DocNode this[string key] {
            get {
                AssertTypeIs(DocNodeType.Dictionary);
                return m_dictionary[key];
            }
            set {
                AssertTypeIs(DocNodeType.Dictionary);
                m_dictionary[key] = value;
            }
        }

        public override int Count {
            get {
                if (Type != DocNodeType.Dictionary && Type != DocNodeType.List) {
                    ThrowAccessException("Countable (Dictionary or List)", Type.ToString());
                }
                if (Type == DocNodeType.Dictionary) {
                    return m_dictionary.Count;
                }
                if (Type == DocNodeType.List) {
                    return m_list.Count;
                }
                throw new System.NotImplementedException();
            }
        }

        public override bool ContainsKey(string key) {
            AssertTypeIs(DocNodeType.Dictionary);
            return m_dictionary.ContainsKey(key);
        }

        public override IEnumerable<DocNode> Values {
            get {
                AssertTypeIs(DocNodeType.List);
                return m_list;
            }
        }

        public override IEnumerable<KeyValuePair<string, DocNode>> Pairs {
            get {
                AssertTypeIs(DocNodeType.Dictionary);
                return m_dictionary;
            }
        }

        public override string StringValue {
            get {
                AssertTypeIs(DocNodeType.Scalar);
                return m_scalar;
            }
            set {
                AssertTypeIs(DocNodeType.Scalar);
                m_scalar = value;
            }
        }

        public override string SourceInformation {
            get {
                if(m_sourceInfo != null) return m_sourceInfo;
                return "ComposedDocNode " + Type;
            }
        }

        public override string ToString() {
            return string.Format("ComposedDocNode({0}, {1})", Type, 
                (Type == DocNodeType.Scalar ? m_scalar : Count.ToString()));
        }

        //////////////////////////////////////////////////////////////////////
        // Composition methods

        public void Add(DocNode d) {
            AssertTypeIs(DocNodeType.List);
            m_list.Add(d);
        }

        public void Add(string key, DocNode value) {
            AssertTypeIs(DocNodeType.Dictionary);
            m_dictionary.Add(key, value);
        }

        ////////////////////////////////////////////
        DocNodeType m_type;
        Dictionary<string, DocNode> m_dictionary;
        List<DocNode> m_list;
        string m_scalar;

        string m_sourceInfo;
    }

}