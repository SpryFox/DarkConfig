using System.Collections.Generic;

namespace DarkConfig {
    /// <summary>
    /// Extension methods for DocNode implementations that make accessing their contents 
    /// more convenient.
    /// </summary>
    public static class DocNodeExtensions {
        public static int AsInt(this DocNode doc) {
            return As<int>(doc);
        }

        public static float AsFloat(this DocNode doc) {
            return As<float>(doc);
        }

        public static string AsString(this DocNode doc) {
            return doc.StringValue;
        }

        public static bool AsBool(this DocNode doc) {
            return As<bool>(doc);
        }

        public static T As<T>(this DocNode doc) {
            T retval = default(T);
            ConfigReifier.Reify<T>(ref retval, doc);
            return retval;
        }

        public static bool Contains(this DocNode doc, string item) {
            if (doc.Type != DocNodeType.List) {
                throw new DocNodeAccessException("Expected List, is " + doc.Type);
            }
            for(int i = 0; i < doc.Count; i++) {
                if(doc[i].StringValue == item) {
                    return true;
                }
            }
            return false;
        }
    }
}