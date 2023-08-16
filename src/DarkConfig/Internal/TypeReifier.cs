using System;
using System.Collections.Generic;
using System.Reflection;

namespace DarkConfig.Internal {
    public class TypeReifier {
        /// Manually-registered FromDoc's
        public readonly Dictionary<Type, FromDocFunc> RegisteredFromDocs = new Dictionary<Type, FromDocFunc>();

        /// Manually-registered PostDoc's
        public readonly Dictionary<Type, PostDocFunc> RegisteredPostDocs = new Dictionary<Type, PostDocFunc>();

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
            if (doc == null) { throw new ArgumentNullException(nameof(doc)); }

            // Manually box the struct value and then put it through the normal object code path.
            object setRef = obj;
            SetFieldsOnObject(typeof(T), ref setRef, doc, options);
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
            if (obj == null) { throw new ArgumentNullException(nameof(obj)); }
            if (doc == null) { throw new ArgumentNullException(nameof(doc)); }

            // if T is object, that is not the real type so call GetType() to get the underlying type.
            var objType = typeof(T) == typeof(object) ? obj.GetType() : typeof(T);

            // We need setRef here because refs are not covariant
            object setRef = obj;
            SetFieldsOnObject(objType, ref setRef, doc, options);
            obj = (T) setRef;
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
        public void SetStaticMembers(Type type, DocNode doc, ReificationOptions options) {
            bool ignoreCase = (options & ReificationOptions.CaseSensitive) != ReificationOptions.CaseSensitive;

            var typeInfo = reflectionCache.GetTypeInfo(type);

            List<string> missingRequiredMemberNames = null;
            var setMemberHashes = new List<int>();

            // Set static fields.
            for (int fieldIndex = 0; fieldIndex < typeInfo.StaticFieldNames.Count; fieldIndex++) {
                string fieldName = typeInfo.StaticFieldNames[fieldIndex];
                var fieldInfo = typeInfo.StaticFieldInfos[fieldIndex];

                if (!doc.TryGetValue(fieldName, ignoreCase, out var valueDoc)) {
                    if (fieldIndex < typeInfo.NumRequiredStaticFields) {
                        missingRequiredMemberNames ??= new List<string>();
                        missingRequiredMemberNames.Add(fieldName);
                    }
                    continue;
                }

                object newValue = typeInfo.SourceInfoMemberName == fieldName ? doc.SourceInformation
                    : ReadValueOfType(fieldInfo.FieldType, fieldInfo.GetValue(null), valueDoc, options);
                setMemberHashes.Add(ignoreCase ? fieldName.ToLowerInvariant().GetHashCode() : fieldName.GetHashCode());
                fieldInfo.SetValue(null, newValue);
            }

            // Set static properties.
            for (int propertyIndex = 0; propertyIndex < typeInfo.StaticPropertyNames.Count; propertyIndex++) {
                string propertyName = typeInfo.StaticPropertyNames[propertyIndex];
                var propertyInfo = typeInfo.StaticPropertyInfos[propertyIndex];

                if (!doc.TryGetValue(propertyName, ignoreCase, out var valueDoc)) {
                    if (propertyIndex < typeInfo.NumRequiredStaticProperties) {
                        missingRequiredMemberNames ??= new List<string>();
                        missingRequiredMemberNames.Add(propertyName);
                    }
                    continue;
                }

                object newValue = typeInfo.SourceInfoMemberName == propertyName ? doc.SourceInformation
                    : ReadValueOfType(propertyInfo.PropertyType, propertyInfo.GetValue(null), valueDoc, options);
                setMemberHashes.Add(ignoreCase ? propertyName.ToLowerInvariant().GetHashCode() : propertyName.GetHashCode());
                propertyInfo.SetValue(null, newValue);
            }

            // Check whether any required members in the type were not set.
            if (missingRequiredMemberNames != null && missingRequiredMemberNames.Count > 0) {
                throw new MissingFieldsException(doc, $"Missing doc fields: {JoinList(missingRequiredMemberNames, ", ")}");
            }

            // Check whether any fields in the doc were unused.
            if ((options & ReificationOptions.AllowExtraFields) == 0 && setMemberHashes.Count != doc.Count) {
                var extraDocFields = new List<string>();

                foreach (var kv in doc.Pairs) {
                    int docKeyHash = (ignoreCase ? kv.Key.ToLowerInvariant() : kv.Key).GetHashCode();
                    if (!setMemberHashes.Contains(docKeyHash)) {
                        extraDocFields.Add(kv.Key);
                    }
                }

                if (extraDocFields.Count > 0) {
                    throw new ExtraFieldsException(doc, $"Extra doc fields: {JoinList(extraDocFields, ", ")}");
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
            object setRef = obj;
            bool containedMember = SetMember(typeof(T), ref setRef, fieldName, doc, options);
            obj = (T) setRef;
            return containedMember;
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
        public bool SetFieldOnStruct<T>(ref T obj, string fieldName, DocNode doc, ReificationOptions? options = null) where T : struct {
            object setRef = obj;
            bool containedMember = SetMember(typeof(T), ref setRef, fieldName, doc, options);
            obj = (T) setRef;
            return containedMember;
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
                            addMethod?.Invoke(existing, new[] {ReadValueOfType(setEntryType, null, value, options)});
                        }

                        return existing;
                    }

                    // Nullable<T>
                    if (genericTypeDef == typeof(Nullable<>)) {
                        var comparisonOptions = (options & ReificationOptions.CaseSensitive) == 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        if (doc.Type == DocNodeType.Scalar && string.Equals(doc.StringValue, "null", comparisonOptions)) {
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
                try {
                    if (typeInfo.PostDoc != null) {
                        existing = typeInfo.PostDoc.Invoke(null, new[] {existing});
                    }
                    // Call a manually-registered PostDoc if it exists
                    else if (RegisteredPostDocs.TryGetValue(targetType, out var postDocFunc)) {
                        existing = postDocFunc.Invoke(existing);
                    }
                } catch (TargetInvocationException e) {
                    if (e.InnerException == null) {
                        throw;
                    }
                    throw e.InnerException;
                }

                return existing;
            } catch (Exception e) when (!((e as ParseException)?.HasNode ?? false)) {
                throw new ParseException(doc, $"Encountered exception", e);
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
                if (typeInfo.FieldNames.Count + typeInfo.PropertyNames.Count != 1) {
                    throw new ParseException(doc, $"Trying to set a value of type: {type} {typeInfo.FieldNames.Count + typeInfo.PropertyNames.Count}"
                        + $" from value of wrong type: " +
                        (doc.Type == DocNodeType.Scalar ? doc.StringValue : doc.Type.ToString()));
                }

                if (typeInfo.FieldNames.Count > 0) {
                    var fieldInfo = typeInfo.FieldInfos[0];
                    fieldInfo.SetValue(setCopy, ReadValueOfType(fieldInfo.FieldType, fieldInfo.GetValue(setCopy), doc, options));
                }

                if (typeInfo.PropertyNames.Count > 0) {
                    var propertyInfo = typeInfo.PropertyInfos[0];
                    propertyInfo.SetValue(setCopy, ReadValueOfType(propertyInfo.PropertyType, propertyInfo.GetValue(setCopy), doc, options));
                }

                obj = setCopy;
                return;
            }

            bool ignoreCase = (options & ReificationOptions.CaseSensitive) != ReificationOptions.CaseSensitive;

            // Set the fields on the object.
            var setMemberHashes = new List<int>();
            List<string> missingRequiredMembers = null;

            // Set fields.
            for (int fieldIndex = 0; fieldIndex < typeInfo.FieldNames.Count; fieldIndex++) {
                string fieldName = typeInfo.FieldNames[fieldIndex];
                var fieldInfo = typeInfo.FieldInfos[fieldIndex];

                if (!doc.TryGetValue(fieldName, ignoreCase, out var valueDoc)) {
                    if (fieldIndex < typeInfo.NumRequiredFields) {
                        missingRequiredMembers ??= new List<string>();
                        missingRequiredMembers.Add(fieldName);
                    }
                    continue;
                }

                object newValue = typeInfo.SourceInfoMemberName == fieldName ? doc.SourceInformation
                    : ReadValueOfType(fieldInfo.FieldType, fieldInfo.GetValue(setCopy), valueDoc, options);
                setMemberHashes.Add(ignoreCase ? fieldName.ToLowerInvariant().GetHashCode() : fieldName.GetHashCode());
                fieldInfo.SetValue(setCopy, newValue);
            }

            // Set properties.
            for (int propertyIndex = 0; propertyIndex < typeInfo.PropertyNames.Count; propertyIndex++) {
                string propertyName = typeInfo.PropertyNames[propertyIndex];
                var propertyInfo = typeInfo.PropertyInfos[propertyIndex];

                if (!doc.TryGetValue(propertyName, ignoreCase, out var valueDoc)) {
                    if (propertyIndex < typeInfo.NumRequiredFields) {
                        missingRequiredMembers ??= new List<string>();
                        missingRequiredMembers.Add(propertyName);
                    }
                    continue;
                }

                object newValue = typeInfo.SourceInfoMemberName == propertyName ? doc.SourceInformation
                    : ReadValueOfType(propertyInfo.PropertyType, propertyInfo.GetValue(setCopy), valueDoc, options);
                setMemberHashes.Add(ignoreCase ? propertyName.ToLowerInvariant().GetHashCode() : propertyName.GetHashCode());
                propertyInfo.SetValue(setCopy, newValue);
            }

            // Throw an error if any required fields in the class were unset
            if (missingRequiredMembers != null) {
                throw new MissingFieldsException(doc, $"Missing doc fields: {JoinList(missingRequiredMembers, ", ")}");
            }

            // Check whether any fields in the doc were unused
            if (setMemberHashes.Count != doc.Count) {
                var extraDocFields = new List<string>();
                foreach (var kv in doc.Pairs) {
                    int docKeyHash = (ignoreCase ? kv.Key.ToLowerInvariant() : kv.Key).GetHashCode();
                    if (!setMemberHashes.Contains(docKeyHash)) {
                        extraDocFields.Add(kv.Key);
                    }
                }
                throw new ExtraFieldsException(doc, $"Extra doc fields: {JoinList(extraDocFields, ", ")}");
            }

            obj = setCopy;
        }

        /// <summary>
        /// Sets a single field or property value on an object from the given DocNode dict.
        ///
        /// This is mostly only useful as a helper when writing FromDoc's
        /// </summary>
        /// <param name="type">The type of obj</param>
        /// <param name="obj">The object to set the field on</param>
        /// <param name="memberName">The name of the field to set</param>
        /// <param name="doc">A yaml dictionary to grab the value from</param>
        /// <param name="options">Reification options override</param>
        /// <returns>true if we successfully set the field, false otherwise</returns>
        /// <exception cref="ExtraFieldsException">If the field does not exist as a member of <paramref name="type"/> and extra fields are disallowed</exception>
        /// <exception cref="MissingFieldsException">If the field is marked as mandatory and is missing in the yaml doc</exception>
        bool SetMember(Type type, ref object obj, string memberName, DocNode doc, ReificationOptions? options = null) {
            if (doc == null) {
                return false;
            }

            var typeInfo = reflectionCache.GetTypeInfo(type);

            options ??= Configs.Settings.DefaultReifierOptions;

            // Grab global settings
            bool ignoreCase = (options & ReificationOptions.CaseSensitive) == 0;
            bool allowExtra = (options & ReificationOptions.AllowExtraFields) == ReificationOptions.AllowExtraFields;

            bool docHasKey = doc.TryGetValue(memberName, ignoreCase, out var valueDoc);

            // Look for it in the fields list
            for (int fieldIndex = 0; fieldIndex < typeInfo.FieldNames.Count; ++fieldIndex) {
                if (!string.Equals(typeInfo.FieldNames[fieldIndex], memberName, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                    continue;
                }

                if (!docHasKey) {
                    if (fieldIndex < typeInfo.NumRequiredFields) {
                        throw new MissingFieldsException(doc, $"Missing doc field: {memberName}");
                    }
                    return false;
                }

                var fieldInfo = typeInfo.FieldInfos[fieldIndex];
                fieldInfo.SetValue(obj, ReadValueOfType(fieldInfo.FieldType, fieldInfo.GetValue(obj), valueDoc, options));
                return true;
            }

            // Look for it in the properties list
            for (int propertyIndex = 0; propertyIndex < typeInfo.PropertyNames.Count; ++propertyIndex) {
                if (!string.Equals(typeInfo.PropertyNames[propertyIndex], memberName, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                    continue;
                }

                if (!docHasKey) {
                    if (propertyIndex < typeInfo.NumRequiredProperties) {
                        throw new MissingFieldsException(doc, $"Missing doc field: {memberName}");
                    }
                    return false;
                }

                var propertyInfo = typeInfo.PropertyInfos[propertyIndex];
                propertyInfo.SetValue(obj, ReadValueOfType(propertyInfo.PropertyType, propertyInfo.GetValue(obj), valueDoc, options));
                return true;
            }

            // Look for it in the static fields list
            for (int fieldIndex = 0; fieldIndex < typeInfo.StaticFieldNames.Count; ++fieldIndex) {
                if (!string.Equals(typeInfo.StaticFieldNames[fieldIndex], memberName, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                    continue;
                }

                if (!docHasKey) {
                    return false;
                }

                var fieldInfo = typeInfo.StaticFieldInfos[fieldIndex];
                fieldInfo.SetValue(obj, ReadValueOfType(fieldInfo.FieldType, fieldInfo.GetValue(obj), valueDoc, options));
                return true;
            }

            // Look for it in the static properties list
            for (int propertyIndex = 0; propertyIndex < typeInfo.StaticPropertyNames.Count; ++propertyIndex) {
                if (!string.Equals(typeInfo.StaticPropertyNames[propertyIndex], memberName, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                    continue;
                }

                if (!docHasKey) {
                    return false;
                }

                var propertyInfo = typeInfo.StaticPropertyInfos[propertyIndex];
                propertyInfo.SetValue(obj, ReadValueOfType(propertyInfo.PropertyType, propertyInfo.GetValue(obj), valueDoc, options));
                return true;
            }

            if (!allowExtra) {
                throw new ExtraFieldsException(doc, $"Could not find a member named {memberName} on type {type.Name}");
            }

            return false;
        }
    }
}
