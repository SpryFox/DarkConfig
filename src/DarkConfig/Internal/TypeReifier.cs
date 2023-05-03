using System;
using System.Collections.Generic;
using System.Reflection;

namespace DarkConfig.Internal {
    public class TypeReifier {
        /// Manually-registered FromDoc's
        public readonly Dictionary<Type, FromDocFunc> RegisteredFromDocs = new Dictionary<Type, FromDocFunc>();

        /////////////////////////////////////////////////
        
        public TypeReifier() {
            RegisteredFromDocs[typeof(DateTime)] = BuiltInTypeReifiers.FromDateTime;
            RegisteredFromDocs[typeof(TimeSpan)] = BuiltInTypeReifiers.FromTimeSpan;
        }

        /// <summary>
        /// Sets all members on a struct from the given dictionary DocNode
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="doc">The doc to read fields from.  Must be a dictionary.</param>
        /// <param name="options"></param>
        /// <typeparam name="T"></typeparam>
        public void SetFieldsOnStruct<T>(ref T obj, DocNode doc, ReificationOptions? options = null) where T : struct {
            var type = typeof(T);
            object setRef = obj;
            SetFieldsOnObject(type, ref setRef, doc, options);
            obj = (T) setRef;
        }

        /// <summary>
        /// Sets all members on the object *obj* (which must not be null) from *dict*.
        /// Expects *obj* to be a plain class, but if it's a boxed struct it will work as well.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="doc">The doc to read fields from.  Must be a dictionary.</param>
        /// <param name="options">(optional) Reifier options</param>
        /// <typeparam name="T"></typeparam>
        public void SetFieldsOnObject<T>(ref T obj, DocNode doc, ReificationOptions? options = null) where T : class {
            Configs.Assert(obj != null, "Can't SetFields on null");
            var type = typeof(T);
            if (type == typeof(object)) {
                // caller is using an object, but that is not the real type
                type = obj.GetType();
            }

            object setRef = obj;
            SetFieldsOnObject(type, ref setRef, doc, options);
            obj = (T) setRef;
        }

