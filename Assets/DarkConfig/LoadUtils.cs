using System.Collections.Generic;
using DarkConfig;
using System;

namespace DarkConfig {
    public class LoadUtils {
        public static void SetParentDefaults<K, V>(
            ref Dictionary<K, V> container,
            DocNode doc,
            Func<V, K> getBasedOn,
            string[] unparentableFieldNames = null) {
            // clear existing values before the reify; because later we bake them
            var fields = typeof(V).GetFields();
            if(container != null) {
                foreach(var kv in container) {
                    foreach(var field in fields) {
                        field.SetValue(kv.Value, GetDefault(field.FieldType));
                    }
                }
            }

            Config.Reify(ref container, doc);

            var parentRelationships = new Dictionary<V, V>();

            // hook up parent references
            foreach (var kv in container) {
                var val = kv.Value;
                var basedOn = getBasedOn(val);
                if(basedOn == null) continue;
                if (!container.ContainsKey(basedOn)) {
                    Config.Log(LogVerbosity.Error,
                        string.Format("In file {0}, {1} is based on {2}, which doesn't exist",
                            doc.SourceInformation, val, basedOn));
                    continue;
                }

                parentRelationships[val] = container[basedOn];
            }

            // set fields from the parents
            foreach (var kv in container) {
                var val = kv.Value;
                foreach (var field in fields) {
                    if (field.IsSpecialName) continue;
                        if(unparentableFieldNames != null) {
                        bool shouldNotParentThisField = false;
                        for(int i = 0; i < unparentableFieldNames.Length; i++) {
                            if(field.Name == unparentableFieldNames[i]) {
                                shouldNotParentThisField = true;
                                break;
                            }
                        }
                        if(shouldNotParentThisField) continue;
                    }

                    var fieldValue = GetParentedFieldValue(field, val, parentRelationships, 0);
                    field.SetValue(val, fieldValue);
                }
            }
        }

        static object GetParentedFieldValue<V>(
                System.Reflection.FieldInfo field,
                V conf,
                Dictionary<V, V> parentRelationships,
                int recursionDepth) {

            var fieldValue = field.GetValue(conf);
            V parent;
            if(!parentRelationships.TryGetValue(conf, out parent)) return fieldValue;
            if(parent == null) return fieldValue;
            if (recursionDepth > 100) {
                Config.Log(LogVerbosity.Error, 
                    string.Format("Might be a loop in the basedOn references at: {0}, parent {1}", conf, parent));
                return fieldValue;
            }
            if (fieldValue == null) {
                // need to get the default from the parent
                return GetParentedFieldValue(field, parent, parentRelationships, recursionDepth + 1);
            } else {
                return fieldValue;
            }
        }

        static object GetDefault(System.Type type) {
            if(type.IsValueType) return System.Activator.CreateInstance(type);
            return null;
        }
    }
}