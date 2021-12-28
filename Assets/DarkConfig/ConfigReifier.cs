using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DarkConfig.Internal {
    public static class ConfigReifier {
        /// User-defined type reifiers
        public static readonly Dictionary<Type, FromDocDelegate> CustomReifiers = new Dictionary<Type, FromDocDelegate>();
        
        /////////////////////////////////////////////////
        
        /// Create an instance of an object and immediately set fields on it from the document. 
        /// The type of instance is supplied via the generic parameter.
        public static T CreateInstance<T>(DocNode dict, ConfigOptions? options = null) where T : new() {
            object obj = Activator.CreateInstance<T>();
            SetFieldsOnObject(ref obj, dict, options ?? Settings.DefaultReifierOptions);
            return (T) obj;
        }

        /// Create an instance of an object and immediately set fields on it from the document. 
        /// The type of instance is supplied explicitly as the first argument.
        /// Requires a zero-args constructor on the type though it doesn't enforce that.
        public static object CreateInstance(Type t, DocNode dict, ConfigOptions? options = null) {
            object obj = Activator.CreateInstance(t);
            SetFieldsOnObject(ref obj, dict, options ?? Settings.DefaultReifierOptions);
            return obj;
        }

        /// Sets all members on the object *obj* (which must not be null) from *dict*.
        /// Expects *obj* to be a plain class, but if it's a boxed struct it will work as well.
        public static void SetFieldsOnObject<T>(ref T obj, DocNode dict, ConfigOptions? options = null) where T : class {
            Platform.Assert(obj != null, "Can't SetFields on null");
            Type type = typeof(T);
            if (type == typeof(object)) {
                // caller is using an object, but that is not the real type
                type = obj.GetType();
            }

            var setCopy = (object) obj;
            SetFieldsOnObject(type, ref setCopy, dict, options ?? Settings.DefaultReifierOptions);
            obj = (T) setCopy;
        }

        /// Sets all members on the struct *obj* (which must not be null) from *dict*.
        public static void SetFieldsOnStruct<T>(ref T obj, DocNode dict, ConfigOptions? options = null)
            where T : struct {
            Type type = typeof(T);
            var setCopy = (object) obj;
            SetFieldsOnObject(type, ref setCopy, dict, options ?? Settings.DefaultReifierOptions);
            obj = (T) setCopy;
        }


        /////////////////////////////////////////////////

        static bool IsDelegateType(Type type) {
            // http://mikehadlow.blogspot.com/2010/03/how-to-tell-if-type-is-delegate.html
            return typeof(MulticastDelegate).IsAssignableFrom(type.BaseType);
        }
        
        static Type GetFirstNonObjectBaseClass(Type t) {
            var curr = t;
            while (curr.BaseType != null && curr.BaseType != typeof(System.Object)) {
                curr = curr.BaseType;
            }

            return curr;
        }

        /// Sets all members on the object *obj* based on the appropriate key from *doc*
        public static void SetFieldsOnObject(Type type, ref object obj, DocNode doc, ConfigOptions options) {
            if (doc == null) {
                return;
            }

            // Grab global settings
            bool ignoreCase = (options & ConfigOptions.CaseSensitive) != ConfigOptions.CaseSensitive;
            bool checkForMissingFields = (options & ConfigOptions.AllowMissingFields) != ConfigOptions.AllowMissingFields;
            bool checkForExtraFields = (options & ConfigOptions.AllowExtraFields) != ConfigOptions.AllowExtraFields;
            
            // Override global settings with type-specific settings
            var typeSpecificSettings = Internal.ReflectionCache.GetClassAttributeFlags(type);
            if (typeSpecificSettings.HasConfigMandatoryAttribute) {
                checkForMissingFields = true;
            }
            if (typeSpecificSettings.HasConfigAllowMissingAttribute) {
                checkForMissingFields = false;
            }

            if (GetFirstNonObjectBaseClass(type).ToString() == "UnityEngine.Object") {
                // Unity Objects have a lot of fields, it never makes sense to set most of them from configs
                checkForMissingFields = false;
            }

            var setCopy = obj;
            if (doc.Type != DocNodeType.Dictionary) {
                // ==== Special Case ====
                // Allow specifying object types with a single property or field as a scalar value in configs.
                // This is syntactic sugar that lets us wrap values in classes.
                var typeMemberMetadata = Internal.ReflectionCache.GetTypeMemberMetadata(type);
                Platform.Assert(typeMemberMetadata.Count == 1, "Trying to set a field of type: ",
                    type, typeMemberMetadata.Count, "from value of wrong type:",
                    doc.Type == DocNodeType.Scalar ? doc.StringValue : doc.Type.ToString(),
                    "at",
                    doc.SourceInformation);
                var memberMetadata = typeMemberMetadata[0];
                SetMember(memberMetadata.Info, memberMetadata.IsField, ref setCopy, doc, options);
                obj = setCopy;
                return;
            }

            var requiredMembers = new List<string>();
            var setMembers = new List<string>();

            bool isAnyFieldMandatory = false;

            // Set the fields on the object.
            foreach (var memberMetadata in Internal.ReflectionCache.GetTypeMemberMetadata(type)) {
                var memberInfo = memberMetadata.Info;
                
                // Override global and class settings per-field.
                bool memberIsMandatory = memberMetadata.HasConfigMandatoryAttribute;
                bool memberAllowMissing = memberMetadata.HasConfigAllowMissingAttribute;
                bool memberIgnore = memberMetadata.HasConfigIgnoreAttribute;
                isAnyFieldMandatory |= memberIsMandatory;

                // never report delegates or events as present or missing
                memberIgnore |= IsDelegateType(memberMetadata.Type); 

                if (memberIgnore) {
                    continue;
                }

                // do meta stuff based on attributes/validation
                string fieldName = memberMetadata.ShortName;

                if (checkForMissingFields || memberIsMandatory) {
                    requiredMembers.Add(fieldName);
                }

                if (doc.TryGetValue(fieldName, ignoreCase, out var node)) {
                    SetMember(memberInfo, memberMetadata.IsField, ref setCopy, node, options);
                    setMembers.Add(fieldName);
                } else if (memberAllowMissing) {
                    // pretend like we set it
                    setMembers.Add(fieldName);
                }
            }

            // Check whether any fields in the doc were unused 
            if (checkForExtraFields) {
                var extraDocFields = new List<string>();
                
                foreach (var kv in doc.Pairs) {
                    string docKey = kv.Key;

                    bool wasSet = false;
                    foreach (var setMember in setMembers) {
                        if (string.Equals(setMember, docKey, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                            wasSet = true;
                            break;
                        }
                    }
                    
                    if (!wasSet) {
                        extraDocFields.Add(docKey);
                    }
                }

                if (extraDocFields.Count > 0) {
                    throw new ExtraFieldsException($"Extra doc fields: {JoinList(extraDocFields, ", ")} {doc.SourceInformation}");
                }
            }

            if (checkForMissingFields || isAnyFieldMandatory) {
                // check whether any fields in the class were unset
                var missing = new List<string>();
                foreach (var typeField in requiredMembers) {
                    if (!setMembers.Contains(typeField)) {
                        missing.Add(typeField);
                    }
                }

                if (missing.Count > 0) {
                    throw new MissingFieldsException($"Missing doc fields: {JoinList(missing, ", ")} {doc.SourceInformation}");
                }
            }

            obj = setCopy;
        }

        static void SetMember(MemberInfo memberInfo, bool isField, ref object obj, DocNode value, ConfigOptions? options) {
            if (isField) {
                var fieldInfo = (FieldInfo)memberInfo;
                if (obj == null && !fieldInfo.IsStatic) {
                    // silently don't set non-static fields
                    return;
                }
                object existing = fieldInfo.GetValue(obj);
                object updated = ValueOfType(fieldInfo.FieldType, existing, value, options);
                object setCopy = obj; // needed for structs
                fieldInfo.SetValue(setCopy, updated);
                obj = setCopy;                
            } else {
                var propertyInfo = (PropertyInfo)memberInfo;
                if (obj == null && !propertyInfo.CanWrite) {
                    // silently don't set non-static fields
                    return;
                }
                object existing = propertyInfo.GetValue(obj);
                object updated = ValueOfType(propertyInfo.PropertyType, existing, value, options);
                object setCopy = obj; // needed for structs
                propertyInfo.SetValue(setCopy, updated);
                obj = setCopy;
            }
        }

        /// convenience method to parse an enum value out of a string
        static T GetEnum<T>(string v) {
            return (T) Enum.Parse(typeof(T), v);
        }

        static string JoinList(List<string> args, string joinStr) {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < args.Count; i++) {
                sb.Append(args[i]);
                if (i < args.Count - 1) {
                    sb.Append(joinStr);
                }
            }

            return sb.ToString();
        }

        static object CallPostDoc(Type serializedType, object obj) {
            var postDoc = Internal.ReflectionCache.GetPostDocMethod(serializedType);
            if (postDoc != null) {
                try {
                    obj = postDoc.Invoke(null, new[] {obj});
                } catch (System.Reflection.TargetInvocationException e) {
                    throw e.InnerException;
                }
            }

            return obj;
        }

        public static object ValueOfType(Type fieldType, object existing, DocNode value, ConfigOptions? options) {
            try {
                if (fieldType == typeof(bool)) {
                    return Convert.ToBoolean(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                // floating-point value types
                if (fieldType == typeof(float)) {
                    return Convert.ToSingle(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(double)) {
                    return Convert.ToDouble(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(decimal)) {
                    return Convert.ToDecimal(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                // integral value types
                if (fieldType == typeof(sbyte)) {
                    return Convert.ToSByte(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(byte)) {
                    return Convert.ToByte(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(char)) {
                    return Convert.ToChar(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(short)) {
                    return Convert.ToInt16(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(ushort)) {
                    return Convert.ToUInt16(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(int)) {
                    return Convert.ToInt32(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(uint)) {
                    return Convert.ToUInt32(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(long)) {
                    return Convert.ToInt64(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(ulong)) {
                    return Convert.ToUInt64(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(string)) {
                    return value.StringValue;
                }

                if (fieldType.IsEnum) { // AudioRolloffMode, "Custom" => AudioRolloffMode.Custom
                    return Enum.Parse(fieldType, value.StringValue, true);
                }

                if (fieldType == typeof(DocNode)) {
                    return value;
                }

                if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                    if (value.Type == DocNodeType.Scalar && value.StringValue == "null") return null;
                    var innerType = Nullable.GetUnderlyingType(fieldType);
                    return ValueOfType(innerType, existing, value, options);
                }

                if (CustomReifiers.TryGetValue(fieldType, out var del)) {
                    existing = del(existing, value);
                    return CallPostDoc(fieldType, existing);
                }

                if (fieldType.IsArray) { // [1,2,3,4,5] => new int[] { 1,2,3,4,5 }
                    Type arrTyp = fieldType.GetElementType();
                    if (fieldType.GetArrayRank() == 2) {
                        var firstList = value[0];
                        var destArr = Array.CreateInstance(arrTyp, firstList.Count, value.Count);
                        int j = 0;
                        foreach (DocNode subList in value.Values) {
                            int i = 0;
                            foreach (var val in subList.Values.Select(item => ValueOfType(arrTyp, null, item, options))
                            ) {
                                destArr.SetValue(val, new int[] {i, j});
                                i++;
                            }

                            j++;
                        }

                        return destArr;
                    } else {
                        var iexisting = (Array) existing;
                        if (iexisting == null) {
                            iexisting = Array.CreateInstance(arrTyp, value.Count);
                        } else if (iexisting.Length != value.Count) {
                            var oldArr = iexisting;
                            iexisting = Array.CreateInstance(arrTyp, value.Count);
                            for (int i = 0; i < iexisting.Length && i < oldArr.Length; i++) {
                                iexisting.SetValue(oldArr.GetValue(i), i);
                            }
                        }

                        for (int i = 0; i < iexisting.Length; i++) {
                            var existingElt = iexisting.GetValue(i);
                            var updatedElt = ValueOfType(arrTyp, existingElt, value[i], options);
                            iexisting.SetValue(updatedElt, i);
                        }

                        return iexisting;
                    }
                }

                if (fieldType.IsGenericType) {
                    // this chunk of code handles generic dictionaries and lists; it only
                    // works with string keys on the dictionaries, and for now any values
                    // must have zero-args constructors
                    if (fieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
                        Type[] typeParameters = fieldType.GetGenericArguments();

                        if (existing == null) {
                            existing = Activator.CreateInstance(fieldType);
                        }

                        var iexisting = (System.Collections.IDictionary) existing;
                        Type kType = typeParameters[0];
                        Type vType = typeParameters[1];
                        ComposedDocNode keyNode = new ComposedDocNode(DocNodeType.Scalar,
                            sourceInformation: value.SourceInformation); // can reuse this one object
                        HashSet<object> usedKeys = new HashSet<object>();

                        // create/update all pairs in the doc
                        foreach (var kv in value.Pairs) {
                            keyNode.StringValue = kv.Key;
                            object existingKey = ValueOfType(kType, null, keyNode, options);
                            object existingValue = null;
                            if (iexisting.Contains(existingKey)) {
                                existingValue = iexisting[existingKey];
                            }

                            var updated = ValueOfType(vType, existingValue, kv.Value, options);
                            iexisting[existingKey] = updated;
                            usedKeys.Add(existingKey);
                        }

                        // remove any pairs not in the doc
                        var keysToRemove = new List<object>();
                        foreach (var k in iexisting.Keys) {
                            if (!usedKeys.Contains(k)) {
                                keysToRemove.Add(k);
                            }
                        }

                        foreach (var k in keysToRemove) {
                            iexisting.Remove(k);
                        }

                        return iexisting;
                    }

                    if (fieldType.GetGenericTypeDefinition() == typeof(List<>)) {
                        Type[] typeParameters = fieldType.GetGenericArguments();
                        if (existing == null) {
                            existing = Activator.CreateInstance(fieldType);
                        }

                        var iexisting = (System.Collections.IList) existing;
                        while (iexisting.Count > value.Count) {
                            iexisting.RemoveAt(iexisting.Count - 1);
                        }

                        for (int i = 0; i < iexisting.Count; i++) {
                            var existingElt = iexisting[i];
                            var updatedElt = ValueOfType(typeParameters[0], existingElt, value[i], options);
                            iexisting[i] = updatedElt;
                        }

                        while (iexisting.Count < value.Count) {
                            var newElt = ValueOfType(typeParameters[0], null, value[iexisting.Count], options);
                            iexisting.Add(newElt);
                        }

                        return existing;
                    }
                }

                var fromDocMethod = Internal.ReflectionCache.GetFromDocMethod(fieldType);
                if (fromDocMethod != null) {
                    // if there's a custom parser method on the class, delegate all work to that
                    // TODO: this doesn't do inherited FromDoc methods properly, but it should
                    try {
                        existing = fromDocMethod.Invoke(null, new[] {existing, value});
                        return CallPostDoc(fieldType, existing);
                    } catch (TargetInvocationException e) {
                        throw e.InnerException;
                    }
                }

                if (fieldType.IsClass) {
                    if (existing == null) {
                        existing = CreateInstance(fieldType, value, options);
                        return CallPostDoc(fieldType, existing);
                    } else {
                        SetFieldsOnObject(fieldType, ref existing, value, options ?? Settings.DefaultReifierOptions);
                        return CallPostDoc(fieldType, existing);
                    }
                }

                if (fieldType.IsValueType) {
                    // a struct; set the members and return it
                    if (existing == null) {
                        // structs can be null when boxed
                        existing = CreateInstance(fieldType, value, options);
                        return CallPostDoc(fieldType, existing);
                    } else {
                        SetFieldsOnObject(fieldType, ref existing, value, options ?? Settings.DefaultReifierOptions);
                        return CallPostDoc(fieldType, existing);
                    }
                }
            } catch (Exception e) {
                throw new ParseException($"Exception based on document starting at: {value.SourceInformation}", e);
            }

            throw new NotSupportedException($"Don't know how to update value of type {fieldType}");
        }
    }
}