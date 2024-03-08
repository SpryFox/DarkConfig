using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using YamlDotNet.Core.Tokens;

namespace DarkConfig.Internal {
    /// Cached type reflection data.
    /// Reflection is quite expensive especially on consoles
    /// so it's worth trying to reduce how much we need to do it as much as possible.
    internal class ReflectionCache {
        internal class TypeInfo {
            public MethodInfo FromDoc;
            public MethodInfo FromDocString;
            public MethodInfo PostDoc;

            // A mapping of union type identifiers to concrete types
            public MultiCaseDictionary<Type> UnionKeys;
            public bool IsUnionInline = false;

            // Source Info
            public int SourceInfoMemberIndex = -1;
            public int SourceInfoStaticMemberIndex = -1;

            #region Instanced
            public byte NumRequiredFields;
            public byte NumRequiredProperties;
            public byte NumOptionalFields;
            // NumOptionalProperties is implicitly defined as the length of the arrays minus the three other counts.
            // These arrays contain sorted data:
            // Required fields, then required properties, then optional fields, then optional properties
            public readonly List<string> MemberNames = new List<string>();
            public readonly List<MemberInfo> MemberInfos = new List<MemberInfo>();

            public Type GetMemberType(int memberIndex) {
                MemberInfo member = MemberInfos[memberIndex];
                if (IsField(memberIndex, false)) {
                    return ((FieldInfo) member).FieldType;
                } else {
                    return ((PropertyInfo) member).PropertyType;
                }
            }

            public object GetMemberValue(object obj, int memberIndex) {
                MemberInfo member = MemberInfos[memberIndex];
                if (IsField(memberIndex, false)) {
                    return ((FieldInfo) member).GetValue(obj);
                } else {
                    return ((PropertyInfo) member).GetValue(obj);
                }
            }

            public void SetMemberValue(object obj, int memberIndex, object value) {
                MemberInfo member = MemberInfos[memberIndex];
                if (IsField(memberIndex, false)) {
                    ((FieldInfo) member).SetValue(obj, value);
                } else {
                    ((PropertyInfo) member).SetValue(obj, value);
                }
            }

            [Flags]
            public enum MemberOptionFlags {
                Inline = 1 << 0
            }
            public readonly List<MemberOptionFlags> MemberOptions = new List<MemberOptionFlags>();
            #endregion

            #region Static
            public byte NumRequiredStaticFields;
            public byte NumRequiredStaticProperties;
            public byte NumOptionalStaticFields;
            // NumOptionalStaticProperties is implicitly defined as the length of the arrays minus the three other counts.
            // These arrays contain sorted data:
            // Required fields, then required properties, then optional fields, then optional properties
            public readonly List<string> StaticMemberNames = new List<string>();
            public readonly List<MemberInfo> StaticMemberInfos = new List<MemberInfo>();
            #endregion

            public bool IsField(int memberIndex, bool isStatic) {
                if (isStatic) {
                    if (memberIndex < NumRequiredStaticFields) {
                        return true;
                    }

                    int staticOptionalsStart = NumRequiredStaticFields + NumRequiredStaticProperties;
                    return memberIndex >= staticOptionalsStart && memberIndex < (staticOptionalsStart + NumOptionalStaticFields);
                }

                if (memberIndex < NumRequiredFields) {
                    return true;
                }

                int optionalsStart = NumRequiredFields + NumRequiredProperties;
                return memberIndex >= optionalsStart && memberIndex < (optionalsStart + NumOptionalFields);
            }

            public bool IsRequired(int memberIndex, bool isStatic) {
                if (isStatic) {
                    return memberIndex < NumRequiredStaticFields + NumRequiredStaticProperties;
                }
                return memberIndex < NumRequiredFields + NumRequiredProperties;
            }