        /// <summary>
        /// Sets all members on the object obj based on the appropriate key from doc.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="obj"></param>
        /// <param name="doc"></param>
        /// <param name="options"></param>
        /// <exception cref="ExtraFieldsException"></exception>
        /// <exception cref="MissingFieldsException"></exception>
        public void SetFieldsOnObject(Type type, ref object obj, DocNode doc, ReificationOptions? options = null) {
            if (doc == null) {
                return;
            }

            if (options == null) {
                options = Configs.Settings.DefaultReifierOptions;
            }

            var typeInfo = reflectionCache.GetTypeInfo(type);

            // Grab global settings
            bool ignoreCase = (options & ReificationOptions.CaseSensitive) != ReificationOptions.CaseSensitive;
            bool checkForMissingFields = (options & ReificationOptions.AllowMissingFields) != ReificationOptions.AllowMissingFields;
            bool checkForExtraFields = (options & ReificationOptions.AllowExtraFields) != ReificationOptions.AllowExtraFields;
            
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

            object setCopy = obj;
            if (doc.Type != DocNodeType.Dictionary) {
                // ==== Special Case ====
                // Allow specifying object types with a single property or field as a scalar value in configs.
                // This is syntactic sugar that lets us wrap values in classes.
                int targetMemberIndex = -1;
                bool foundMultipleEligible = false;
                for (int memberIndex = 0; memberIndex < typeInfo.Members.Count; memberIndex++) {
                    var memberMetadata = typeInfo.Members[memberIndex];
                    // Only consider non-static members, unless we are using static reification
                    if (obj == null || !memberMetadata.IsField || !((FieldInfo)memberMetadata.Info).IsStatic) {
                        if (targetMemberIndex == -1) {
                            targetMemberIndex = memberIndex;
                        } else {
                            foundMultipleEligible = true;
                            break;
                        }
                    }
                }
                if (targetMemberIndex != -1 && !foundMultipleEligible) {
                    var memberMetadata = typeInfo.Members[targetMemberIndex];
                    SetMember(memberMetadata.Info, memberMetadata.IsField, ref setCopy, doc, options);
                    obj = setCopy;
                    return;
                } else {
                    throw new Exception($"Trying to set a field of type: {type} {typeInfo.Members.Count} from value of wrong type: " +
                        (doc.Type == DocNodeType.Scalar ? doc.StringValue : doc.Type.ToString()) + $" at {doc.SourceInformation}");
                }
            }

            var requiredMembers = new List<string>();
            var setMembers = new List<string>();

            bool isAnyFieldMandatory = false;

            // Set the fields on the object.
            foreach (var memberMetadata in typeInfo.Members) {
                // never report delegates or events as present or missing
                if (IsDelegateType(memberMetadata.Type)) {
                    continue;
                }
                
                if (memberMetadata.HasConfigSourceInformationAttribute) {
                    // Special field to auto-populate with SourceInformation
                    if (memberMetadata.Type != typeof(string)) {
                        throw new Exception("Field with ConfigSourceInformation should be a string");
                    }
                    SetMember(memberMetadata.Info, memberMetadata.IsField, ref setCopy, doc.SourceInformation, options);
                    continue;
                }
                
                // Override global and class settings per-field.
                bool memberIsMandatory = memberMetadata.HasConfigMandatoryAttribute;
                bool memberAllowMissing = memberMetadata.HasConfigAllowMissingAttribute;
                isAnyFieldMandatory |= memberIsMandatory;
                
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

                    bool IsExtraField = true;
                    foreach (string setMember in setMembers) {
                        if (string.Equals(setMember, docKey, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                            IsExtraField = false;
                            break;
                        }
                    }

                    if (IsExtraField) {
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
        /// Sets a single field value on an object from the given docnode dict.
        /// 
        /// This is mostly only useful as a helper when writing FromDoc's
        /// </summary>
        /// <param name="obj">The object to set the field on</param>
        /// <param name="fieldName">The name of the field to set</param>
        /// <param name="doc">A yaml dictionary to grab the value from</param>
        /// <param name="options">Reification options override</param>
        /// <typeparam name="T">The type of <paramref name="obj"/></typeparam>
        /// <returns>true if we successfully set the field, false otherwise</returns>
        /// <exception cref="ExtraFieldsException">If the field does not exist as a member of <typeparamref name="T"/> and extra fields are disallowed</exception>
        /// <exception cref="MissingFieldsException">If the field is marked as mandatory and is missing in the yaml doc</exception>
        public bool SetFieldOnObject<T>(ref T obj, string fieldName, DocNode doc, ReificationOptions? options = null) where T : class {
            object setCopy = obj;
            bool containedField = SetFieldOnObject(typeof(T), ref setCopy, fieldName, doc, options);
            obj = (T)setCopy;
            return containedField;
        }

        /// <summary>
        /// Sets a single field value on an object from the given docnode dict.
        ///
        /// This is mostly only useful as a helper when writing FromDoc's
        /// </summary>
        /// <param name="type">The type of obj</param>
        /// <param name="obj">The object to set the field on</param>
        /// <param name="fieldName">The name of the field to set</param>
        /// <param name="doc">A yaml dictionary to grab the value from</param>
        /// <param name="options">Reification options override</param>
        /// <returns>true if we successfully set the field, false otherwise</returns>
        /// <exception cref="ExtraFieldsException">If the field does not exist as a member of <paramref name="type"/> and extra fields are disallowed</exception>
        /// <exception cref="MissingFieldsException">If the field is marked as mandatory and is missing in the yaml doc</exception>
        public bool SetFieldOnObject(Type type, ref object obj, string fieldName, DocNode doc, ReificationOptions? options = null) {
            if (doc == null) {
                return false;
            }

            var typeInfo = reflectionCache.GetTypeInfo(type);

            options ??= Configs.Settings.DefaultReifierOptions;

            // Grab global settings
            bool caseSensitive = (options & ReificationOptions.CaseSensitive) == ReificationOptions.CaseSensitive;
            bool allowMissing = (options & ReificationOptions.AllowMissingFields) == ReificationOptions.AllowMissingFields;
            bool allowExtra = (options & ReificationOptions.AllowExtraFields) == ReificationOptions.AllowExtraFields;

            // Override global settings with type-specific settings
            if ((typeInfo.AttributeFlags & ReflectionCache.ClassAttributesFlags.HasConfigMandatoryAttribute) != 0) {
                allowMissing = true;
            }
            if ((typeInfo.AttributeFlags & ReflectionCache.ClassAttributesFlags.HasConfigAllowMissingAttribute) != 0) {
                allowMissing = false;
            }

            if (GetFirstNonObjectBaseClass(type).ToString() == "UnityEngine.Object") {
                // Unity Objects have a lot of fields, it never makes sense to set most of them from configs
                allowMissing = false;
            }

            foreach (var memberMetadata in typeInfo.Members) {
                if (!string.Equals(memberMetadata.ShortName, fieldName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                
                // Override global and class settings per-field.
                bool missingIsOk = memberMetadata.HasConfigAllowMissingAttribute || (allowMissing && !memberMetadata.HasConfigMandatoryAttribute);

                // never report delegates or events as present or missing
                if (IsDelegateType(memberMetadata.Type)) {
                    return false;
                }

                if (doc.TryGetValue(fieldName, !caseSensitive, out var node)) {
                    SetMember(memberMetadata.Info, memberMetadata.IsField, ref obj, node, options);
                    return true;
                }
                
                if (!missingIsOk) {
                    throw new MissingFieldsException($"Missing doc field: {fieldName} {doc.SourceInformation}");
                }

                return false;
            }

            if (!allowExtra) {
                throw new ExtraFieldsException($"Extra doc fields: {fieldName} {doc.SourceInformation}");
            }

            return false;
        }

        /// <summary>
        /// Sets a single field value on a struct from the given docnode dict.
        ///
        /// This is mostly only useful as a helper when writing FromDoc's
        /// </summary>
        /// <param name="obj">The struct to set the field on</param>
        /// <param name="fieldName">The name of the field to set</param>
        /// <param name="doc">A yaml dictionary to grab the value from</param>
        /// <param name="options">Reification options override</param>
        /// <typeparam name="T">The type of <paramref name="obj"/></typeparam>
        /// <exception cref="ExtraFieldsException">If the field does not exist as a member of <typeparamref name="T"/> and extra fields are disallowed</exception>
        /// <exception cref="MissingFieldsException">If the field is marked as mandatory and is missing in the yaml doc</exception>
        public void SetFieldOnStruct<T>(ref T obj, string fieldName, DocNode doc, ReificationOptions? options = null) where T : struct {
            var type = typeof(T);
            object setRef = obj;
            SetFieldOnObject(type, ref setRef, fieldName, doc, options);
            obj = (T)setRef;
        }

        /// <summary>
        /// Reads the given doc node and converts it to an instance of the given type.
        /// </summary>
        /// <param name="fieldType"></param>
        /// <param name="existing"></param>
        /// <param name="doc"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ParseException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public object ReadValueOfType(Type fieldType, object existing, DocNode doc, ReificationOptions? options) {
            try {
                if (fieldType == typeof(bool)) {
                    return Convert.ToBoolean(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                // floating-point value types
                if (fieldType == typeof(float)) {
                    return Convert.ToSingle(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(double)) {
                    return Convert.ToDouble(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(decimal)) {
                    return Convert.ToDecimal(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                // integral value types
                if (fieldType == typeof(sbyte)) {
                    return Convert.ToSByte(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(byte)) {
                    return Convert.ToByte(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(char)) {
                    return Convert.ToChar(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(short)) {
                    return Convert.ToInt16(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(ushort)) {
                    return Convert.ToUInt16(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(int)) {
                    return Convert.ToInt32(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(uint)) {
                    return Convert.ToUInt32(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(long)) {
                    return Convert.ToInt64(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (fieldType == typeof(ulong)) {
                    return Convert.ToUInt64(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }

                // String type
                if (fieldType == typeof(string)) {
                    return doc.StringValue;
                }

                // Enum
                if (fieldType.IsEnum) {
                    return Enum.Parse(fieldType, doc.StringValue, true);
                }

                // Nullable generic type
                if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                    if (doc.Type == DocNodeType.Scalar && doc.StringValue == "null") {
                        return null;
                    }
                    var innerType = Nullable.GetUnderlyingType(fieldType);
                    return ReadValueOfType(innerType, existing, doc, options);
                }

                // Custom reifier
                if (RegisteredFromDocs.TryGetValue(fieldType, out var fromDoc)) {
                    existing = fromDoc(existing, doc);
                    CallPostDoc(fieldType, ref existing);
                    return existing;
                }

                // DocNode
                if (fieldType == typeof(DocNode)) {
                    return doc;
                }

                // Arrays
                if (fieldType.IsArray) { 
                    int rank = fieldType.GetArrayRank();
                    var elementType = fieldType.GetElementType();
                    var arrayValue = existing as Array;

                    if (elementType == null) {
                        throw new Exception("Null element type for array.");
                    }
                    
                    if (rank == 1) { // simple arrays
                        if (doc.Count == 0) {
                            return Array.CreateInstance(elementType, 0);
                        }
                        
                        if (arrayValue == null) {
                            arrayValue = Array.CreateInstance(elementType, doc.Count);
                        } else if (arrayValue.Length != doc.Count) {
                            // Copy the existing values to the new array so we can feed them
                            // in as existing values when reading array elements. 
                            var oldArr = arrayValue;
                            arrayValue = Array.CreateInstance(elementType, doc.Count);
                            int numToCopy = Math.Min(oldArr.Length, arrayValue.Length);
                            Array.Copy(oldArr, arrayValue, numToCopy);
                        }

                        // Read the array values.
                        for (int a = 0; a < arrayValue.Length; a++) {
                            var existingElement = arrayValue.GetValue(a);
                            var updatedElement = ReadValueOfType(elementType, existingElement, doc[a], options);
                            arrayValue.SetValue(updatedElement, a);
                        }
                    } else { // n-dimensional arrays
                        if (doc.Count == 0) {
                            // Return a zero-length array of the correct dimensions. 
                            return Array.CreateInstance(elementType, new int[rank]);
                        }
                    
                        // Figure out the size of each dimension the array.
                        var lengths = new int[rank];
                        var currentArray = doc;
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
                        ReadArray(doc, 0);
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

                        var existingDict = (System.Collections.IDictionary) existing;
                        var keyType = typeParameters[0];
                        var valueType = typeParameters[1];
                        var keyNode = new ComposedDocNode(DocNodeType.Scalar, sourceInformation: doc.SourceInformation); // can reuse this one object
                        var usedKeys = new HashSet<object>();

                        // create/update all pairs in the doc
                        foreach (var kv in doc.Pairs) {
                            keyNode.StringValue = kv.Key;
                            object existingKey = ReadValueOfType(keyType, null, keyNode, options);
                            object existingValue = null;
                            if (existingDict.Contains(existingKey)) {
                                existingValue = existingDict[existingKey];
                            }

                            var updated = ReadValueOfType(valueType, existingValue, kv.Value, options);
                            existingDict[existingKey] = updated;
                            usedKeys.Add(existingKey);
                        }

                        // remove any pairs not in the doc
                        var keysToRemove = new List<object>();
                        foreach (var k in existingDict.Keys) {
                            if (!usedKeys.Contains(k)) {
                                keysToRemove.Add(k);
                            }
                        }

                        foreach (var k in keysToRemove) {
                            existingDict.Remove(k);
                        }

                        return existingDict;
                    }

                    if (fieldType.GetGenericTypeDefinition() == typeof(List<>)) {
                        var typeParameters = fieldType.GetGenericArguments();
                        
                        if (existing == null) {
                            existing = Activator.CreateInstance(fieldType);
                        }
                        
                        var existingList = (System.Collections.IList) existing;
                        
                        while (existingList.Count > doc.Count) {
                            existingList.RemoveAt(existingList.Count - 1);
                        }

                        for (int i = 0; i < existingList.Count; i++) {
                            existingList[i] = ReadValueOfType(typeParameters[0], existingList[i], doc[i], options);
                        }

                        while (existingList.Count < doc.Count) {
                            existingList.Add(ReadValueOfType(typeParameters[0], null, doc[existingList.Count], options));
                        }

                        return existing;
                    }

                    if (fieldType.GetGenericTypeDefinition() == typeof(HashSet<>)) {
                        var typeParameters = fieldType.GetGenericArguments();

                        if (existing == null) {
                            existing = Activator.CreateInstance(fieldType);
                        } else {
                            // ideally we would try and keep any exising values and update them, but that is hard to do meaningfully for sets, so just rebuild it entirely
                            fieldType.GetMethod("Clear").Invoke(existing, null);
                        }

                        // HashSet<> has no generic-less object interface we can use, so use reflection to add elements
                        var addMethod = fieldType.GetMethod("Add");
                        foreach (var value in doc.Values) {
                            var elem = ReadValueOfType(typeParameters[0], null, value, options);
                            addMethod.Invoke(existing, new object[] { elem });
                        }

                        return existing;
                    }
                }

                var typeInfo = reflectionCache.GetTypeInfo(fieldType);
                var fromDocMethod = typeInfo.FromDoc;
                if (fromDocMethod != null) {
                    // if there's a custom parser method on the class, delegate all work to that
                    // TODO: this doesn't do inherited FromDoc methods properly, but it should
                    try {
                        existing = fromDocMethod.Invoke(null, new[] {existing, doc});
                    } catch (TargetInvocationException e) {
                        if (e.InnerException != null) {
                            throw e.InnerException;                            
                        }
                        throw;
                    }
                    CallPostDoc(fieldType, ref existing, typeInfo);
                    return existing;
                }

                if (fieldType.IsClass) {
                    if (existing == null) {
                        existing = Activator.CreateInstance(fieldType);
                    }
                    SetFieldsOnObject(fieldType, ref existing, doc, options);
                    CallPostDoc(fieldType, ref existing, typeInfo);
                    return existing;
                }

                if (fieldType.IsValueType) {
                    // a struct; set the members and return it
                    if (existing == null) { // structs can be null when boxed
                        existing = Activator.CreateInstance(fieldType);
                    }
                    SetFieldsOnObject(fieldType, ref existing, doc, options);
                    CallPostDoc(fieldType, ref existing, typeInfo);
                    return existing;
                }
            } catch (Exception e) {
                throw new ParseException($"Exception based on document starting at: {doc.SourceInformation}", e);
            }

            throw new NotSupportedException($"Don't know how to update value of type {fieldType}");
        }
        
        /////////////////////////////////////////////////

        readonly ReflectionCache reflectionCache = new ReflectionCache();

        /////////////////////////////////////////////////

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
        
        void SetMember(MemberInfo memberInfo, bool isField, ref object obj, DocNode doc, ReificationOptions? options) {
            if (isField) {
                var fieldInfo = (FieldInfo)memberInfo;
                if (obj == null && !fieldInfo.IsStatic) {
                    // silently don't set non-static fields
                    return;
                }
                object existing = fieldInfo.GetValue(obj);
                object updated = ReadValueOfType(fieldInfo.FieldType, existing, doc, options);
                SetMember(memberInfo, isField, ref obj, updated, options);
            } else {
                var propertyInfo = (PropertyInfo)memberInfo;
                if (obj == null && !propertyInfo.CanWrite) {
                    // silently don't set non-static fields
                    return;
                }
                object existing = propertyInfo.GetValue(obj);
                object updated = ReadValueOfType(propertyInfo.PropertyType, existing, doc, options);
                SetMember(memberInfo, isField, ref obj, updated, options);
            }
        }

        void SetMember(MemberInfo memberInfo, bool isField, ref object obj, object updated, ReificationOptions? options) {
            if (isField) {
                var fieldInfo = (FieldInfo)memberInfo;
                if (obj == null && !fieldInfo.IsStatic) {
                    // silently don't set non-static fields
                    return;
                }
                object setCopy = obj; // needed for structs
                fieldInfo.SetValue(setCopy, updated);
                obj = setCopy;                
            } else {
                var propertyInfo = (PropertyInfo)memberInfo;
                if (obj == null && !propertyInfo.CanWrite) {
                    // silently don't set non-static fields
                    return;
                }
                object setCopy = obj; // needed for structs
                propertyInfo.SetValue(setCopy, updated);
                obj = setCopy;
            }
        }

        /// String.Join for Lists. Only used for logging.
        string JoinList(IReadOnlyList<string> args, string joinStr) {
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
        /// <param name="obj">the object to call postdoc on</param>
        /// <param name="typeInfo">(optional) the reflection type info so we don't have to get it again from the reflection cache</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        void CallPostDoc(Type serializedType, ref object obj, ReflectionCache.TypeInfo typeInfo = null) {
            if (typeInfo == null) {
                typeInfo = reflectionCache.GetTypeInfo(serializedType);
            }
            
            var postDoc = typeInfo.PostDoc;
            if (postDoc == null) {
                return;
            }
            
            try {
                obj = postDoc.Invoke(null, new[] {obj});
            } catch (TargetInvocationException e) {
                if (e.InnerException == null) {
                    throw;
                }
                throw e.InnerException;
            }
        }
    }
}
