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
        /// Sets all members on an object from the values specified in the dict <paramref name="doc"/>.
        /// </summary>
        /// <param name="obj">The instance to set fields on.  Must not be null.</param>
        /// <param name="doc">The doc to read fields from. Must be a dictionary.</param>
        /// <param name="options">(optional) Reifier options</param>
        /// <typeparam name="T"></typeparam>
        public void SetFieldsOnObject<T>(ref T obj, DocNode doc, ReificationOptions? options = null) where T : class {
            if (obj == null) {
                throw new ArgumentNullException(nameof(obj));
            }
            
            // if T is object, that is not the real type so call GetType() to get the underlying type.
            var objType = typeof(T) == typeof(object) ? obj.GetType() : typeof(T);
            
            object setRef = obj;
            SetFieldsOnObject(objType, ref setRef, doc, options);
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
        void SetFieldsOnObject(Type type, ref object obj, DocNode doc, ReificationOptions? options = null) {
            if (type == null) { throw new ArgumentNullException(nameof(type)); }
            if (obj == null) { throw new ArgumentNullException(nameof(obj)); }
            if (doc == null) { throw new ArgumentNullException(nameof(doc)); }

            options ??= Configs.Settings.DefaultReifierOptions;

            var typeInfo = reflectionCache.GetTypeInfo(type);

            object setCopy = obj;
            if (doc.Type != DocNodeType.Dictionary) {
                // ==== Special Case ====
                // Allow specifying object types with a single property or field as a scalar value in configs.
                // This is syntactic sugar that lets us wrap values in classes.
                int targetMemberIndex = -1;
                bool foundMultipleEligible = false;
                for (int memberIndex = 0; memberIndex < typeInfo.MemberAttributes.Count; memberIndex++) {
                    var memberFlags = typeInfo.MemberAttributes[memberIndex];
                    
                    // Skip static fields and properties
                    if ((memberFlags & ReflectionCache.MemberFlags.Static) != 0) {
                        continue;
                    }

                    if (targetMemberIndex == -1) {
                        targetMemberIndex = memberIndex;
                    } else {
                        foundMultipleEligible = true;
                        break;
                    }
                }
                
                if (targetMemberIndex != -1 && !foundMultipleEligible) {
                    var targetMemberInfo = typeInfo.MemberInfo[targetMemberIndex];
                    if ((typeInfo.MemberAttributes[targetMemberIndex] & ReflectionCache.MemberFlags.Field) != 0) {
                        var fieldInfo = (FieldInfo) targetMemberInfo;
                        fieldInfo.SetValue(setCopy, ReadValueOfType(fieldInfo.FieldType, fieldInfo.GetValue(setCopy), doc, options));
                    } else {
                        var propertyInfo = (PropertyInfo) targetMemberInfo;
                        propertyInfo.SetValue(setCopy, ReadValueOfType(propertyInfo.PropertyType, propertyInfo.GetValue(setCopy), doc, options));
                    }
                    obj = setCopy;
                    return;
                }
                throw new Exception($"Trying to set a field of type: {type} {typeInfo.MemberNames.Count} from value of wrong type: " +
                    (doc.Type == DocNodeType.Scalar ? doc.StringValue : doc.Type.ToString()) + $" at {doc.SourceInformation}");
            }
            
            bool ignoreCase = (options & ReificationOptions.CaseSensitive) != ReificationOptions.CaseSensitive;
            List<int> setMemberHashes = null;

            // Set the fields on the object.
            for (int memberIndex = 0; memberIndex < typeInfo.MemberNames.Count; ++memberIndex) {
                var memberFlags = typeInfo.MemberAttributes[memberIndex];
                var memberInfo = typeInfo.MemberInfo[memberIndex];
                
                if ((memberFlags & ReflectionCache.MemberFlags.ConfigSourceInfo) != 0) {
                    // Special field to auto-populate with SourceInformation
                    if ((memberFlags & ReflectionCache.MemberFlags.Field) != 0) {
                        ((FieldInfo)memberInfo).SetValue(setCopy, doc.SourceInformation);
                    } else {
                        ((PropertyInfo)memberInfo).SetValue(setCopy, doc.SourceInformation);
                    }
                    continue;
                }

                // do meta stuff based on attributes/validation
                string key = typeInfo.MemberNames[memberIndex];
                
                if (doc.TryGetValue(key, ignoreCase, out var valueDoc)) {
                    if ((memberFlags & ReflectionCache.MemberFlags.Field) != 0) {
                        var fieldInfo = (FieldInfo) typeInfo.MemberInfo[memberIndex];
                        fieldInfo.SetValue(setCopy, ReadValueOfType(fieldInfo.FieldType, fieldInfo.GetValue(setCopy), valueDoc, options));
                    } else {
                        var propertyInfo = (PropertyInfo) typeInfo.MemberInfo[memberIndex];
                        propertyInfo.SetValue(setCopy, ReadValueOfType(propertyInfo.PropertyType, propertyInfo.GetValue(setCopy), valueDoc, options));
                    }
                    setMemberHashes ??= new List<int>();
                    setMemberHashes.Add((ignoreCase ? key.ToLowerInvariant() : key).GetHashCode());
                } else if (memberIndex >= typeInfo.NumRequired) {
                    // It's an optional field so pretend like we set it
                    setMemberHashes ??= new List<int>();
                    setMemberHashes.Add((ignoreCase ? key.ToLowerInvariant() : key).GetHashCode());
                }
            }

            // Check whether any fields in the doc were unused 
            if ((options & ReificationOptions.AllowExtraFields) == 0) {
                var extraDocFields = new List<string>();
                
                foreach (var kv in doc.Pairs) {
                    int docKeyHash = (ignoreCase ? kv.Key.ToLowerInvariant() : kv.Key).GetHashCode();
                    if (setMemberHashes == null || !setMemberHashes.Contains(docKeyHash)) {
                        extraDocFields.Add(kv.Key);
                    }
                }

                if (extraDocFields.Count > 0) {
                    throw new ExtraFieldsException($"Extra doc fields: {JoinList(extraDocFields, ", ")} {doc.SourceInformation}");
                }
            }

            // check whether any fields in the class were unset
            if (typeInfo.NumRequired > 0) {
                List<string> missingMembers = null;

                for (int memberIndex = 0; memberIndex < typeInfo.NumRequired; ++memberIndex) {
                    string memberName = typeInfo.MemberNames[memberIndex];
                    int memberNameHash = (ignoreCase ? memberName.ToLowerInvariant() : memberName).GetHashCode();
                    if (setMemberHashes != null && setMemberHashes.Contains(memberNameHash)) {
                        continue;
                    }

                    missingMembers ??= new List<string>();
                    missingMembers.Add(memberName);
                }

                if (missingMembers != null) {
                    throw new MissingFieldsException($"Missing doc fields: {JoinList(missingMembers, ", ")} {doc.SourceInformation}");
                }
            }

            obj = setCopy;
        }

        /// <summary>
        /// Sets all static fields and properties for a given type with the values in the given
        /// DocNode dictionary
        /// </summary>
        /// <param name="type">Set this type's static fields and properties</param>
        /// <param name="doc">Dictionary with static field and property data</param>
        /// <param name="options">Reification options</param>
        /// <exception cref="ExtraFieldsException">If more keys were present in the dictionary than static type members.</exception>
        /// <exception cref="MissingFieldsException">If one or more static members were missing from the config data.</exception>
        public void SetStaticFieldsOnClass(Type type, DocNode doc, ReificationOptions options) {
            bool ignoreCase = (options & ReificationOptions.CaseSensitive) != ReificationOptions.CaseSensitive;
            
            var typeInfo = reflectionCache.GetTypeInfo(type);
            List<int> setMemberHashes = null;

            // Set all the static fields and properties
            for (int memberIndex = 0; memberIndex < typeInfo.MemberNames.Count; ++memberIndex) {
                var memberFlags = typeInfo.MemberAttributes[memberIndex];
                if ((memberFlags & ReflectionCache.MemberFlags.Static) == 0) {
                    continue;
                }
                
                var memberInfo = typeInfo.MemberInfo[memberIndex];
                
                // Special field to auto-populate with SourceInformation
                if ((memberFlags & ReflectionCache.MemberFlags.ConfigSourceInfo) != 0) {
                    if ((memberFlags & ReflectionCache.MemberFlags.Field) != 0) {
                        ((FieldInfo) memberInfo).SetValue(null, doc.SourceInformation);
                    } else {
                        ((PropertyInfo) memberInfo).SetValue(null, doc.SourceInformation);
                    }
                    continue;
                }

                // do meta stuff based on attributes/validation
                string memberName = typeInfo.MemberNames[memberIndex];

                if (doc.TryGetValue(memberName, ignoreCase, out var valueDoc)) {
                    if ((memberFlags & ReflectionCache.MemberFlags.Field) != 0) {
                        var fieldInfo = (FieldInfo) memberInfo;
                        object newValue = ReadValueOfType(fieldInfo.FieldType, fieldInfo.GetValue(null), valueDoc, options);
                        fieldInfo.SetValue(null, newValue);
                    } else {
                        var propertyInfo = (PropertyInfo) memberInfo;
                        propertyInfo.SetValue(null, ReadValueOfType(propertyInfo.PropertyType, propertyInfo.GetValue(null), valueDoc, options));
                    }
                    setMemberHashes ??= new List<int>();
                    setMemberHashes.Add((ignoreCase ? memberName.ToLowerInvariant() : memberName).GetHashCode());
                } else if (memberIndex >= typeInfo.NumRequired) {
                    // It's an optional field so pretend like we set it
                    setMemberHashes ??= new List<int>();
                    setMemberHashes.Add((ignoreCase ? memberName.ToLowerInvariant() : memberName).GetHashCode());
                }
            }
            
            // Check whether any fields in the doc were unused 
            if ((options & ReificationOptions.AllowExtraFields) == 0) {
                var extraDocFields = new List<string>();
                
                foreach (var kv in doc.Pairs) {
                    int docKeyHash = (ignoreCase ? kv.Key.ToLowerInvariant() : kv.Key).GetHashCode();
                    if (setMemberHashes == null || !setMemberHashes.Contains(docKeyHash)) {
                        extraDocFields.Add(kv.Key);
                    }
                }

                if (extraDocFields.Count > 0) {
                    throw new ExtraFieldsException($"Extra doc fields: {JoinList(extraDocFields, ", ")} {doc.SourceInformation}");
                }
            }

            // check whether any fields in the class were unset
            if (typeInfo.NumRequired > 0) {
                List<string> missingMembers = null;

                for (int memberIndex = 0; memberIndex < typeInfo.NumRequired; ++memberIndex) {
                    var memberFlags = typeInfo.MemberAttributes[memberIndex];
                    if ((memberFlags & ReflectionCache.MemberFlags.Static) == 0) {
                        continue;
                    }

                    string memberName = typeInfo.MemberNames[memberIndex];
                    int memberNameHash = (ignoreCase ? memberName.ToLowerInvariant() : memberName).GetHashCode();
                    if (setMemberHashes != null && setMemberHashes.Contains(memberNameHash)) {
                        continue;
                    }

                    missingMembers ??= new List<string>();
                    missingMembers.Add(memberName);
                }

                if (missingMembers != null) {
                    throw new MissingFieldsException($"Missing doc fields: {JoinList(missingMembers, ", ")} {doc.SourceInformation}");
                }
            }
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
            bool allowExtra = (options & ReificationOptions.AllowExtraFields) == ReificationOptions.AllowExtraFields;

            for (int memberIndex = 0; memberIndex < typeInfo.MemberNames.Count; ++memberIndex) {
                if (!string.Equals(typeInfo.MemberNames[memberIndex], fieldName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (doc.TryGetValue(fieldName, !caseSensitive, out var node)) {
                    if ((typeInfo.MemberAttributes[memberIndex] & ReflectionCache.MemberFlags.Field) != 0) {
                        var fieldInfo = (FieldInfo) typeInfo.MemberInfo[memberIndex];
                        fieldInfo.SetValue(obj, ReadValueOfType(fieldInfo.FieldType, fieldInfo.GetValue(obj), node, options));
                    } else {
                        var propertyInfo = (PropertyInfo) typeInfo.MemberInfo[memberIndex];
                        propertyInfo.SetValue(obj, ReadValueOfType(propertyInfo.PropertyType, propertyInfo.GetValue(obj), node, options));
                    }
                    return true;
                }
                
                // Specifying member is mandatory
                if (memberIndex < typeInfo.NumRequired) {
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
        /// <param name="targetType">Type of the value to read</param>
        /// <param name="existing">An existing instance of that type to update, or null.</param>
        /// <param name="doc">The DocNode containing the value's data</param>
        /// <param name="options">Reification options</param>
        /// <returns>An updated version of <paramref name="existing"/> if it was not null,
        /// or a new instance of <paramref name="targetType"/> containing new data from <paramref name="doc"/></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ParseException"></exception>
        public object ReadValueOfType(Type targetType, object existing, DocNode doc, ReificationOptions? options) {
            try {
                #region Atomic data types
                if (targetType == typeof(bool)) { return Convert.ToBoolean(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }

                // floating-point value types
                if (targetType == typeof(float)) { return Convert.ToSingle(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }
                if (targetType == typeof(double)) { return Convert.ToDouble(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }
                if (targetType == typeof(decimal)) { return Convert.ToDecimal(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }

                // integral value types
                if (targetType == typeof(sbyte)) { return Convert.ToSByte(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }
                if (targetType == typeof(byte)) { return Convert.ToByte(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }
                if (targetType == typeof(char)) { return Convert.ToChar(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }
                if (targetType == typeof(short)) { return Convert.ToInt16(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }
                if (targetType == typeof(ushort)) { return Convert.ToUInt16(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }
                if (targetType == typeof(int)) { return Convert.ToInt32(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }
                if (targetType == typeof(uint)) { return Convert.ToUInt32(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }
                if (targetType == typeof(long)) { return Convert.ToInt64(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }
                if (targetType == typeof(ulong)) { return Convert.ToUInt64(doc.StringValue, System.Globalization.CultureInfo.InvariantCulture); }

                // String type
                if (targetType == typeof(string)) { return doc.StringValue; }
                #endregion
                
                // Enums
                if (targetType.IsEnum) {
                    return Enum.Parse(targetType, doc.StringValue, (options & ReificationOptions.CaseSensitive) == 0);
                }
                
                // DocNode
                if (targetType == typeof(DocNode)) {
                    return doc;
                }

                // Custom manually-registered reifier. These are allowed to override default parsing behavior.
                if (RegisteredFromDocs.TryGetValue(targetType, out var fromDoc)) {
                    existing = fromDoc(existing, doc);
                    // CallPostDoc(fieldType, ref existing);
                    return existing;
                }

                #region Arrays
                if (targetType.IsArray) { 
                    int rank = targetType.GetArrayRank();
                    var elementType = targetType.GetElementType();
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
                #endregion

                #region Generic Collections
                if (targetType.IsGenericType) {
                    // this chunk of code handles generic dictionaries and lists; it only
                    // works with string keys on the dictionaries, and for now any values
                    // must have zero-args constructors

                    var genericTypeDef = targetType.GetGenericTypeDefinition();
                    
                    // Dictionary<K,V>
                    if (genericTypeDef == typeof(Dictionary<,>)) {
                        existing ??= Activator.CreateInstance(targetType);
                        var existingDict = (System.Collections.IDictionary) existing;
                        
                        var typeParameters = targetType.GetGenericArguments();
                        var keyType = typeParameters[0];
                        var valueType = typeParameters[1];
                        var keyNode = new ComposedDocNode(DocNodeType.Scalar, sourceInformation: doc.SourceInformation); // can reuse this one object
                        var readKeyHashes = new List<int>();

                        // create/update all pairs in the doc
                        foreach ((string docKey, var docValue) in doc.Pairs) {
                            keyNode.StringValue = docKey;
                            object readKey = ReadValueOfType(keyType, null, keyNode, options);
                            object existingValue = existingDict.Contains(readKey) ? existingDict[readKey] : null;
                            existingDict[readKey] = ReadValueOfType(valueType, existingValue, docValue, options);
                            readKeyHashes.Add(readKey.GetHashCode());
                        }

                        // remove any key value pairs not in the doc
                        var keysToRemove = new List<object>();
                        foreach (object key in existingDict.Keys) {
                            if (!readKeyHashes.Contains(key.GetHashCode())) {
                                keysToRemove.Add(key);
                            }
                        }
                        foreach (object keyToRemove in keysToRemove) {
                            existingDict.Remove(keyToRemove);
                        }

                        return existingDict;
                    }

                    // List<T>
                    if (genericTypeDef == typeof(List<>)) {
                        var listElementType = targetType.GetGenericArguments()[0];

                        existing ??= Activator.CreateInstance(targetType);
                        var existingList = (System.Collections.IList) existing;

                        // Remove any extra existing slots we won't need.
                        while (existingList.Count > doc.Count) {
                            existingList.RemoveAt(existingList.Count - 1);
                        }

                        // Copy over values into existing slots
                        for (int i = 0; i < existingList.Count; i++) {
                            existingList[i] = ReadValueOfType(listElementType, existingList[i], doc[i], options);
                        }

                        // If there aren't enough slots, keep reading values and adding them to new slots.
                        while (existingList.Count < doc.Count) {
                            existingList.Add(ReadValueOfType(listElementType, null, doc[existingList.Count], options));
                        }

                        return existing;
                    }

                    // HashSet<T>
                    if (genericTypeDef == typeof(HashSet<>)) {
                        var setEntryType = targetType.GetGenericArguments()[0];

                        if (existing != null) {
                            // There's no way to migrate existing data since changing any existing data in the set constitutes a new, different set element by definition.
                            // ...so just call Clear on it if necessary.
                            targetType.GetMethod("Clear")?.Invoke(existing, null);
                        } else {
                            existing = Activator.CreateInstance(targetType);
                        }

                        // HashSet<> has no generic-less object interface we can use, so use reflection to add elements
                        var addMethod = targetType.GetMethod("Add");
                        foreach (var value in doc.Values) {
                            addMethod?.Invoke(existing, new[] { ReadValueOfType(setEntryType, null, value, options) });
                        }

                        return existing;
                    }
                    
                    // Nullable<T>
                    if (genericTypeDef == typeof(Nullable<>)) {
                        var comparisonOptions = (options & ReificationOptions.CaseSensitive) == 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        if (doc.Type == DocNodeType.Scalar && string.Equals(doc.StringValue ,"null", comparisonOptions)) {
                            return null;
                        }
                        var innerType = Nullable.GetUnderlyingType(targetType);
                        return ReadValueOfType(innerType, existing, doc, options);
                    }
                }
                #endregion

                var typeInfo = reflectionCache.GetTypeInfo(targetType);
                
                // Call a FromDoc method if one exists in the type
                if (typeInfo.FromDoc != null) {
                    // if there's a custom parser method on the class, delegate all work to that
                    // TODO: this doesn't do inherited FromDoc methods properly, but it should
                    try {
                        // TODO (graham): This doesn't pass through reification options (https://github.com/SpryFox/DarkConfig/issues/48)
                        existing = typeInfo.FromDoc.Invoke(null, new[] {existing, doc});
                    } catch (TargetInvocationException e) {
                        if (e.InnerException != null) {
                            throw e.InnerException;                            
                        }
                        throw;
                    }
                } else {
                    existing ??= Activator.CreateInstance(targetType);
                    SetFieldsOnObject(targetType, ref existing, doc, options);
                }
                
                // Call a PostDoc function for this type if it exists.
                if (typeInfo.PostDoc != null) {
                    try {
                        existing = typeInfo.PostDoc.Invoke(null, new[] {existing});
                    } catch (TargetInvocationException e) {
                        if (e.InnerException == null) {
                            throw;
                        }
                        throw e.InnerException;
                    }
                }
                
                return existing;
            } catch (Exception e) {
                throw new ParseException($"Exception based on document starting at: {doc.SourceInformation}", e);
            }
        }
        
        /////////////////////////////////////////////////

        readonly ReflectionCache reflectionCache = new ReflectionCache();

        /////////////////////////////////////////////////

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
    }
}
