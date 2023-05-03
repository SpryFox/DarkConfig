using System;
using System.Collections.Generic;
using System.Reflection;

namespace DarkConfig.Internal {
    /// Cached type reflection data.
    /// Reflection is quite expensive especially on consoles
    /// so it's worth trying to reduce how much we need to do it as much as possible.
    class ReflectionCache {
        internal class TypeInfo {
            public ClassAttributesFlags AttributeFlags = ClassAttributesFlags.None;
            public MethodInfo FromDoc;
            public MethodInfo PostDoc;
            public List<MemberMetadata> Members;
        }
        
        /// Information about either a field or property on a particular type. 
        internal struct MemberMetadata {
            public string ShortName;
            public MemberInfo Info;
            public Type Type;
            
            public bool IsField;
            public bool HasConfigMandatoryAttribute;
            public bool HasConfigAllowMissingAttribute;
            public bool HasConfigSourceInformationAttribute;
        }

        [Flags]
        internal enum ClassAttributesFlags {
            None = 0,
            HasConfigMandatoryAttribute = 1 << 0,
            HasConfigAllowMissingAttribute = 1 << 1,
            
            Invalid = HasConfigMandatoryAttribute | HasConfigAllowMissingAttribute
        }
        
        ////////////////////////////////////////////

        internal TypeInfo GetTypeInfo(Type type) {
            return cachedTypeInfo.TryGetValue(type, out var info) ? info : CacheTypeInfo(type);
        }
        
        ////////////////////////////////////////////
        
        readonly Dictionary<Type, TypeInfo> cachedTypeInfo = new Dictionary<Type, TypeInfo>();
        
        ////////////////////////////////////////////

        TypeInfo CacheTypeInfo(Type type) {
            var info = new TypeInfo {
                FromDoc = type.GetMethod("FromDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
                PostDoc = type.GetMethod("PostDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            };

            // Read class attributes
            foreach (object attribute in type.GetCustomAttributes(true)) {
                switch (attribute) {
                    case ConfigMandatoryAttribute _: info.AttributeFlags |= ClassAttributesFlags.HasConfigMandatoryAttribute; break;
                    case ConfigAllowMissingAttribute _: info.AttributeFlags |= ClassAttributesFlags.HasConfigAllowMissingAttribute; break;
                }
            }
            
            Configs.Assert(info.AttributeFlags != ClassAttributesFlags.Invalid, $"Type {type.Name} has both ConfigAllowMissing and ConfigMandatory attributes.");
            
            const BindingFlags MEMBER_BINDING_FLAGS = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            var properties = type.GetProperties(MEMBER_BINDING_FLAGS);
            var fields = type.GetFields(MEMBER_BINDING_FLAGS);

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
            info.Members = new List<MemberMetadata>(memberCount);

            int currentMemberIndex = 0;
            
            // Read all properties from the type.
            foreach (var propertyInfo in properties) {
                if (propertyInfo.IsSpecialName || !propertyInfo.CanWrite || !propertyInfo.CanRead) {
                    continue;
                }
                
                bool ignored = false;
                
                var metadata = new MemberMetadata {
                    Info = propertyInfo,
                    ShortName = RemoveHungarianPrefix(propertyInfo.Name),
                    IsField = false,
                    Type = propertyInfo.PropertyType
                };
                
                foreach (object attribute in propertyInfo.GetCustomAttributes(true)) {
                    if (attribute is ConfigMandatoryAttribute) {
                        metadata.HasConfigMandatoryAttribute = true;
                    } else if (attribute is ConfigAllowMissingAttribute) {
                        metadata.HasConfigAllowMissingAttribute = true;
                    } else if (attribute is ConfigIgnoreAttribute) {
                        ignored = true;
                    }
                    
                    if (attribute is ConfigSourceInformationAttribute) {
                        metadata.HasConfigSourceInformationAttribute = true;
                    }
                }

                if (!ignored) {
                    info.Members.Add(metadata);
                }
            }

            // Read all fields from the type.
            foreach (var fieldInfo in fields) {
                // Compiler-generated property backing fields have the name "<propertyName>k_BackingField" so 
                // ignore any fields with names that start with '<'.  Apparently IsSpecialName doesn't cover
                // this case.
                if (fieldInfo.IsSpecialName || fieldInfo.Name[0] == '<') {
                    continue;
                }
                
                bool ignored = false;

                var metadata = new MemberMetadata {
                    Info = fieldInfo,
                    ShortName = RemoveHungarianPrefix(fieldInfo.Name),
                    IsField = true,
                    Type = fieldInfo.FieldType
                };
                
                foreach (object attribute in fieldInfo.GetCustomAttributes(true)) {
                    if (attribute is ConfigMandatoryAttribute) {
                        metadata.HasConfigMandatoryAttribute = true;
                    } else if (attribute is ConfigAllowMissingAttribute) {
                        metadata.HasConfigAllowMissingAttribute = true;
                    } else if (attribute is ConfigIgnoreAttribute) {
                        ignored = true;
                    }
                    
                    if (attribute is ConfigSourceInformationAttribute) {
                        metadata.HasConfigSourceInformationAttribute = true;
                    }
                }

                if (!ignored) {
                    info.Members.Add(metadata);
                }
            }

            cachedTypeInfo[type] = info;
            return info;
        }
        
        /// Removes one letter hungarian notation prefixes from field names.
        string RemoveHungarianPrefix(string name) {
            return name.Length > 1 && name[1] == '_' ? name.Substring(2) : name;
        }
    }
}
