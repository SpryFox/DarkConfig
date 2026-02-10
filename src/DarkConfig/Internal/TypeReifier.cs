using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DarkConfig.Internal {
    public class TypeReifier {
        /// Manually-registered FromDoc's
        public readonly Dictionary<Type, FromDocFunc> RegisteredFromDocs = new();

        /// Manually-registered PostDoc's
        public readonly Dictionary<Type, PostDocFunc> RegisteredPostDocs = new();

        /// Globally-registered ValueRead
        public ObjectReifiedFunc RegisteredObjectReified;

        /////////////////////////////////////////////////

        public TypeReifier() {
            RegisteredFromDocs[typeof(DateTime)] = BuiltInTypeReifiers.FromDateTime;
            RegisteredFromDocs[typeof(TimeSpan)] = BuiltInTypeReifiers.FromTimeSpan;
        }

        internal class ReificationResult {
            internal bool ShouldVerifyMemberHashes;
            internal readonly List<int> SetMemberHashes = new List<int>();

            /// <summary>
            /// Make sure all the members of the document have been consumed by this parsing.
            /// </summary>
            /// <exception cref="ExtraFieldsException"></exception>
            internal void VerifyAllMembersConsumed(Type targetType, DocNode doc, ReificationOptions? options) {
                // Check whether any fields in the doc were unused
                if (ShouldVerifyMemberHashes && doc.Type == DocNodeType.Dictionary && SetMemberHashes.Count != doc.Count) {
                    bool ignoreCase = ((options ?? Configs.Settings.DefaultReifierOptions) & ReificationOptions.CaseSensitive) == 0;
                    var extraDocFields = new List<string>();
                    foreach (var kv in doc.Pairs) {
                        if (!SetMemberHashes.Contains(kv.Key.GetCanonicalHashCode(ignoreCase))) {
                            extraDocFields.Add(kv.Key);
                        }
                    }
                    throw new ExtraFieldsException(targetType, doc, $"Extra doc fields: {JoinList(extraDocFields, ", ")}");
                }
            }

            internal void MergeIn(ReificationResult InlineResult) {
                SetMemberHashes.AddRange(InlineResult.SetMemberHashes);
            }
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

            // Set static fields and properties
            var typeInfo = reflectionCache.GetTypeInfo(type);
            var setMemberHashes = new List<int>();
            List<string> missingRequiredMemberNames = null;
            for (int memberIndex = 0; memberIndex < typeInfo.StaticMemberNames.Count; ++memberIndex) {
                string memberName = typeInfo.StaticMemberNames[memberIndex];

                if (!doc.TryGetValue(memberName, ignoreCase, out var memberDoc)) {
                    if (typeInfo.IsRequired(memberIndex, true)) {
                        missingRequiredMemberNames ??= new List<string>();
                        missingRequiredMemberNames.Add(memberName);
                    }
                    continue;
                }

                if (typeInfo.IsField(memberIndex, true)) {
                    var fieldInfo = (FieldInfo) typeInfo.StaticMemberInfos[memberIndex];
                    object newValue = typeInfo.SourceInfoStaticMemberIndex == memberIndex ? doc.SourceInformation
                        : ReadValueOfType(fieldInfo.FieldType, fieldInfo.GetValue(null), memberDoc, options);
                    setMemberHashes.Add(memberName.GetCanonicalHashCode(ignoreCase));
                    fieldInfo.SetValue(null, newValue);
                } else {
                    var propertyInfo = (PropertyInfo) typeInfo.StaticMemberInfos[memberIndex];
                    object newValue = typeInfo.SourceInfoStaticMemberIndex == memberIndex ? doc.SourceInformation
                        : ReadValueOfType(propertyInfo.PropertyType, propertyInfo.GetValue(null), memberDoc, options);
                    setMemberHashes.Add(memberName.GetCanonicalHashCode(ignoreCase));
                    propertyInfo.SetValue(null, newValue);
                }
            }

            // Check whether any required members in the type were not set.
            if (missingRequiredMemberNames is {Count: > 0}) {
                throw new MissingFieldsException(
                    type: type,
                    node: doc,
                    message: $"Missing doc fields: {JoinList(missingRequiredMemberNames, ", ")}",
                    requiredFieldsCount: typeInfo.NumRequiredFields,
                    missingFieldsCount: missingRequiredMemberNames.Count);
            }

            // Check whether any fields in the doc were unused.
            if ((options & ReificationOptions.AllowExtraFields) == 0 && setMemberHashes.Count != doc.Count) {
                var extraDocFields = new List<string>();
                foreach (var kv in doc.Pairs) {
                    if (!setMemberHashes.Contains(kv.Key.GetCanonicalHashCode(ignoreCase))) {
                        extraDocFields.Add(kv.Key);
                    }
                }
                throw new ExtraFieldsException(type, doc, $"Extra doc fields: {JoinList(extraDocFields, ", ")}");
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
        /// <param name="verifyAllMembersConsumed">true to perform validation that all members are consumed, false to allow extra fields</param>
        /// <returns>An updated version of <paramref name="existing"/> if it was not null,
        /// or a new instance of <paramref name="targetType"/> containing new data from <paramref name="doc"/></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ParseException"></exception>
        public object ReadValueOfType(Type targetType, object existing, DocNode doc, ReificationOptions? options, bool verifyAllMembersConsumed = true) {
            var result = new ReificationResult();
            existing = ReadValueOfTypeWithoutExtraFieldsValidation(targetType, existing, doc, result, options);
            if (verifyAllMembersConsumed) {
                result.VerifyAllMembersConsumed(targetType, doc, options);
            }
            if (RegisteredObjectReified != null && existing != null) {
                RegisteredObjectReified.Invoke(existing, doc);
            }
            return existing;
        }

        private object ReadValueOfTypeWithoutExtraFieldsValidation(Type targetType, object existing, DocNode doc, ReificationResult result, ReificationOptions? options) {
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

                bool ignoreCase = (options & ReificationOptions.CaseSensitive) == 0;

                // Enums
                if (targetType.IsEnum) {
                    try {
                        return Enum.Parse(targetType, doc.StringValue, ignoreCase);
                    } catch (ArgumentException) {
                        string[] enumNames = Enum.GetNames(targetType);
                        throw new Exception($"Failed to parse '{doc.StringValue}' as {targetType}. Options are: {String.Join(", ", enumNames)}");
                    }
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
                        ExpectDictionary(doc, false);
                        existing ??= Activator.CreateInstance(targetType);
                        var existingDict = (System.Collections.IDictionary) existing;

                        var typeParameters = targetType.GetGenericArguments();
                        var keyType = typeParameters[0];
                        var valueType = typeParameters[1];
                        var keyNode = new ComposedDocNode(DocNodeType.Scalar, sourceDocNode: doc); // can reuse this one object
                        var readKeyHashes = new List<int>();

                        // create/update all pairs in the doc
                        foreach ((string docKey, var docValue) in doc.Pairs) {
                            keyNode.StringValue = docKey;
                            object readKey = ReadValueOfType(keyType, null, keyNode, options);
                            object existingValue = existingDict.Contains(readKey) ? existingDict[readKey] : null;
                            existingDict[readKey] = ReadValueOfType(valueType, existingValue, docValue, options);
                            readKeyHashes.Add(readKey.GetHashCode());

                            result.SetMemberHashes.Add(docKey.GetCanonicalHashCode(ignoreCase));
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
                        ExpectList(doc, false);
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
                        ExpectList(doc, false);

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
                            addMethod?.Invoke(existing, new[] {
                                ReadValueOfType(setEntryType, null, value, options)
                            });
                        }

                        return existing;
                    }

                    // Nullable<T>
                    if (genericTypeDef == typeof(Nullable<>)) {
                        var comparisonOptions = ignoreCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
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
                if (typeInfo.FromDocString != null && doc.Type == DocNodeType.Scalar) {
                    try {
                        existing = typeInfo.FromDocString.Invoke(null, new[] {existing, doc.StringValue});
                    } catch (TargetInvocationException e) {
                        if (e.InnerException != null) {
                            throw e.InnerException;
                        }
                        throw;
                    }
                } else if (typeInfo.FromDocStringEx != null && doc.Type == DocNodeType.Scalar) {
                    try {
                        existing = typeInfo.FromDocStringEx.Invoke(null, new[] {existing, doc.StringValue, doc.SourceFile, doc.SourceNode});
                    } catch (TargetInvocationException e) {
                        if (e.InnerException != null) {
                            throw e.InnerException;
                        }
                        throw;
                    }
                } else if (typeInfo.FromDoc != null) {
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
                    result.ShouldVerifyMemberHashes = true;
                    if (typeInfo.UnionKeys != null) {
                        if (doc.Type == DocNodeType.List) {
                            throw new ParseException(doc, $"Encountered a list when trying to parse {targetType}");
                        }

                        if (doc.Type == DocNodeType.Scalar) {
                            if (typeInfo.UnionKeys.TryGetValue(doc.StringValue, out var subType, ignoreCase)) {
                                // support empty object without needing {}
                                if (typeInfo.NumRequiredFields != 0) {
                                    throw new MissingFieldsException(
                                        targetType,
                                        doc,
                                        $"Type {subType} has multiple required fields and so cannot be specified without a body.",
                                        typeInfo.NumRequiredFields,
                                        typeInfo.NumRequiredFields);
                                }

                                existing = Activator.CreateInstance(subType);
                            } else {
                                throw new ParseException(doc, $"Could not parse {targetType} -- {doc.StringValue} is not a valid type");
                            }
                        } else {
                            int pairIndex = 0;
                            object parsedValue = null;
                            foreach (var pair in doc.Pairs) {
                                if (typeInfo.UnionKeys.TryGetValue(pair.Key, out var subType, ignoreCase)) {
                                    var subTypeInfo = reflectionCache.GetTypeInfo(subType);
                                    if (subTypeInfo.IsUnionInline) {
                                        // ensure this is the first key of the keys in this type (because we may be inline in something else)
                                        bool isFirstKey = true;
                                        int testPairIndex = 0;
                                        foreach (var testPair in doc.Pairs) {

                                            // only need to test the pairs above us
                                            if (!isFirstKey || testPairIndex >= pairIndex) {
                                                break;
                                            }

                                            // make sure the pairs above us are not one of our's
                                            foreach (var ourMemberName in subTypeInfo.MemberNames) {
                                                if (testPair.Key == ourMemberName) {
                                                    isFirstKey = false;
                                                    break;
                                                }
                                            }
                                            testPairIndex++;
                                        }
                                        if (isFirstKey) {
                                            parsedValue = ReadValueOfTypeWithoutExtraFieldsValidation(subType, null, doc, result, options);
                                            break;
                                        }
                                    } else {
                                        result.SetMemberHashes.Add(pair.Key.GetCanonicalHashCode(ignoreCase));
                                        parsedValue = ReadValueOfType(subType, null, pair.Value, options);
                                        break;
                                    }
                                }
                                pairIndex++;
                            }
                            if (parsedValue == null) {
                                throw new ParseException(doc,
                                                         $"Could not parse {targetType} -- none of the keys are a valid type: {JoinList(doc.Pairs.Select(pair => pair.Key).ToList(), ", ")}\n"
                                                        +
                                                         $"Expected keys are: {JoinList(typeInfo.UnionKeys.Select(pair => pair.Item1).ToList(), ", ")}");
                            } else {
                                existing = parsedValue;
                            }
                        }
                    } else {
                        existing ??= Activator.CreateInstance(targetType);
                        SetFieldsOnObjectWithoutExtraFieldsValidation(targetType, ref existing, doc, result, options);
                    }
                }

                // Call a PostDoc function for this type if it exists.
                if (existing != null) {
                    try {
                        if (typeInfo.PostDoc != null) {
                            existing = typeInfo.PostDoc.Invoke(null, new[] {
                                existing
                            });
                        }
                        // Call a manually-registered PostDoc if it exists
                        else if (RegisteredPostDocs.TryGetValue(targetType, out var postDocFunc)) {
                            existing = postDocFunc.Invoke(existing);
                        }
                        if (typeInfo.ApplySourceInfo != null) {
                            typeInfo.ApplySourceInfo.Invoke(existing, new[] {
                                doc
                            });
                        }
                    } catch (TargetInvocationException e) {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException ?? e).Throw();
                    }
                }

                return existing;
            } catch (Exception e) when (!((e as ParseException)?.HasNode ?? false)) {
                throw new ParseException(doc, "Encountered exception", e);
            }
        }

        /////////////////////////////////////////////////

        readonly ReflectionCache reflectionCache = new ReflectionCache();

        /////////////////////////////////////////////////

        /// String.Join for Lists. Only used for logging.
        static string JoinList(IReadOnlyList<string> args, string joinStr) {
            var sb = new System.Text.StringBuilder();

            int i = 0;
            foreach (string arg in args) {
                sb.Append(arg);
                if (i < args.Count - 1) {
                    sb.Append(joinStr);
                }

                i++;
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
            var result = new ReificationResult {ShouldVerifyMemberHashes = true};
            SetFieldsOnObjectWithoutExtraFieldsValidation(type, ref obj, doc, result, options);
            result.VerifyAllMembersConsumed(type, doc, options);
        }

        void ExpectList(DocNode doc, bool strict) {
            if (doc.Type == DocNodeType.Scalar) {
                if (string.IsNullOrWhiteSpace(doc.StringValue)) {
                    throw new ParseException(doc, "Nothing specified where a list (\"[value1, value2]\" or multiple lines each prefixed with a \"-\") was expected.");
                }
                throw new ParseException(doc, $"Expected a list (\"[value1, value2]\" or multiple lines each prefixed with a \"-\") but got the string \"{doc.StringValue}\" instead.");
            }
            if (doc.Type == DocNodeType.Dictionary && (strict || doc.Count > 0)) {
                throw new ParseException(doc, "Expected a list (\"[value1, value2]\" or multiple lines each prefixed with a \"-\") but got a dictionary (\"key: value\") instead.");
            }
        }

        void ExpectDictionary(DocNode doc, bool strict) {
            if (doc.Type == DocNodeType.Scalar) {
                if (String.IsNullOrWhiteSpace(doc.StringValue)) {
                    throw new ParseException(doc, "Nothing specified where a dictionary (\"key: value\") was expected.");
                }
                throw new ParseException(doc, $"Expected a dictionary (\"key: value\") but got the string \"{doc.StringValue}\" instead.");
            }
            if (doc.Type == DocNodeType.List && (strict || doc.Count > 0)) {
                throw new ParseException(doc, "Expected a dictionary  (\"key: value\") but got a list (\"[value1, value2]\" or multiple lines each prefixed with a \"-\") instead.");
            }
        }

        /// <exception cref="MissingFieldsException"></exception>
        void SetFieldsOnObjectWithoutExtraFieldsValidation(Type type, ref object obj, DocNode doc, ReificationResult result, ReificationOptions? options) {
            if (type == null) { throw new ArgumentNullException(nameof(type)); }
            if (obj == null) { throw new ArgumentNullException(nameof(obj)); }
            if (doc == null) { throw new ArgumentNullException(nameof(doc)); }

            options ??= Configs.Settings.DefaultReifierOptions;

            var typeInfo = reflectionCache.GetTypeInfo(type);

            bool ignoreCase = (options & ReificationOptions.CaseSensitive) == 0;

            int numRequiredMembers = typeInfo.NumRequiredFields + typeInfo.NumRequiredProperties;
            bool singleProperty = numRequiredMembers == 1;
            bool isInline = typeInfo.MemberOptions.Count > 0 && (typeInfo.MemberOptions[0] & ReflectionCache.TypeInfo.MemberOptionFlags.Inline) != 0;
            if (singleProperty && (doc.Type != DocNodeType.Dictionary || isInline)) {
                // check if any optional members are specified and don't do this if they are
                bool containsOptionalValue = false;
                if (doc.Type == DocNodeType.Dictionary && typeInfo.NumOptionalFields > 0) {
                    for (int memberIndex = 0; memberIndex < typeInfo.MemberNames.Count; ++memberIndex) {
                        if (!typeInfo.IsRequired(memberIndex, false)) {
                            string memberName = typeInfo.MemberNames[memberIndex];
                            if (doc.ContainsKey(memberName, ignoreCase)) {
                                containsOptionalValue = true;
                                break;
                            }
                        }
                    }
                }

                if (!containsOptionalValue) {
                    // ==== Special Case ====
                    // Allow specifying object types with a single required property or field as a scalar value in configs, or if the property is marked as inline, as any
                    // type.
                    // This is syntactic sugar that lets us wrap values in classes or make simple classes more pleasant to author YAML for.
                    object newValue = ReadValueOfType(typeInfo.GetMemberType(0), typeInfo.GetMemberValue(obj, 0), doc, options, !isInline);
                    typeInfo.SetMemberValue(obj, 0, newValue);

                    // Don't verify the set member hashes, because that happens inside the `ReadValueOfType` call instead.
                    result.ShouldVerifyMemberHashes = false;

                    return;
                }
            }

            ExpectDictionary(doc, true);

            // Set fields and properties.
            result.SetMemberHashes.Capacity = Math.Max(result.SetMemberHashes.Capacity, result.SetMemberHashes.Count + typeInfo.MemberNames.Count);

            List<string> missingRequiredMembers = null;
            for (int memberIndex = 0; memberIndex < typeInfo.MemberNames.Count; ++memberIndex) {
                var memberOptions = typeInfo.MemberOptions[memberIndex];

                if (memberOptions.HasFlag(ReflectionCache.TypeInfo.MemberOptionFlags.Inline)) {
                    ReificationResult inlineResult = new();
                    object inlineValue = null;
                    var MemberType = typeInfo.GetMemberType(memberIndex);
                    try {
                        inlineValue = ReadValueOfTypeWithoutExtraFieldsValidation(MemberType, typeInfo.GetMemberValue(obj, memberIndex), doc, inlineResult, options);
                    } catch (MissingFieldsException missingFieldsException) {
                        // if the inline type is optional, allow the error just don't write the field if every required field is not set
                        if (typeInfo.IsRequired(memberIndex, false)) throw;
                        if (missingFieldsException.RequiredFieldsCount != missingFieldsException.MissingFieldsCount) throw;
                        if (missingFieldsException.ParsedType != MemberType) throw;
                    }
                    if (inlineValue != null) {
                        typeInfo.SetMemberValue(obj, memberIndex, inlineValue);
                        result.MergeIn(inlineResult);
                    }
                    continue;
                }

                string memberName = typeInfo.MemberNames[memberIndex];
                if (!doc.TryGetValue(memberName, ignoreCase, out var memberDoc)) {
                    if (typeInfo.IsRequired(memberIndex, false)) {
                        missingRequiredMembers ??= new List<string>();
                        missingRequiredMembers.Add(memberName);
                    }

                    continue;
                }

                object newValue = typeInfo.SourceInfoMemberIndex == memberIndex ? doc.SourceInformation
                    : ReadValueOfType(typeInfo.GetMemberType(memberIndex), typeInfo.GetMemberValue(obj, memberIndex), memberDoc, options);
                typeInfo.SetMemberValue(obj, memberIndex, newValue);

                result.SetMemberHashes.Add(memberName.GetCanonicalHashCode(ignoreCase));
            }

            // Throw an error if any required fields in the class were unset
            if (missingRequiredMembers != null) {
                throw new MissingFieldsException(
                    type: type,
                    node: doc,
                    message: $"Missing doc fields: {JoinList(missingRequiredMembers, ", ")}",
                    requiredFieldsCount: typeInfo.NumRequiredFields,
                    missingFieldsCount: missingRequiredMembers.Count);
            }
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

            // Grab global settings
            bool ignoreCase = ((options ?? Configs.Settings.DefaultReifierOptions) & ReificationOptions.CaseSensitive) == 0;
            bool allowExtra = ((options ?? Configs.Settings.DefaultReifierOptions) & ReificationOptions.AllowExtraFields) != 0;

            var typeInfo = reflectionCache.GetTypeInfo(type);

            bool docHasKey = doc.TryGetValue(memberName, ignoreCase, out var valueDoc);

            // Look for it in the instanced members list
            for (int memberIndex = 0; memberIndex < typeInfo.MemberNames.Count; ++memberIndex) {
                if (!string.Equals(typeInfo.MemberNames[memberIndex], memberName, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                    continue;
                }

                if (!docHasKey) {
                    if (typeInfo.IsRequired(memberIndex, false) || (options != null && (options & ReificationOptions.AllowMissingFields) == 0)) {
                        throw new MissingFieldsException(
                            type: type,
                            node: doc,
                            message: $"Missing doc field: {memberName}",
                            requiredFieldsCount: typeInfo.NumRequiredFields,
                            missingFieldsCount: 1);
                    }
                    return false;
                }

                if (typeInfo.IsField(memberIndex, false)) {
                    var fieldInfo = (FieldInfo) typeInfo.MemberInfos[memberIndex];
                    fieldInfo.SetValue(obj, ReadValueOfType(fieldInfo.FieldType, fieldInfo.GetValue(obj), valueDoc, options));
                } else {
                    var propertyInfo = (PropertyInfo) typeInfo.MemberInfos[memberIndex];
                    propertyInfo.SetValue(obj, ReadValueOfType(propertyInfo.PropertyType, propertyInfo.GetValue(obj), valueDoc, options));
                }

                return true;
            }

            // Look for it in the static members list
            for (int memberIndex = 0; memberIndex < typeInfo.StaticMemberNames.Count; ++memberIndex) {
                if (!string.Equals(typeInfo.StaticMemberNames[memberIndex], memberName, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                    continue;
                }

                if (!docHasKey) {
                    if (typeInfo.IsRequired(memberIndex, true)) {
                        throw new MissingFieldsException(
                            type: type,
                            node: doc,
                            message: $"Missing doc field: {memberName}",
                            requiredFieldsCount: typeInfo.NumRequiredFields,
                            missingFieldsCount: 1);
                    }
                    return false;
                }

                if (typeInfo.IsField(memberIndex, true)) {
                    var fieldInfo = (FieldInfo) typeInfo.StaticMemberInfos[memberIndex];
                    fieldInfo.SetValue(obj, ReadValueOfType(fieldInfo.FieldType, fieldInfo.GetValue(obj), valueDoc, options));
                } else {
                    var propertyInfo = (PropertyInfo) typeInfo.StaticMemberInfos[memberIndex];
                    propertyInfo.SetValue(obj, ReadValueOfType(propertyInfo.PropertyType, propertyInfo.GetValue(obj), valueDoc, options));
                }

                return true;
            }

            if (!allowExtra) {
                throw new ExtraFieldsException(type, doc, $"Could not find a member named {memberName} on type {type.Name}");
            }
            return false;
        }
    }
}
