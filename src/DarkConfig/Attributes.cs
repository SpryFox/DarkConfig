using System;

namespace DarkConfig {
    /// If the field annotated with a Mandatory, or any field on a Mandatory class,
    /// is not present in the YAML, DarkConfig will complain, regardless of other
    /// settings.
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct)]
    public class ConfigMandatoryAttribute : Attribute { }

    /// If an AllowMissing field, or any field on an AllowMissing class, is not
    /// present in the YAML, DarkConfig will not complain, regardless of other
    /// settings.
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct)]
    public class ConfigAllowMissingAttribute : Attribute { }

    /// If a field has the Ignore attribute, it will be completely ignored by
    /// DarkConfig; not set, not checked, it's as if it wasn't on the class in the
    /// first place.
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ConfigIgnoreAttribute : Attribute { }

    /// If a field has the SourceInformation attribute then the field is
    /// automatically populated with DocNode.SourceInformation by SetFieldsOnObject()
    /// Useful if you do validation or want better error reporting after reification
    /// Use #if flags to remove this in production code where it's not needed
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ConfigSourceInformationAttribute : Attribute { }

    /// Specifies a specific named value that should be read from the yaml
    /// and assigned to this field or property.  Useful when you'd prefer to use
    /// different names in C# and yaml.
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ConfigKeyAttribute : Attribute {
        public string Key;

        /// <summary>
        /// Read the value corresponding to <paramref name="key"/> when
        /// reifying this value, rather than the value associated with the
        /// key that matches this field/property value's name.
        /// </summary>
        /// <param name="key">The substitute key</param>
        public ConfigKeyAttribute(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                throw new ArgumentNullException(nameof(key));
            }

            Key = key.Trim();
        }
    }

    /// Marks this type as a polymorphic union of it's parent type and indicates it's key
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigUnionAttribute : Attribute {
        public string Key;

        /// <summary>
        /// When parsing the parent type, if the key is <paramref name="key"/> then this type will be
        /// parsed instead.
        /// </summary>
        /// <param name="key">The substitute key</param>
        public ConfigUnionAttribute(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                throw new ArgumentNullException(nameof(key));
            }
            Key = key.Trim();
        }
    }
}
