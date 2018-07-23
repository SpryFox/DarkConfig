using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DarkConfig {
    public delegate object FromDocDelegate(object obj, DocNode doc);

    public class ConfigReifier {
        /// <summary>
        /// Default options for refication.  Change this if you want to change
        /// DarkConfig behavior without passing in parameters to each call.
        /// </summary>
        public static ConfigOptions DefaultOptions = ConfigOptions.AllowMissingExtraFields | ConfigOptions.CaseSensitive;

        /// <summary>
        /// Sets up *obj* based on the contents of the parsed document *doc*
        /// So if obj is a Thing:
        ///   public class Thing {
        ///      float m1;
        ///      string m2;
        ///   }
        ///
        /// You can create a new instance, or set an existing instance's fields with this parsed document:
        ///  {"m1":1.0, "m2":"test"}
        ///
        /// *obj* can be null; if it is it gets assigned a new instance based on its type and the contents of *doc* (this is why the parameter is a ref)
        /// 
        /// Works on static and private variables, too.
        /// </summary>
        public static void Reify<T>(ref T obj, DocNode doc, ConfigOptions? options = null) {
            obj = (T)ValueOfType(typeof(T), obj, doc, options);
        }

		/// <summary>
		/// Sets up *obj* based on the contents of the parsed document *doc* with a type override.
		/// Useful for (eg) instaiating concrete classes of an interface based on a keyword.
		/// So if obj is a Thing:
		///   public class Thing {
		///      float m1;
		///      string m2;
		///   }
		///
		/// You can create a new instance, or set an existing instance's fields with this parsed document:
		///  {"m1":1.0, "m2":"test"}
		///
		/// *obj* can be null; if it is it gets assigned a new instance based on its type and the contents of *doc* (this is why the parameter is a ref)
		/// 
		/// Works on static and private variables, too.
		/// </summary>
		public static void Reify<T>(ref T obj, Type objType, DocNode doc, ConfigOptions? options = null) {
			obj = (T)ValueOfType(objType, obj, doc, options);
		}

        /// <summary>
        /// Sets up static variables (and only static variables) on type *T* based on the contents of the parsed document *doc*
        ///
        /// Ignores any fields in *doc* that are for non-static fields.
        /// </summary>
        public static void ReifyStatic<T>(DocNode doc, ConfigOptions? options = null) {
            ReifyStatic(typeof(T), doc, options);
        }

        /// <summary>
        /// Same as ReifyStatic<T>, but with a type argument instead of a generic.  Static classes can't be used in generics, so use this version instead.
        /// </summary>
        public static void ReifyStatic(Type type, DocNode doc, ConfigOptions? options = null) {
            object dummyObj = null;
            SetFieldsOnObject(type, ref dummyObj, doc, DefaultedOptions(options));
        }

        /// <summary>
        /// Register a handler for loading a particular type.
        /// 
        /// The delegate accepts two parameters: the existing object (if any), and the 
        /// DocNode that is meant to update the object.  It should attempt to update
        /// the object in-place, or if that's not possible, to return a new instance
        /// of the correct type.
        /// The return value is the updated/created object.
        /// </summary>
        public static void Register<T>(FromDocDelegate del) {
            Register(typeof(T), del);
        }

        public static void Register(Type t, FromDocDelegate del) {
            s_fromDocs[t] = del;
        }

        static object CallPostDoc(Type serializedType, object obj) {
            var postDoc = ReflectionCache.GetPostDoc(serializedType);
            if (postDoc != null) {
                try {
                    obj = postDoc.Invoke(null, new[] { obj });
                } catch (System.Reflection.TargetInvocationException e) {
                    throw e.InnerException;
                }
            }

            return obj;
        }

        static object ValueOfType(Type fieldType, object existing, DocNode value, ConfigOptions? options) {
            try {
                if (fieldType == typeof(System.Boolean)) {
                    return Convert.ToBoolean(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                // floating-point value types
                if (fieldType == typeof(System.Single)) {
                    return Convert.ToSingle(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (fieldType == typeof(System.Double)) {
                    return Convert.ToDouble(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (fieldType == typeof(System.Decimal)) {
                    return Convert.ToDecimal(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                // integral value types
                if (fieldType == typeof(System.SByte)) {
                    return Convert.ToSByte(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (fieldType == typeof(System.Byte)) {
                    return Convert.ToByte(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (fieldType == typeof(System.Char)) {
                    return Convert.ToChar(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (fieldType == typeof(System.Int16)) {
                    return Convert.ToInt16(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (fieldType == typeof(System.UInt16)) {
                    return Convert.ToUInt16(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (fieldType == typeof(System.Int32)) {
                    return Convert.ToInt32(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (fieldType == typeof(System.UInt32)) {
                    return Convert.ToUInt32(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (fieldType == typeof(System.Int64)) {
                    return Convert.ToInt64(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (fieldType == typeof(System.UInt64)) {
                    return Convert.ToUInt64(value.StringValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (fieldType == typeof(string)) {
                    return value.StringValue;
                }
                if (fieldType.IsEnum) {        // AudioRolloffMode, "Custom" => AudioRolloffMode.Custom
                    return Enum.Parse(fieldType, value.StringValue, true);
                }
                if(fieldType == typeof(DocNode)) {
                    return value;
                }
                if(fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                    if(value.Type == DocNodeType.Scalar && value.StringValue == "null") return null;
                    var innerType = Nullable.GetUnderlyingType(fieldType);
                    return ValueOfType(innerType, existing, value, options);
                }
                if (s_fromDocs.ContainsKey(fieldType)) {
                    existing = s_fromDocs[fieldType](existing, value);
                    return CallPostDoc(fieldType, existing);
				}
                if (fieldType.IsArray) {                    // [1,2,3,4,5] => new int[] { 1,2,3,4,5 }
                    Type arrTyp = fieldType.GetElementType();
                    if (fieldType.GetArrayRank() == 2) {
                        var firstList = value[0];
                        var destArr = Array.CreateInstance(arrTyp, firstList.Count, value.Count);
                        int j = 0;
                        foreach (DocNode subList in value.Values) {
                            int i = 0;
                            foreach (var val in subList.Values.Select(item => ValueOfType(arrTyp, null, item, options))) {
                                destArr.SetValue(val, new int[] { i, j });
                                i++;
                            }
                            j++;
                        }
                        return destArr;
                    } else {
                        var iexisting = (Array)existing;
                        if(iexisting == null) {
                            iexisting = Array.CreateInstance(arrTyp, value.Count);
                        } else if(iexisting.Length != value.Count) {
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
                        var iexisting = (System.Collections.IDictionary)existing;
                        Type kType = typeParameters[0];
                        Type vType = typeParameters[1];
                        ComposedDocNode keyNode = new ComposedDocNode(DocNodeType.Scalar, sourceInformation: value.SourceInformation);  // can reuse this one object
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

                        var iexisting = (System.Collections.IList)existing;
                        while(iexisting.Count > value.Count) {
                            iexisting.RemoveAt(iexisting.Count - 1);
                        }
                        for (int i = 0; i < iexisting.Count; i++) {
                            var existingElt = iexisting[i];
                            var updatedElt = ValueOfType(typeParameters[0], existingElt, value[i], options);
                            iexisting[i] = updatedElt;
                        }
                        while(iexisting.Count < value.Count) {
                            var newElt = ValueOfType(typeParameters[0], null, value[iexisting.Count], options);
                            iexisting.Add(newElt);
                        }
                        return existing;
                    }
                }
                var fromDocMethod = ReflectionCache.GetFromDoc(fieldType);
                if (fromDocMethod != null) { // if there's a custom parser method on the class, delegate all work to that
                    // TODO: this doesn't do inherited FromDoc methods properly, but it should
                    try {
                        existing = fromDocMethod.Invoke(null, new[] { existing, value });
                        return CallPostDoc(fieldType, existing);
                    } catch (System.Reflection.TargetInvocationException e) {
                        throw e.InnerException;
                    }
                }
                if (fieldType.IsClass) {
                    if (existing == null) {
                        existing = CreateInstance(fieldType, value, options);
                        return CallPostDoc(fieldType, existing);
                    } else {
                        SetFieldsOnObject(fieldType, ref existing, value, DefaultedOptions(options));
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
                        SetFieldsOnObject(fieldType, ref existing, value, DefaultedOptions(options));
                        return CallPostDoc(fieldType, existing);
                    }
                }
            } catch (Exception e) {
                throw new ParseException("Exception based on document starting at: " + value.SourceInformation, e);
            }

            throw new System.NotSupportedException("Don't know how to update value of type " + fieldType.ToString());
        }



        /// <summary>
        /// Create an instance of an object and immediately set fields on it from the document. 
        /// The type of instance is supplied via the generic parameter.
        /// </summary>
        public static T CreateInstance<T>(DocNode dict, ConfigOptions? options = null) where T : new() {
            var obj = (object)System.Activator.CreateInstance<T>();
            SetFieldsOnObject(ref obj, dict, DefaultedOptions(options));
            return (T)obj;
        }

        /// <summary>
        /// Create an instance of an object and immediately set fields on it from the document. 
        /// The type of instance is supplied explicitly as the first argument.
        /// Requires a zero-args constructor on the type though it doesn't enforce that.
        /// </summary>
        public static object CreateInstance(Type t, DocNode dict, ConfigOptions? options = null) {
            var obj = System.Activator.CreateInstance(t);
            SetFieldsOnObject(ref obj, dict, DefaultedOptions(options));
            return obj;
        }

        /// <summary>
        /// Sets all members on the object *obj* (which must not be null) from *dict*.
        /// Expects *obj* to be a plain class, but if it's a boxed struct it will work as well.
        /// </summary>
        public static void SetFieldsOnObject<T>(ref T obj, DocNode dict, ConfigOptions? options = null) where T : class {
            Config.Assert(obj != null, "Can't SetFields on null");
            Type type = typeof(T);
            if(type == typeof(object)) {
                // caller is using an object, but that is not the real type
                type = obj.GetType();
            }
            var setCopy = (object)obj;
            SetFieldsOnObject(type, ref setCopy, dict, DefaultedOptions(options));
            obj = (T)setCopy;
        }

        /// <summary>
        /// Sets all members on the struct *obj* (which must not be null) from *dict*.
        /// </summary>
        public static void SetFieldsOnStruct<T>(ref T obj, DocNode dict, ConfigOptions? options = null) where T : struct {
            Type type = typeof(T);
            var setCopy = (object)obj;
            SetFieldsOnObject(type, ref setCopy, dict, DefaultedOptions(options));
            obj = (T)setCopy;
        }

        static ConfigOptions DefaultedOptions(ConfigOptions? options) {
            if(options == null) return DefaultOptions;
            return options.Value;
        }

        public static bool IsDelegate(Type type) {
            // http://mikehadlow.blogspot.com/2010/03/how-to-tell-if-type-is-delegate.html
            return typeof (MulticastDelegate).IsAssignableFrom(type.BaseType);
        }

        static Type GetFirstNonObjectBaseClass(Type t) {
            var curr = t;
            while(curr.BaseType != null && curr.BaseType != typeof(System.Object)) {
                curr = curr.BaseType;
            }
            return curr;
        }

        /// Sets all members on the object *obj* based on the appropriate key from *doc*
        static void SetFieldsOnObject(Type type, ref object obj, DocNode doc, ConfigOptions options) {
            if (doc == null) return;

            bool caseInsensitive = (options & ConfigOptions.CaseSensitive) != ConfigOptions.CaseSensitive;
            bool checkMissing = (options & ConfigOptions.AllowMissingFields) != ConfigOptions.AllowMissingFields;
            bool checkExtra = (options & ConfigOptions.AllowExtraFields) != ConfigOptions.AllowExtraFields;
            var classAttrs = ReflectionCache.GetCustomAttributes(type);
            for(int i = 0; i < classAttrs.Length; i++) {
                if(classAttrs[i] is ConfigMandatoryAttribute) { checkMissing = true; continue; }
                if(classAttrs[i] is ConfigAllowMissingAttribute) { checkMissing = false; continue; }
            }
            if(GetFirstNonObjectBaseClass(type).ToString() == "UnityEngine.Object") {
                // Unity Objects have a lot of fields, it never makes sense to set most of them from configs
                checkMissing = false;
            }

            var setCopy = obj;
            if(doc.Type != DocNodeType.Dictionary) {
                // special-case behavior, set the first instance field on it from the doc
                var allFields = ReflectionCache.GetInstanceFields(type);
                Config.Assert(allFields.Length == 1, "Trying to set a field of type: ",
                              type, allFields.Length, "from value of wrong type:", 
                              doc.Type == DocNodeType.Scalar ? doc.StringValue : doc.Type.ToString(),
                              "at",
                              doc.SourceInformation);
                SetField(allFields[0], ref setCopy, doc, options);
                obj = setCopy;
                return;
            }

            var typeFields = new HashSet<string>();
            var usedFields = new HashSet<string>();
            var lowercasedDocKeys = new Dictionary<string, string>();
            if(caseInsensitive) {
                foreach(var kv in doc.Pairs) lowercasedDocKeys[kv.Key.ToLower()] = kv.Key;
            }

            bool anyFieldMandatory = false;

            foreach (ReflectionCache.CachedFieldInfo cf in ReflectionCache.GetStrippedFields(type, caseInsensitive)) {
                var f = cf.field;
                // get attributes for the field
                bool isMandatory = false;
                bool allowMissing = false;
                bool ignore = false;
                var attrs = ReflectionCache.GetCustomAttributes(f);
                for(int i = 0; i < attrs.Length; i++) {
                    if(attrs[i] is ConfigMandatoryAttribute) { isMandatory = true; anyFieldMandatory = true; continue; }
                    if(attrs[i] is ConfigAllowMissingAttribute) { allowMissing = true; continue; }
                    if(attrs[i] is ConfigIgnoreAttribute) { ignore = true; continue; }
                }
                if(IsDelegate(f.FieldType)) ignore = true; // never report postdocs as present or missing

                // figure out the canonical name for the field
                string strippedName = cf.strippedName;

                // do meta stuff based on attributes/validation
                if(ignore) {
                    usedFields.Add(strippedName); // pretend like we set it
                    continue;
                }
                if(checkMissing || isMandatory) typeFields.Add(strippedName);

                string docKey = null;
                if(caseInsensitive) lowercasedDocKeys.TryGetValue(strippedName, out docKey);
                if(docKey == null) docKey = strippedName;
                if (doc.ContainsKey(docKey)) {
                    usedFields.Add(strippedName);
                    SetField(f, ref setCopy, doc[docKey], options);
                } else if(allowMissing) {
                    usedFields.Add(strippedName);  // pretend like we set it
                }
            }

            // validation ---------
            if(checkExtra) {
                // check whether any fields in the doc were unused
                var extra = new List<string>();
                foreach(var kv in doc.Pairs) {
                    var key = kv.Key;
                    if(caseInsensitive) key = ReflectionCache.GetLowercase(key);
                    if(!usedFields.Contains(key)) extra.Add(key);
                }
                if(extra.Count > 0) {
                    throw new ParseException("Extra doc fields: " + JoinList(extra, ", ") + " " + doc.SourceInformation, null);
                }
            }

            if(checkMissing || anyFieldMandatory) {
                // check whether any fields in the class were unset
                var missing = new List<string>();
                foreach(var typeField in typeFields) {
                    if(!usedFields.Contains(typeField)) missing.Add(typeField);
                }
                if(missing.Count > 0) {
                    throw new ParseException("Missing doc fields: " + JoinList(missing, ", ") + " " + doc.SourceInformation, null);
                }
            }
            obj = setCopy;
        }

        static void SetField(FieldInfo f, ref object obj, DocNode value, ConfigOptions? options) {
            if(obj == null && !f.IsStatic) return; // silently don't set non-static fields
            Type fieldType = f.FieldType;
            var existing = f.GetValue(obj);
            var updated = ValueOfType(fieldType, existing, value, options);
            var setCopy = obj; // needed for structs
            f.SetValue(setCopy, updated);
            obj = setCopy;
        }

        /// convenience method to parse an enum value out of a string
        static T GetEnum<T>(string v) {
            return (T)Enum.Parse(typeof(T), v);
        }

        static string JoinList(List<string> args, string joinStr) {
            var sb = new System.Text.StringBuilder();
            for(int i = 0; i < args.Count; i++) {
                sb.Append(args[i]);
                if(i < args.Count - 1) {
                    sb.Append(joinStr);
                }
            }
            return sb.ToString();
        }

        protected static Dictionary<Type, FromDocDelegate> s_fromDocs = new Dictionary<Type, FromDocDelegate>();
    }
}
