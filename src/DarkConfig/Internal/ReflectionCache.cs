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

            public List<string> MemberNames;
            public List<MemberFlags> MemberAttributes;
            public List<MemberInfo> MemberInfo;
        }

        [Flags]
        internal enum ClassAttributesFlags : byte {
            None = 0,
            HasConfigMandatoryAttribute = 1 << 0,
            HasConfigAllowMissingAttribute = 1 << 1,
            
            Invalid = HasConfigMandatoryAttribute | HasConfigAllowMissingAttribute
        }

        [Flags]
        internal enum MemberFlags : byte {
            None = 0,
            IsField = 1 << 0,
            HasConfigMandatoryAttribute = 1 << 1,
            HasConfigAllowMissingAttribute = 1 << 2,
            HasConfigSourceInformationAttribute = 1 << 3
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
            
            info.MemberNames = new List<string>(memberCount);
            info.MemberAttributes = new List<MemberFlags>(memberCount);
            info.MemberInfo = new List<MemberInfo>(memberCount);

            // Read all properties from the type.
            foreach (var propertyInfo in properties) {
                // TODO (graham) should we skip properties with no getter?
                // Skip computed properties and delegate types. 
                if (propertyInfo.IsSpecialName || !propertyInfo.CanWrite || !propertyInfo.CanRead || IsDelegateType(propertyInfo.PropertyType)) {
                    continue;
                }

                bool ignored = false;
                var flags = MemberFlags.None;
                foreach (object attribute in propertyInfo.GetCustomAttributes(true)) {
                    if (attribute is ConfigIgnoreAttribute) {
                        ignored = true;
                        break;
                    }
                    switch (attribute) {
                        case ConfigMandatoryAttribute: flags |= MemberFlags.HasConfigMandatoryAttribute; break;
                        case ConfigAllowMissingAttribute: flags |= MemberFlags.HasConfigAllowMissingAttribute; break;
                        case ConfigSourceInformationAttribute: 
                            flags |= MemberFlags.HasConfigSourceInformationAttribute;
                            // Special field to auto-populate with SourceInformation
                            if (propertyInfo.PropertyType != typeof(string)) {
                                throw new Exception("Field with ConfigSourceInformation should be a string");
                            }
                            break;
                    }
                }

                if (ignored) {
                    continue;
                }

                info.MemberInfo.Add(propertyInfo);
                info.MemberNames.Add(RemoveHungarianPrefix(propertyInfo.Name));
                info.MemberAttributes.Add(flags);
            }

            // Read all fields from the type.
            foreach (var fieldInfo in fields) {
                // Compiler-generated property backing fields have the name "<propertyName>k_BackingField" so 
                // ignore any fields with names that start with '<'.  Apparently IsSpecialName doesn't cover
                // this case.
                // Also skip delegate types.
                if (fieldInfo.IsSpecialName || fieldInfo.Name[0] == '<' || IsDelegateType(fieldInfo.FieldType)) {
                    continue;
                }
                
                bool ignored = false;
                var flags = MemberFlags.IsField;
                foreach (object attribute in fieldInfo.GetCustomAttributes(true)) {
                    if (attribute is ConfigIgnoreAttribute) {
                        ignored = true;
                        break;
                    }
                    switch (attribute) {
                        case ConfigMandatoryAttribute: flags |= MemberFlags.HasConfigMandatoryAttribute; break;
                        case ConfigAllowMissingAttribute: flags |= MemberFlags.HasConfigAllowMissingAttribute; break;
                        case ConfigSourceInformationAttribute: 
                            flags |= MemberFlags.HasConfigSourceInformationAttribute;
                            // Special field to auto-populate with SourceInformation
                            if (fieldInfo.FieldType != typeof(string)) {
                                throw new Exception("Field with ConfigSourceInformation should be a string");
                            }
                            break;
                    }
                }

                if (ignored) {
                    continue;
                }

                info.MemberInfo.Add(fieldInfo);
                info.MemberNames.Add(RemoveHungarianPrefix(fieldInfo.Name));
                info.MemberAttributes.Add(flags);
            }

            cachedTypeInfo[type] = info;
            return info;
        }
        
        /// Removes one letter hungarian notation prefixes from field names.
        string RemoveHungarianPrefix(string name) {
            return name.Length > 1 && name[1] == '_' ? name.Substring(2) : name;
        }
        
        bool IsDelegateType(Type type) {
            // http://mikehadlow.blogspot.com/2010/03/how-to-tell-if-type-is-delegate.html
            return typeof(MulticastDelegate).IsAssignableFrom(type.BaseType);
        }
    }
}