            public void AddMember(string memberName, MemberInfo memberInfo, bool isRequired, bool isField, bool isStatic, bool isSourceInfo) {
                if (isSourceInfo) {
                    // Special field to auto-populate with SourceInformation
                    if ((!isStatic && SourceInfoMemberIndex >= 0) || (isStatic && SourceInfoStaticMemberIndex >= 0)) {
                        string existingName = !isStatic ? MemberNames[SourceInfoMemberIndex] : StaticMemberNames[SourceInfoStaticMemberIndex];
                        throw new Exception($"Property {memberInfo.Name} annotated with ConfigSourceInformation, but type {memberInfo.DeclaringType?.Name} "
                            + $"already has a member named {existingName} with that annotation");
                    }
                    if (isField) {
                        if (((FieldInfo) memberInfo).FieldType != typeof(string)) {
                            throw new Exception($"Field {memberInfo.Name} in type {memberInfo.DeclaringType?.Name} "
                                + "annotated with ConfigSourceInformation must be a string");
                        }
                    } else {
                        if (((PropertyInfo) memberInfo).PropertyType != typeof(string)) {
                            throw new Exception($"Property {memberInfo.Name} in type {memberInfo.DeclaringType?.Name} "
                                + "annotated with ConfigSourceInformation must be a string");
                        }
                    }
                }

                if (isStatic) {
                    int insertionIndex;

                    if (isRequired) {
                        insertionIndex = 0;
                        if (isField) {
                            NumRequiredStaticFields++;
                        } else {
                            insertionIndex += NumRequiredStaticFields;
                            NumRequiredStaticProperties++;
                        }
                    } else {
                        insertionIndex = NumRequiredStaticFields + NumRequiredStaticProperties;
                        if (isField) {
                            NumOptionalStaticFields++;
                        } else {
                            insertionIndex += NumOptionalStaticFields;
                        }
                    }

                    if (isSourceInfo) {
                        SourceInfoStaticMemberIndex = insertionIndex;
                    } else if (insertionIndex <= SourceInfoStaticMemberIndex) {
                        SourceInfoStaticMemberIndex++;
                    }

                    StaticMemberNames.Insert(insertionIndex, memberName);
                    StaticMemberInfos.Insert(insertionIndex, memberInfo);
                } else {
                    int insertionIndex;

                    if (isRequired) {
                        insertionIndex = 0;
                        if (isField) {
                            insertionIndex += NumRequiredFields; // insert at end
                            NumRequiredFields++;
                        } else {
                            insertionIndex += NumRequiredFields;
                            NumRequiredProperties++;
                        }
                    } else {
                        insertionIndex = NumRequiredFields + NumRequiredProperties;
                        if (isField) {
                            insertionIndex += NumOptionalFields; // insert at end
                            NumOptionalFields++;
                        } else {
                            insertionIndex += NumOptionalFields;
                        }
                    }

                    if (isSourceInfo) {
                        SourceInfoMemberIndex = insertionIndex;
                    } else if (insertionIndex <= SourceInfoStaticMemberIndex) {
                        SourceInfoMemberIndex++;
                    }

                    MemberNames.Insert(insertionIndex, memberName);
                    MemberInfos.Insert(insertionIndex, memberInfo);

                    MemberOptionFlags optionFlags = default;
                    foreach (object attribute in memberInfo.GetCustomAttributes(true)) {
                        switch (attribute) {
                            case ConfigInlineAttribute _:
                                optionFlags |= MemberOptionFlags.Inline;
                                break;
                        }
                    }
                    MemberOptions.Insert(insertionIndex, optionFlags);
                }
            }
        }

        ////////////////////////////////////////////

        internal TypeInfo GetTypeInfo(Type type) {
            return cachedTypeInfo.TryGetValue(type, out var info) ? info : CacheTypeInfo(type);
        }

        ////////////////////////////////////////////

        readonly Dictionary<Type, TypeInfo> cachedTypeInfo = new Dictionary<Type, TypeInfo>();
        readonly HashSet<Assembly> prechachedAssemblies = new();

        ////////////////////////////////////////////

        // Precache everything in this assembly that requires iterating all types to resolve
        bool PrecacheAssembly(Assembly sourceAssembly) {
            if (!prechachedAssemblies.Contains(sourceAssembly)) {
                prechachedAssemblies.Add(sourceAssembly);
                foreach (Type type in sourceAssembly.GetTypes()) {
                    foreach (object attribute in type.GetCustomAttributes(false)) {
                        switch (attribute) {
                            case ConfigUnionAttribute _:
                                GetTypeInfo(type);
                                break;
                            case ConfigUnionInlineAttribute _:
                                GetTypeInfo(type);
                                break;
                        }
                    }
                }
                return true;
            }

            return false;
        }

