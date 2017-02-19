using System;
using System.Text;
using System.Collections.Generic;

namespace DarkConfig {
    public class DocPath : IEquatable<DocPath> {
        string CurrentSegment;

        DocPath Parent;

        public DocPath(string segment) {
            CurrentSegment = segment;
        }

        public DocPath(string segment, DocPath parent) {
            CurrentSegment = segment;
            Parent = parent;
        }

        public DocPath(string segment, string parent) {
            CurrentSegment = segment;
            Parent = new DocPath(parent);
        }

        public bool Equals(DocPath p) {
            if(object.ReferenceEquals(this, p)) return true;
            if(p == null) return this == null;
            if(CurrentSegment != p.CurrentSegment) return false;
            if(Parent == null) return p.Parent == null;
            return Parent.Equals(p.Parent);
        }

        public override int GetHashCode() {
            var h = CurrentSegment.GetHashCode();
            var h2 =  Parent != null ? Parent.GetHashCode() : 0;
            return unchecked(((h << 5) + h) ^ h2);
        }

        public override string ToString() {
            var tmpList = new List<DocPath>();
            ToList(tmpList);
            var sb = new StringBuilder();
            for(int i = 0; i < tmpList.Count; i++) {
                if(i != 0) {
                    sb.Append(".");
                }
                sb.Append(tmpList[i].CurrentSegment);
            }
            return sb.ToString();
        }

        public void ToList(List<DocPath> result) {
            if(Parent == null) {
                result.Add(this);
            } else {
                Parent.ToList(result);
                result.Add(this);
            }
        }
    }
}