using System;
using System.Collections.Generic;
using System.Reflection;

namespace DarkConfig.Internal {
    /// Cached type reflection data.
    /// Reflection is quite expensive especially on consoles
    /// so it's worth trying to reduce how much we need to do it as much as possible.
    static class ReflectionCache {
        internal class TypeInfo {
            public ClassAttributesFlags AttributeFlags = ClassAttributesFlags.None;
            public MemberMetadata[] Members;
            
            public MethodInfo FromDoc;
            public MethodInfo PostDoc;
        }
        
        /// Information about either a field or property on a particular type. 
        internal struct MemberMetadata {
            public string ShortName;
            public MemberInfo Info;
            public Type Type;
            public bool IsField;
            public bool HasConfigMandatoryAttribute;
            public bool HasConfigAllowMissingAttribute;
            public bool HasConfigIgnoreAttribute;
        }

        [Flags]
        internal enum ClassAttributesFlags {
            None = 0,
            HasConfigMandatoryAttribute = 1 << 0,
            HasConfigAllowMissingAttribute = 1 << 1
        }
        
        ////////////////////////////////////////////

        internal static TypeInfo GetTypeInfo(Type type) {
            return cachedTypeInfo.TryGetValue(type, out var info) ? info : CacheTypeInfo(type);
        }
        
        ////////////////////////////////////////////
        
        static readonly Dictionary<Type, TypeInfo> cachedTypeInfo = new Dictionary<Type, TypeInfo>();
        
        ////////////////////////////////////////////

        static TypeInfo CacheTypeInfo(Type type) {
            var info = new TypeInfo {
                FromDoc = type.GetMethod("FromDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
                PostDoc = type.GetMethod("PostDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            };

            // Read class attributes
            foreach (var attribute in type.GetCustomAttributes(true)) {
                switch (attribute) {
                    case ConfigMandatoryAttribute _: info.AttributeFlags |= ClassAttributesFlags.HasConfigMandatoryAttribute; break;
                    case ConfigAllowMissingAttribute _: info.AttributeFlags |= ClassAttributesFlags.HasConfigAllowMissingAttribute; break;
                }
            }
            
            Platform.Assert((info.AttributeFlags & ClassAttributesFlags.HasConfigMandatoryAttribute) == 0 || (info.AttributeFlags & ClassAttributesFlags.HasConfigAllowMissingAttribute) == 0, 
                "Type", type.Name, "has both ConfigAllowMissing and ConfigMandatory attributes.");
            
            
            var memberBindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            var properties = type.GetProperties(memberBindingFlags);
            var fields = type.GetFields(memberBindingFlags);

            // Count the members.
            int memberCount = 0;
            foreach (var propertyInfo in properties) {
                if (!propertyInfo.IsSpecialName && propertyInfo.CanWrite && propertyInfo.CanRead) {
                    memberCount++;
                }
            }
            foreach (var fieldInfo in fields) {
                if (!fieldInfo.IsSpecialName && fieldInfo.Name[0] != '<') {
                    memberCount++;
                }
            }
            info.Members = new MemberMetadata[memberCount];

            int currentMemberIndex = 0;
            
            // Read all properties from the type.
            foreach (var propertyInfo in properties) {
                if (propertyInfo.IsSpecialName || !propertyInfo.CanWrite || !propertyInfo.CanRead) {
                    continue;
                }
                
                var metadata = new MemberMetadata {
                    Info = propertyInfo,
                    ShortName = RemoveHungarianPrefix(propertyInfo.Name),
                    IsField = false,
                    Type = propertyInfo.PropertyType
                };
                SetMemberAttributeFlags(ref metadata);
                info.Members[currentMemberIndex] = metadata;
                currentMemberIndex++;
            }

            // Read all fields from the type.
            foreach (var fieldInfo in fields) {
                // Compiler-generated property backing fields have the name "<propertyName>k_BackingField" so 
                // ignore any fields with names that start with '<'.  Apparently IsSpecialName doesn't cover
                // this case.
                if (fieldInfo.IsSpecialName || fieldInfo.Name[0] == '<') {
                    continue;
                }
                
                var metadata = new MemberMetadata {
                    Info = fieldInfo,
                    ShortName = RemoveHungarianPrefix(fieldInfo.Name),
                    IsField = true,
                    Type = fieldInfo.FieldType
                };
                SetMemberAttributeFlags(ref metadata);
                info.Members[currentMemberIndex] = metadata;
                currentMemberIndex++;
            }

            cachedTypeInfo[type] = info;
            return info;
        }
        
        /// Removes one letter hungarian notation prefixes from field names.
        static string RemoveHungarianPrefix(string name) {
            return name.Length > 1 && name[1] == '_' ? name.Substring(2) : name;
        }

        static void SetMemberAttributeFlags(ref MemberMetadata metadata) {
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