using System;
using System.Collections.Generic;
using System.Reflection;

namespace DarkConfig.Internal {
    public static class ConfigReifier {
        /// User-defined type reifiers
        public static readonly Dictionary<Type, FromDocDelegate> CustomReifiers = new Dictionary<Type, FromDocDelegate>();
        
        /////////////////////////////////////////////////
        
        /// Sets all members on a struct from the given dictionary DocNode
        public static void SetFieldsOnStruct<T>(ref T obj, DocNode dict, ConfigOptions? options = null) where T : struct {
            Type type = typeof(T);
            object setRef = obj;
            SetFieldsOnObject(type, ref setRef, dict, options);
            obj = (T) setRef;
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

            object setRef = obj;
            SetFieldsOnObject(type, ref setRef, dict, options);
            obj = (T) setRef;
        }

        /// Sets all members on the object obj based on the appropriate key from doc
        public static void SetFieldsOnObject(Type type, ref object obj, DocNode doc, ConfigOptions? options = null) {
            if (doc == null) {
                return;
            }

            if (options == null) {
                options = Settings.DefaultReifierOptions;
            }

            var typeInfo = ReflectionCache.GetTypeInfo(type);

            // Grab global settings
            bool ignoreCase = (options & ConfigOptions.CaseSensitive) != ConfigOptions.CaseSensitive;
            bool checkForMissingFields = (options & ConfigOptions.AllowMissingFields) != ConfigOptions.AllowMissingFields;
            bool checkForExtraFields = (options & ConfigOptions.AllowExtraFields) != ConfigOptions.AllowExtraFields;
            
            // Override global settings with type-specific settings
            if ((typeInfo.AttributeFlags & ReflectionCache.ClassAttributesFlags.HasConfigMandatoryAttribute) != 0) {
                checkForMissingFields = true;
            }
            if ((typeInfo.AttributeFlags & ReflectionCache.ClassAttributesFlags.HasConfigAllowMissingAttribute) != 0) {
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
                Platform.Assert(typeInfo.Members.Length == 1, "Trying to set a field of type: ",
                    type, typeInfo.Members.Length, "from value of wrong type:",
                    doc.Type == DocNodeType.Scalar ? doc.StringValue : doc.Type.ToString(),
                    "at",
                    doc.SourceInformation);
                
                ref var memberMetadata = ref typeInfo.Members[0];
                SetMember(memberMetadata.Info, memberMetadata.IsField, ref setCopy, doc, options);
                obj = setCopy;
                return;
            }

            var requiredMembers = new List<string>();
            var setMembers = new List<string>();

            bool isAnyFieldMandatory = false;

            // Set the fields on the object.
            for (var memberIndex = 0; memberIndex < typeInfo.Members.Length; memberIndex++) {
                ref var memberMetadata = ref typeInfo.Members[memberIndex];
                
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
                    SetMember(memberMetadata.Info, memberMetadata.IsField, ref setCopy, node, options);
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

        /// <summary>
        /// Reads the given doc node and converts it to an instance of the given type.
        /// </summary>
        /// <param name="fieldType"></param>
        /// <param name="existing"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ParseException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static object ReadValueOfType(Type fieldType, object existing, DocNode value, ConfigOptions? options) {
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

                // String type
                if (fieldType == typeof(string)) {
                    return value.StringValue;
                }

                // Enum
                if (fieldType.IsEnum) {
                    return Enum.Parse(fieldType, value.StringValue, true);
                }

                // Nullable generic type
                if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                    if (value.Type == DocNodeType.Scalar && value.StringValue == "null") {
                        return null;
                    }
                    var innerType = Nullable.GetUnderlyingType(fieldType);
                    return ReadValueOfType(innerType, existing, value, options);
                }

                // Custom reifier
                if (CustomReifiers.TryGetValue(fieldType, out var fromDoc)) {
                    existing = fromDoc(existing, value);
                    return CallPostDoc(fieldType, existing);
                }

                // Arrays
                if (fieldType.IsArray) { 
                    int rank = fieldType.GetArrayRank();
                    var elementType = fieldType.GetElementType();
                    var arrayValue = existing as Array;
                    
                    if (rank == 1) { // simple arrays
                        if (value.Count == 0) {
                            return Array.CreateInstance(elementType, 0);
                        }
                        
                        if (arrayValue == null) {
                            arrayValue = Array.CreateInstance(elementType, value.Count);
                        } else if (arrayValue.Length != value.Count) {
                            // Copy the existing values to the new array so we can feed them
                            // in as existing values when reading array elements. 
                            var oldArr = arrayValue;
                            arrayValue = Array.CreateInstance(elementType, value.Count);
                            int numToCopy = Math.Min(oldArr.Length, arrayValue.Length);
                            Array.Copy(oldArr, arrayValue, numToCopy);
                        }

                        // Read the array values.
                        for (int a = 0; a < arrayValue.Length; a++) {
                            var existingElement = arrayValue.GetValue(a);
                            var updatedElement = ReadValueOfType(elementType, existingElement, value[a], options);
                            arrayValue.SetValue(updatedElement, a);
                        }
                    } else { // n-dimensional arrays
                        if (value.Count == 0) {
                            // Return a zero-length array of the correct dimensions. 
                            return Array.CreateInstance(elementType, new int[rank]);
                        }
                    
                        // Figure out the size of each dimension the array.
                        var lengths = new int[rank];
                        var currentArray = value;
                        for (int dimensionIndex = 0; dimensionIndex < rank; ++dimensionIndex) {
                            lengths[dimensionIndex] = currentArray.Count;
                            currentArray = currentArray[0];
                        }

                        int[] currentIndex;
                        
                        // Copy existing array data so they can be fed into ReadValueOfType
                        if (arrayValue != null) {
                            // Is the existing array the correct dimensions that we're reading from the config?
                            bool existingArrayDimensionsMatch = arrayValue.Rank == rank;
                            for (int i = 0; i < rank && existingArrayDimensionsMatch; ++i) {
                                if (arrayValue.GetLength(i) != lengths[i]) {
                                    existingArrayDimensionsMatch = false;
                                }
                            }

                            // If the dimensions don't match, we need to copy values over.
                            if (!existingArrayDimensionsMatch) {
                                var newArray = Array.CreateInstance(elementType, lengths);
                                currentIndex = new int[lengths.Length];
                                void CopyMultiDimensionalArray(int currentRank) {
                                    int numToCopy = Math.Min(arrayValue.GetLength(currentRank), newArray.GetLength(currentRank));

                                    for (int i = 0; i < numToCopy; ++i) {
                                        currentIndex[currentRank] = i;
                                        if (currentRank == rank - 1) {
                                            newArray.SetValue(arrayValue.GetValue(currentIndex), currentIndex);
                                        } else {
                                            CopyMultiDimensionalArray(currentRank + 1);
                                        }
                                    }
                                }
                                CopyMultiDimensionalArray(0);
                                arrayValue = newArray;                                
                            }
                        } else {
                            arrayValue = Array.CreateInstance(elementType, lengths);
                        }
                        
                        currentIndex = new int[lengths.Length];
                        void ReadArray(DocNode current, int currentRank) {
                            for (int i = 0; i < current.Count; ++i) {
                                currentIndex[currentRank] = i;
                                if (currentRank == rank - 1) {
                                    var existingElement = arrayValue.GetValue(currentIndex);
                                    var updatedElement = ReadValueOfType(elementType, existingElement, current[i], options);
                                    arrayValue.SetValue(updatedElement, currentIndex);
                                } else {
                                    ReadArray(current[i], currentRank + 1);
                                }
                            }
                        }
                        ReadArray(value, 0);
                    }
                    
                    return arrayValue;
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
                        var keyType = typeParameters[0];
                        var valueType = typeParameters[1];
                        var keyNode = new ComposedDocNode(DocNodeType.Scalar, sourceInformation: value.SourceInformation); // can reuse this one object
                        var usedKeys = new HashSet<object>();

                        // create/update all pairs in the doc
                        foreach (var kv in value.Pairs) {
                            keyNode.StringValue = kv.Key;
                            object existingKey = ReadValueOfType(keyType, null, keyNode, options);
                            object existingValue = null;
                            if (iexisting.Contains(existingKey)) {
                                existingValue = iexisting[existingKey];
                            }

                            var updated = ReadValueOfType(valueType, existingValue, kv.Value, options);
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
                        var typeParameters = fieldType.GetGenericArguments();
                        
                        if (existing == null) {
                            existing = Activator.CreateInstance(fieldType);
                        }
                        var iexisting = (System.Collections.IList) existing;
                        
                        while (iexisting.Count > value.Count) {
                            iexisting.RemoveAt(iexisting.Count - 1);
                        }

                        for (int i = 0; i < iexisting.Count; i++) {
                            iexisting[i] = ReadValueOfType(typeParameters[0], iexisting[i], value[i], options);
                        }

                        while (iexisting.Count < value.Count) {
                            iexisting.Add(ReadValueOfType(typeParameters[0], null, value[iexisting.Count], options));
                        }

                        return existing;
                    }
                }

                var fromDocMethod = ReflectionCache.GetTypeInfo(fieldType).FromDoc;
                if (fromDocMethod != null) {
                    // if there's a custom parser method on the class, delegate all work to that
                    // TODO: this doesn't do inherited FromDoc methods properly, but it should
                    try {
                        existing = fromDocMethod.Invoke(null, new[] {existing, value});
                        return CallPostDoc(fieldType, existing);
                    } catch (TargetInvocationException e) {
                        if (e.InnerException != null) {
                            throw e.InnerException;                            
                        }
                        throw;
                    }
                }

                if (fieldType.IsClass) {
                    if (existing == null) {
                        existing = Activator.CreateInstance(fieldType);
                    }
                    SetFieldsOnObject(fieldType, ref existing, value, options ?? Settings.DefaultReifierOptions);
                    return CallPostDoc(fieldType, existing);
                }

                if (fieldType.IsValueType) {
                    // a struct; set the members and return it
                    if (existing == null) { // structs can be null when boxed
                        existing = Activator.CreateInstance(fieldType);
                    }
                    SetFieldsOnObject(fieldType, ref existing, value, options ?? Settings.DefaultReifierOptions);
                    return CallPostDoc(fieldType, existing);
                }
            } catch (Exception e) {
                throw new ParseException($"Exception based on document starting at: {value.SourceInformation}", e);
            }

