using System;
using System.Collections.Generic;
using System.Reflection;

namespace DarkConfig.Internal {
    static class ReflectionCache {
        internal class PropertyMetadata {
            public string ShortName;
            public MemberInfo Info;
            public Type Type;
            public bool IsField;
            public bool HasConfigMandatoryAttribute;
            public bool HasConfigAllowMissingAttribute;
            public bool HasConfigIgnoreAttribute;
        }
        
        internal class ClassAttributeFlags {
            public bool HasConfigMandatoryAttribute;
            public bool HasConfigAllowMissingAttribute;
        }
        
        internal static ClassAttributeFlags GetClassAttributeFlags(Type type) {
            ClassAttributeFlags flags;
            if (!classAttributes.TryGetValue(type, out flags)) {
                flags = new ClassAttributeFlags();
                foreach (var attribute in type.GetCustomAttributes(true)) {
                    switch (attribute) {
                        case ConfigMandatoryAttribute _: flags.HasConfigMandatoryAttribute = true; break;
                        case ConfigAllowMissingAttribute _: flags.HasConfigAllowMissingAttribute = true; break;
                    }
                }
                Platform.Assert(!flags.HasConfigMandatoryAttribute || !flags.HasConfigAllowMissingAttribute, 
                    "Type", type.Name, "has both ConfigAllowMissing and ConfigMandatory attributes.");
                classAttributes.Add(type, flags);
            }
            return flags;
        }

        internal static MethodInfo GetPostDocMethod(Type type) {
            MethodInfo postDoc;
            if (!typePostDocs.TryGetValue(type, out postDoc)) {
                postDoc = type.GetMethod("PostDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                typePostDocs.Add(type, postDoc);
            }
            return postDoc;
        }

        internal static MethodInfo GetFromDocMethod(Type type) {
            MethodInfo fromDoc;
            if (!typeFromDocs.TryGetValue(type, out fromDoc)) {
                fromDoc = type.GetMethod("FromDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                typeFromDocs.Add(type, fromDoc);
            }
            return fromDoc;
        }
        
        /// Get property and field info about the type
        internal static List<PropertyMetadata> GetTypeMemberMetadata(Type type) {
            List<PropertyMetadata> props;
            if (!typeProperties.TryGetValue(type, out props)) {
                props = new List<PropertyMetadata>();
                var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
                
                foreach (var propertyInfo in type.GetProperties(flags)) {
                    if (!propertyInfo.IsSpecialName && propertyInfo.CanWrite && propertyInfo.CanRead) {
                        var metadata = new PropertyMetadata {
                            Info = propertyInfo,
                            ShortName = RemoveHungarianPrefix(propertyInfo.Name),
                            IsField = false,
                            Type = propertyInfo.PropertyType
                        };
                        SetMemberAttributeFlags(metadata);
                        props.Add(metadata);
                    }
                }

                foreach (var fieldInfo in type.GetFields(flags)) {
                    // Compiler-generated property backing fields have the name "<propertyName>k_BackingField" so 
                    // ignore any fields with names that start with '<'.  Apparently IsSpecialName doesn't cover
                    // this case.
                    if (!fieldInfo.IsSpecialName && fieldInfo.Name[0] != '<') {
                        var metadata = new PropertyMetadata {
                            Info = fieldInfo,
                            ShortName = RemoveHungarianPrefix(fieldInfo.Name),
                            IsField = true,
                            Type = fieldInfo.FieldType
                        };
                        SetMemberAttributeFlags(metadata);
                        props.Add(metadata);
                    }
                }

                typeProperties[type] = props;
            }

            return props;
        }
        
        ////////////////////////////////////////////
        
        
        static readonly Dictionary<Type, List<PropertyMetadata>> typeProperties = new Dictionary<Type, List<PropertyMetadata>>();
        static readonly Dictionary<Type, ClassAttributeFlags> classAttributes = new Dictionary<Type, ClassAttributeFlags>();
        static readonly Dictionary<Type, MethodInfo> typePostDocs = new Dictionary<Type, MethodInfo>();
        static readonly Dictionary<Type, MethodInfo> typeFromDocs = new Dictionary<Type, MethodInfo>();
        
        ////////////////////////////////////////////

        /// Removes one letter hungarian notation prefixes from field names.
        static string RemoveHungarianPrefix(string name) {
            return name.Length > 1 && name[1] == '_' ? name.Substring(2) : name;
        }

        static void SetMemberAttributeFlags(PropertyMetadata metadata) {
            foreach (var attribute in metadata.Info.GetCustomAttributes(true)) {
                if (attribute is ConfigMandatoryAttribute) {
                    metadata.HasConfigMandatoryAttribute = true;
                } else if (attribute is ConfigAllowMissingAttribute) {
                    metadata.HasConfigAllowMissingAttribute = true;
                } else if (attribute is ConfigIgnoreAttribute) {
                    metadata.HasConfigIgnoreAttribute = true;
                }
            }
        }
    }
}