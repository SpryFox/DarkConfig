using System.Collections.Generic;
using System;

namespace DarkConfig {
    public static class LoadUtils {
        public static void SetParentDefaults<K, V>(ref Dictionary<K, V> container, DocNode doc, Func<V, K> getBasedOn, string[] unparentableFieldNames = null) {
            // clear existing values before the reify; because later we bake them
            var fields = typeof(V).GetFields();
            if (container != null) {
                foreach (var kv in container) {
                    foreach (var field in fields) {
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
                if (basedOn == null) continue;
                if (!container.ContainsKey(basedOn)) {
                    Config.LogError($"In file {doc.SourceInformation}, {val} is based on {basedOn}, which doesn't exist");
                    continue;
                }

                parentRelationships[val] = container[basedOn];
            }

            // set fields from the parents
            foreach (var kv in container) {
                var val = kv.Value;
                foreach (var field in fields) {
                    if (field.IsSpecialName) continue;
                    if (unparentableFieldNames != null) {
                        bool shouldNotParentThisField = false;
                        for (int i = 0; i < unparentableFieldNames.Length; i++) {
                            if (field.Name == unparentableFieldNames[i]) {
                                shouldNotParentThisField = true;
                                break;
                            }
                        }

                        if (shouldNotParentThisField) continue;
                    }

                    var fieldValue = GetParentedFieldValue(field, val, parentRelationships, 0);
                    field.SetValue(val, fieldValue);
                }
            }
        }

        static object GetParentedFieldValue<V>(System.Reflection.FieldInfo field, V conf, Dictionary<V, V> parentRelationships, int recursionDepth) {
            var fieldValue = field.GetValue(conf);
            V parent;
            if (!parentRelationships.TryGetValue(conf, out parent)) {
                return fieldValue;
            }
            if (parent == null) {
                return fieldValue;
            }
            if (recursionDepth > 100) {
                Config.LogError($"Might be a loop in the basedOn references at: {conf}, parent {parent}");
                return fieldValue;
            }
            // if fieldValue is null, we need to get the default from the parent
            return fieldValue ?? GetParentedFieldValue(field, parent, parentRelationships, recursionDepth + 1);
        }

        static object GetDefault(Type type) {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}