            throw new NotSupportedException($"Don't know how to update value of type {fieldType}");
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
        
        static void SetMember(MemberInfo memberInfo, bool isField, ref object obj, DocNode value, ConfigOptions? options) {
            if (isField) {
                var fieldInfo = (FieldInfo)memberInfo;
                if (obj == null && !fieldInfo.IsStatic) {
                    // silently don't set non-static fields
                    return;
                }
                object existing = fieldInfo.GetValue(obj);
                object updated = ReadValueOfType(fieldInfo.FieldType, existing, value, options);
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
                object updated = ReadValueOfType(propertyInfo.PropertyType, existing, value, options);
                object setCopy = obj; // needed for structs
                propertyInfo.SetValue(setCopy, updated);
                obj = setCopy;
            }
        }

        /// String.Join for Lists. Only used for logging.
        static string JoinList(IReadOnlyList<string> args, string joinStr) {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < args.Count; i++) {
                sb.Append(args[i]);
                if (i < args.Count - 1) {
                    sb.Append(joinStr);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Call a PostDoc method for the given object if one exists.  Returns the modified instance.
        /// </summary>
        /// <param name="serializedType"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        static object CallPostDoc(Type serializedType, object obj) {
            var postDoc = ReflectionCache.GetTypeInfo(serializedType).PostDoc;
            
            if (postDoc == null) {
                return obj;
            }
            
            try {
                return postDoc.Invoke(null, new[] {obj});
            } catch (TargetInvocationException e) {
                if (e.InnerException == null) {
                    throw;
                }
                throw e.InnerException;
            }
        }
    }
}