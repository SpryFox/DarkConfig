using System;
using System.Collections.Generic;
using System.Reflection;

namespace DarkConfig {
    static class ReflectionCache {
        public struct CachedFieldInfo {
            public string strippedName;
            public FieldInfo field;
        }
        
        public static object[] GetCustomAttributes(Type type) {
            object[] attrs;
            if (!classAttributes.TryGetValue(type, out attrs)) {
                attrs = type.GetCustomAttributes(true);
                classAttributes[type] = attrs;
            }
            return attrs;
        }

        public static object[] GetCustomAttributes(FieldInfo fi) {
            object[] attrs;
            if (!fieldAttributes.TryGetValue(fi, out attrs)) {
                attrs = fi.GetCustomAttributes(true);
                fieldAttributes[fi] = attrs;
            }
            return attrs;
        }
        
        public static FieldInfo[] GetAllFields(Type type) {
            FieldInfo[] fields;
            if (!typeFields.TryGetValue(type, out fields)) {
                fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                typeFields[type] = fields;
            }
            return fields;
        }

        public static CachedFieldInfo[] GetStrippedFields(Type type, bool caseInsensitive) {
            CachedFieldInfo[] fields;
            var cache = caseInsensitive ? strippedInsensitiveFields : strippedFields;
            if (!cache.TryGetValue(type, out fields)) {
                var unstrippedFields = GetAllFields(type);
                var legalFields = new List<FieldInfo>(unstrippedFields.Length);
                foreach (var unstrippedField in unstrippedFields) {
                    if (!unstrippedField.IsSpecialName) {
                        legalFields.Add(unstrippedField);
                    }
                }

                fields = new CachedFieldInfo[legalFields.Count];
                for (int i = 0; i < fields.Length; i++) {
                    fields[i] = new CachedFieldInfo {
                        field = legalFields[i],
                        strippedName = StripFieldname(legalFields[i].Name, caseInsensitive)
                    };
                }

                cache[type] = fields;
            }
            return fields;
        }

        public static FieldInfo[] GetInstanceFields(Type type) {
            FieldInfo[] fields;
            if (!typeInstanceFields.TryGetValue(type, out fields)) {
                fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                typeInstanceFields[type] = fields;
            }
            return fields;
        }

        public static MethodInfo GetPostDoc(Type type) {
            MethodInfo postDoc;
            if (!typePostDocs.TryGetValue(type, out postDoc)) {
                postDoc = type.GetMethod("PostDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                typePostDocs[type] = postDoc;
            }
            return postDoc;
        }

        public static MethodInfo GetFromDoc(Type type) {
            MethodInfo fromDoc;
            if (!typeFromDocs.TryGetValue(type, out fromDoc)) {
                fromDoc = type.GetMethod("FromDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                typeFromDocs[type] = fromDoc;
            }
            return fromDoc;
        }


        public static string GetLowercase(string str) {
            string lower;
            if (!lowercased.TryGetValue(str, out lower)) {
                lower = str.ToLower();
                lowercased.Add(str, lower);
            }
            return lower;
        }
        
        ////////////////////////////////////////////
        
        static readonly Dictionary<Type, object[]> classAttributes = new Dictionary<Type, object[]>();
        static readonly Dictionary<FieldInfo, object[]> fieldAttributes = new Dictionary<FieldInfo, object[]>();
        static readonly Dictionary<Type, FieldInfo[]> typeFields = new Dictionary<Type, FieldInfo[]>();
        static readonly Dictionary<Type, CachedFieldInfo[]> strippedFields = new Dictionary<Type, CachedFieldInfo[]>();
        static readonly Dictionary<Type, CachedFieldInfo[]> strippedInsensitiveFields = new Dictionary<Type, CachedFieldInfo[]>();
        static readonly Dictionary<Type, FieldInfo[]> typeInstanceFields = new Dictionary<Type, FieldInfo[]>();
        static readonly Dictionary<Type, MethodInfo> typePostDocs = new Dictionary<Type, MethodInfo>();
        static readonly Dictionary<Type, MethodInfo> typeFromDocs = new Dictionary<Type, MethodInfo>();
        static readonly Dictionary<string, string> lowercased = new Dictionary<string, string>();
        
        ////////////////////////////////////////////

        static string StripFieldname(string name, bool caseInsensitive) {
            var stripped =
                (name.StartsWith("m_", StringComparison.Ordinal) ||
                 name.StartsWith("c_", StringComparison.Ordinal))
                    ? name.Substring(2)
                    : name;
            return caseInsensitive ? stripped.ToLower() : stripped;
        }
    }
}