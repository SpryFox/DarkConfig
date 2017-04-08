using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DarkConfig {

public static class ReflectionCache {
    public static object[] GetCustomAttributes(Type type) {
        object[] attrs;
        if(!classAttributes.TryGetValue(type, out attrs)) {
            attrs = type.GetCustomAttributes(true);
            classAttributes[type] = attrs;
        }
        return attrs;
    }

    static Dictionary<Type, object[]> classAttributes = new Dictionary<Type, object[]>();

    public static object[] GetCustomAttributes(FieldInfo fi) {
        object[] attrs;
        if(!fieldAttributes.TryGetValue(fi, out attrs)) {
            attrs = fi.GetCustomAttributes(true);
            fieldAttributes[fi] = attrs;
        }
        return attrs;
    }

    static Dictionary<FieldInfo, object[]> fieldAttributes = new Dictionary<FieldInfo, object[]>();

    public static FieldInfo[] GetAllFields(Type type) {
        FieldInfo[] fields;
        if(!typeFields.TryGetValue(type, out fields)) {
            fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            typeFields[type] = fields;
        }
        return fields;
    }
    static Dictionary<Type, FieldInfo[]> typeFields = new Dictionary<Type, FieldInfo[]>();

    public struct CachedFieldInfo {
        public string strippedName;
        public FieldInfo field;
    }

    static string StripFieldname(string name, bool caseInsensitive) {
        var stripped = 
            (name.StartsWith("m_", StringComparison.Ordinal) || 
            name.StartsWith("c_",StringComparison.Ordinal))
            ? name.Substring(2)
            : name;
        if(caseInsensitive) {
            return stripped.ToLower();
        } else {
            return stripped;
        }
    }

    public static CachedFieldInfo[] GetStrippedFields(Type type, bool caseInsensitive) {
        CachedFieldInfo[] fields;
        var cache = (caseInsensitive ? strippedInsensitiveFields : strippedFields);
        if(!cache.TryGetValue(type, out fields)) {
            var unstrippedFields = GetAllFields(type);
            var legalFields = new List<FieldInfo>(unstrippedFields.Length);
            foreach(var unstrippedField in unstrippedFields) {
                if(!unstrippedField.IsSpecialName) {
                    legalFields.Add(unstrippedField);
                }
            }

            fields = new CachedFieldInfo[legalFields.Count];
            for(int i = 0; i < fields.Length; i++) {
                fields[i] = new CachedFieldInfo {
                    field = legalFields[i],
                    strippedName = StripFieldname(legalFields[i].Name, caseInsensitive)
                };
            }
            cache[type] = fields;
        }
        return fields;
    }
    static Dictionary<Type, CachedFieldInfo[]> strippedFields = new Dictionary<Type, CachedFieldInfo[]>();
    static Dictionary<Type, CachedFieldInfo[]> strippedInsensitiveFields = new Dictionary<Type, CachedFieldInfo[]>();

    public static FieldInfo[] GetInstanceFields(Type type) {
        FieldInfo[] fields;
        if(!typeInstanceFields.TryGetValue(type, out fields)) {
            fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            typeInstanceFields[type] = fields;
        }
        return fields;
    }
    static Dictionary<Type, FieldInfo[]> typeInstanceFields = new Dictionary<Type, FieldInfo[]>();

    public static MethodInfo GetPostDoc(Type type) {
        MethodInfo postDoc;
        if(!typePostDocs.TryGetValue(type, out postDoc)) {
            postDoc = type.GetMethod("PostDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            typePostDocs[type] = postDoc;
        }
        return postDoc;
    }
    static Dictionary<Type, MethodInfo> typePostDocs = new Dictionary<Type, MethodInfo>();

    public static MethodInfo GetFromDoc(Type type) {
        MethodInfo fromDoc;
        if(!typeFromDocs.TryGetValue(type, out fromDoc)) {
            fromDoc = type.GetMethod("FromDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            typeFromDocs[type] = fromDoc;
        }
        return fromDoc;
    }
    static Dictionary<Type, MethodInfo> typeFromDocs = new Dictionary<Type, MethodInfo>();

    public static string GetLowercase(string str) {
        string lower;
        if(!lowercased.TryGetValue(str, out lower)) {
            lower = str.ToLower();
            lowercased.Add(str, lower);
        }
        return lower;
    }
    static Dictionary<string, string> lowercased = new Dictionary<string, string>();
}

}  // namespace DarkConfig