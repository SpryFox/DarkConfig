using System;
using System.Collections.Generic;
using System.Reflection;

namespace DarkConfig.Internal {
    /// Cached type reflection data.
    /// Reflection is quite expensive especially on consoles
    /// so it's worth trying to reduce how much we need to do it as much as possible.
    class ReflectionCache {
        internal class TypeInfo {
            public MethodInfo FromDoc;
            public MethodInfo PostDoc;

            public int NumRequired;
            public List<string> MemberNames;
            public List<MemberFlags> MemberAttributes;
            public List<MemberInfo> MemberInfo;
        }

        [Flags]
        internal enum MemberFlags : byte {
            None = 0,
            Field = 1 << 0,
            ConfigSourceInfo = 1 << 1
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

            var defaultOptions = Configs.Settings.DefaultReifierOptions;

            // Read class attributes
            bool typeHasMandatoryAttribute = false;
            bool typeHasOptionalAttribute = false;
            foreach (object attribute in type.GetCustomAttributes(true)) {
                switch (attribute) {
                    case ConfigMandatoryAttribute _: typeHasMandatoryAttribute = true; break;
                    case ConfigAllowMissingAttribute _: typeHasOptionalAttribute = true; break;
                }
            }

            if (typeHasMandatoryAttribute && typeHasOptionalAttribute) {
                throw new Exception($"Type {type.Name} has both ConfigAllowMissing and ConfigMandatory attributes.");
            }

            const BindingFlags MEMBER_BINDING_FLAGS = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            var properties = type.GetProperties(MEMBER_BINDING_FLAGS);
            var fields = type.GetFields(MEMBER_BINDING_FLAGS);

            // pre-alloc the List<T>'s
            int maxMemberCount = properties.Length + fields.Length;
            info.MemberNames = new List<string>(maxMemberCount);
            info.MemberAttributes = new List<MemberFlags>(maxMemberCount);
            info.MemberInfo = new List<MemberInfo>(maxMemberCount);

            bool typeMemberRequiredDefault = typeHasMandatoryAttribute || (!typeHasOptionalAttribute && ((defaultOptions & ReificationOptions.AllowMissingFields) == 0));
            // Unity Objects have a lot of fields, it never makes sense to set most of them from configs
            if (GetFirstNonObjectBaseClass(type).ToString() == "UnityEngine.Object") {
                typeMemberRequiredDefault = false;
            }

            // Read all properties from the type.
            foreach (var propertyInfo in properties) {
                // TODO (graham) should we skip properties with no getter?
                // Skip computed properties and delegate types. 
                if (propertyInfo.IsSpecialName || !propertyInfo.CanWrite || !propertyInfo.CanRead || IsDelegateType(propertyInfo.PropertyType)) {
                    continue;
                }
                
                // Explicit Required/Optional attributes on the type override the global defaults.
                bool required = typeMemberRequiredDefault;
                bool ignored = false;
                var flags = MemberFlags.None;
                bool propertyHasConfigMandatoryAttribute = false;
                bool propertyHasConfigAllowMissingAttribute = false;
                foreach (object attribute in propertyInfo.GetCustomAttributes(true)) {
                    if (attribute is ConfigIgnoreAttribute) {
                        ignored = true;
                        break;
                    }
                    
                    switch (attribute) {
                        // Explicit Required/Optional attributes on a field override the type and global defaults.
                        case ConfigMandatoryAttribute:
                            propertyHasConfigMandatoryAttribute = true;
                            required = true;
                            break;
                        case ConfigAllowMissingAttribute: 
                            required = false;
                            propertyHasConfigAllowMissingAttribute = true;
                            break;
                        case ConfigSourceInformationAttribute:
                            flags |= MemberFlags.ConfigSourceInfo;
                            // Special field to auto-populate with SourceInformation
                            if (propertyInfo.PropertyType != typeof(string)) {
                                throw new Exception($"Property {propertyInfo.Name} annotated with ConfigSourceInformation must be a string");
                            }
                            break;
                    }
                }
                if (propertyHasConfigMandatoryAttribute && propertyHasConfigAllowMissingAttribute) {
                    throw new Exception($"Property {propertyInfo.Name} has both Mandatory and AllowMissing attributes");
                }

                if (ignored) {
                    continue;
                }

                int insertionPoint = required ? info.NumRequired++ : info.MemberNames.Count;
                info.MemberNames.Insert(insertionPoint, RemoveHungarianPrefix(propertyInfo.Name));
                info.MemberInfo.Insert(insertionPoint, propertyInfo);
                info.MemberAttributes.Insert(insertionPoint, flags);
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
                
                // Explicit Required/Optional attributes on the type override the global defaults.
                bool required = typeMemberRequiredDefault;
                bool ignored = false;
                var flags = MemberFlags.Field;
                bool fieldHasConfigMandatoryAttribute = false;
                bool fieldHasConfigAllowMissingAttribute = false;
                foreach (object attribute in fieldInfo.GetCustomAttributes(true)) {
                    if (attribute is ConfigIgnoreAttribute) {
                        ignored = true;
                        break;
                    }
                    switch (attribute) {
                        case ConfigMandatoryAttribute:
                            fieldHasConfigMandatoryAttribute = true;
                            required = true;
                            break;
                        case ConfigAllowMissingAttribute:
                            fieldHasConfigAllowMissingAttribute = true;
                            required = false; 
                            break;
                        case ConfigSourceInformationAttribute: 
                            flags |= MemberFlags.ConfigSourceInfo;
                            // Special field to auto-populate with SourceInformation
                            if (fieldInfo.FieldType != typeof(string)) {
                                throw new Exception("Field with ConfigSourceInformation should be a string");
                            }
                            break;
                    }
                }
                
                if (fieldHasConfigMandatoryAttribute && fieldHasConfigAllowMissingAttribute) {
                    throw new Exception($"Field {fieldInfo.Name} has both Mandatory and AllowMissing attributes");
                }

                if (ignored) {
                    continue;
                }

                int insertionPoint = required ? info.NumRequired++ : info.MemberNames.Count;
                info.MemberNames.Insert(insertionPoint, RemoveHungarianPrefix(fieldInfo.Name));
                info.MemberInfo.Insert(insertionPoint, fieldInfo);
                info.MemberAttributes.Insert(insertionPoint, flags);
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
        
        Type GetFirstNonObjectBaseClass(Type t) {
            var curr = t;
            while (curr.BaseType != null && curr.BaseType != typeof(object)) {
                curr = curr.BaseType;
            }

            return curr;
        }
    }
}
