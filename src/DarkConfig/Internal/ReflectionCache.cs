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

            // Source Info
            public string SourceInfoMemberName;
            
            // Fields
            public int NumRequiredFields = 0;
            public List<string> FieldNames = new List<string>();
            public List<FieldInfo> FieldInfos = new List<FieldInfo>();
            
            // Properties
            public int NumRequiredProperties = 0;
            public List<string> PropertyNames = new List<string>();
            public List<PropertyInfo> PropertyInfos = new List<PropertyInfo>();
            
            // Static Fields
            public int NumRequiredStaticFields = 0;
            public List<string> StaticFieldNames = new List<string>();
            public List<FieldInfo> StaticFieldInfos = new List<FieldInfo>();
            
            // Static Properties
            public int NumRequiredStaticProperties = 0;
            public List<string> StaticPropertyNames = new List<string>();
            public List<PropertyInfo> StaticPropertyInfos = new List<PropertyInfo>();
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
                
                string propertyName = RemoveHungarianPrefix(propertyInfo.Name);
                
                // Explicit Required/Optional attributes on the type override the global defaults.
                bool ignored = false;
                bool required = typeMemberRequiredDefault;
                bool sourceInfo = false;
                byte numRequirementAttributes = 0;
                foreach (object attribute in propertyInfo.GetCustomAttributes(true)) {
                    if (attribute is ConfigIgnoreAttribute) {
                        ignored = true;
                        break;
                    }
                    
                    switch (attribute) {
                        // Explicit Required/Optional attributes on a field override the type and global defaults.
                        case ConfigMandatoryAttribute:
                            required = true;
                            numRequirementAttributes++;
                            break;
                        case ConfigAllowMissingAttribute: 
                            required = false;
                            numRequirementAttributes++;
                            break;
                        case ConfigSourceInformationAttribute:
                            sourceInfo = true;
                            break;
                        case ConfigKeyAttribute:
                            propertyName = ((ConfigKeyAttribute)attribute).Key;
                            break;
                    }
                }
                
                if (numRequirementAttributes == 2) {
                    throw new Exception($"Property {propertyInfo.Name} has both Mandatory and AllowMissing attributes");
                }

                if (ignored) {
                    continue;
                }

                if (sourceInfo) {
                    // Special field to auto-populate with SourceInformation
                    if (!string.IsNullOrEmpty(info.SourceInfoMemberName)) {
                        throw new Exception($"Property {propertyInfo.Name} annotated with ConfigSourceInformation, but type {type.Name} "
                            + $"already has a member named {info.SourceInfoMemberName} with that annotation");
                    }
                    if (propertyInfo.PropertyType != typeof(string)) {
                        throw new Exception($"Property {propertyInfo.Name} annotated with ConfigSourceInformation must be a string");
                    }
                    info.SourceInfoMemberName = propertyName;
                }
                
                if (propertyInfo.SetMethod?.IsStatic == true) {
                    if (required) {
                        info.StaticPropertyNames.Insert(info.NumRequiredStaticProperties, propertyName);
                        info.StaticPropertyInfos.Insert(info.NumRequiredStaticProperties, propertyInfo);
                        info.NumRequiredStaticProperties++;
                    } else {
                        info.StaticPropertyNames.Add(propertyName);
                        info.StaticPropertyInfos.Add(propertyInfo);
                    }
                } else {
                    if (required) {
                        info.PropertyNames.Insert(info.NumRequiredProperties, propertyName);
                        info.PropertyInfos.Insert(info.NumRequiredProperties, propertyInfo);
                        info.NumRequiredProperties++;
                    } else {
                        info.PropertyNames.Add(propertyName);
                        info.PropertyInfos.Add(propertyInfo);
                    }
                }
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
                
                string fieldName = RemoveHungarianPrefix(fieldInfo.Name);
                
                // Explicit Required/Optional attributes on the type override the global defaults.
                bool ignored = false;
                bool required = typeMemberRequiredDefault;
                bool sourceInfo = false;
                byte numRequirementAttributes = 0;
                foreach (object attribute in fieldInfo.GetCustomAttributes(true)) {
                    if (attribute is ConfigIgnoreAttribute) {
                        ignored = true;
                        break;
                    }
                    switch (attribute) {
                        case ConfigMandatoryAttribute:
                            required = true;
                            numRequirementAttributes++;
                            break;
                        case ConfigAllowMissingAttribute:
                            required = false; 
                            numRequirementAttributes++;
                            break;
                        case ConfigSourceInformationAttribute:
                            sourceInfo = true;
                            break;
                        case ConfigKeyAttribute:
                            fieldName = ((ConfigKeyAttribute)attribute).Key;
                            break;
                    }
                }
                
                if (numRequirementAttributes == 2) {
                    throw new Exception($"Field {fieldInfo.Name} has both Mandatory and AllowMissing attributes");
                }

                if (ignored) {
                    continue;
                }

                if (sourceInfo) {
                    // Special field to auto-populate with SourceInformation
                            
                    if (!string.IsNullOrEmpty(info.SourceInfoMemberName)) {
                        throw new Exception($"Field {fieldInfo.Name} annotated with ConfigSourceInformation, but type {type.Name} "
                            + $"already has a member named {info.SourceInfoMemberName} with that annotation");
                    }
                    if (fieldInfo.FieldType != typeof(string)) {
                        throw new Exception("Field with ConfigSourceInformation should be a string");
                    }

                    info.SourceInfoMemberName = fieldName;
                }

                if (fieldInfo.IsStatic) {
                    if (required) {
                        info.StaticFieldNames.Insert(info.NumRequiredStaticFields, fieldName);
                        info.StaticFieldInfos.Insert(info.NumRequiredStaticFields, fieldInfo);
                        info.NumRequiredStaticFields++;
                    } else {
                        info.StaticFieldNames.Add(fieldName);
                        info.StaticFieldInfos.Add(fieldInfo);
                    }
                } else {
                    if (required) {
                        info.FieldNames.Insert(info.NumRequiredFields, fieldName);
                        info.FieldInfos.Insert(info.NumRequiredFields, fieldInfo);
                        info.NumRequiredFields++;
                    } else {
                        info.FieldNames.Add(fieldName);
                        info.FieldInfos.Add(fieldInfo);
                    }
                }
            }

            cachedTypeInfo[type] = info;
            return info;
        }
        
        /// Removes one letter hungarian notation prefixes from field names.
        static string RemoveHungarianPrefix(string name) {
            return name.Length > 1 && name[1] == '_' ? name.Substring(2) : name;
        }

        static bool IsDelegateType(Type type) {
            // http://mikehadlow.blogspot.com/2010/03/how-to-tell-if-type-is-delegate.html
            return typeof(MulticastDelegate).IsAssignableFrom(type.BaseType);
        }

        static Type GetFirstNonObjectBaseClass(Type t) {
            var curr = t;
            while (curr.BaseType != null && curr.BaseType != typeof(object)) {
                curr = curr.BaseType;
            }

            return curr;
        }
    }
}