        TypeInfo CacheTypeInfo(Type type) {
            // If precaching this assembly loads the type we're trying to construct, early out.
            if (PrecacheAssembly(type.Assembly) && cachedTypeInfo.TryGetValue(type, out var typeInfo)) {
                return typeInfo;
            }

            var info = new TypeInfo {
                FromDoc = type.GetMethod("FromDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, new[] {type, typeof(DocNode)}),
                FromDocString = type.GetMethod("FromDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, new[] {type, typeof(string)}),
                PostDoc = type.GetMethod("PostDoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            };

            var defaultOptions = Configs.Settings.DefaultReifierOptions;

            // Read class attributes
            bool typeHasMandatoryAttribute = false;
            bool typeHasOptionalAttribute = false;
            bool typeHasUnionInlineAttribute = false;
            string typeUnionKey = null;
            foreach (object attribute in type.GetCustomAttributes(true)) {
                switch (attribute) {
                    case ConfigMandatoryAttribute _:
                        typeHasMandatoryAttribute = true;
                        break;
                    case ConfigAllowMissingAttribute _:
                        typeHasOptionalAttribute = true;
                        break;
                }
            }

            foreach (object attribute in type.GetCustomAttributes(false)) {
                switch (attribute) {
                    case ConfigUnionAttribute unionAttribute:
                        typeUnionKey = unionAttribute.Key;
                        break;
                    case ConfigUnionInlineAttribute unionInlineAttribute:
                        typeUnionKey = unionInlineAttribute.Key;
                        typeHasUnionInlineAttribute = true;
                        break;
                }
            }
            info.IsUnionInline = typeHasUnionInlineAttribute;

            if (typeHasMandatoryAttribute && typeHasOptionalAttribute) {
                throw new Exception($"Type {type.Name} has both ConfigAllowMissing and ConfigMandatory attributes.");
            }

            // if type is a union, register it with its base type
            if (typeUnionKey != null) {
                if (type.BaseType == typeof(Object) || type.BaseType == null) {
                    throw new Exception($"Type {type.Name} has ConfigUnion but is not a child type");
                }
                TypeInfo parentInfo = GetTypeInfo(type.BaseType);
                parentInfo.UnionKeys ??= new MultiCaseDictionary<Type>();

                if (!parentInfo.UnionKeys.TryAdd(typeUnionKey, type)) {
                    throw new Exception($"Type {type.Name} has ConfigUnion with duplicate key {typeUnionKey}");
                }
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

                    // Explicit Required/Optional attributes on a field override the type and global defaults.
                    if (attribute is ConfigMandatoryAttribute) {
                        required = true;
                        numRequirementAttributes++;
                    } else if (attribute is ConfigAllowMissingAttribute) {
                        required = false;
                        numRequirementAttributes++;
                    } else if (attribute is ConfigSourceInformationAttribute) {
                        sourceInfo = true;
                    } else if (attribute is ConfigKeyAttribute keyAttribute) {
                        propertyName = keyAttribute.Key;
                    }
                }

                if (numRequirementAttributes == 2) {
                    throw new Exception($"Property {propertyInfo.Name} has both Mandatory and AllowMissing attributes");
                }

                if (!ignored) {
                    info.AddMember(propertyName, propertyInfo, required, false, propertyInfo.SetMethod?.IsStatic == true, sourceInfo);
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

                    // Explicit Required/Optional attributes on a field override the type and global defaults.
                    if (attribute is ConfigMandatoryAttribute) {
                        required = true;
                        numRequirementAttributes++;
                    } else if (attribute is ConfigAllowMissingAttribute) {
                        required = false;
                        numRequirementAttributes++;
                    } else if (attribute is ConfigSourceInformationAttribute) {
                        sourceInfo = true;
                    } else if (attribute is ConfigKeyAttribute keyAttribute) {
                        fieldName = keyAttribute.Key;
                    }
                }

                if (numRequirementAttributes == 2) {
                    throw new Exception($"Field {fieldInfo.Name} has both Mandatory and AllowMissing attributes");
                }

                if (!ignored) {
                    info.AddMember(fieldName, fieldInfo, required, true, fieldInfo.IsStatic, sourceInfo);
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
