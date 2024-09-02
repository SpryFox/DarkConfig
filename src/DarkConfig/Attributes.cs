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

    /// <summary>
    /// Marks this type as a polymorphic union of its parent type and indicates the key whose presence implies this type
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigUnionAttribute : Attribute {
        public readonly string Key;

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

    /// <summary>
    /// Marks this type as a polymorphic union of its parent type and indicates the key whose presence implies this type
    /// The key for this union is in the same doc as it's properties. The key is always the first property specified.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigUnionInlineAttribute : Attribute {
        public readonly string Key;

        /// <summary>
        /// When parsing the parent type, if the key is <paramref name="key"/> then this type will be
        /// parsed instead.
        /// </summary>
        /// <param name="key">The substitute key</param>
        public ConfigUnionInlineAttribute(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                throw new ArgumentNullException(nameof(key));
            }
            Key = key.Trim();
        }
    }

    /// If the field annotated with inline then we will look for it's properties in the same doc as the parent
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ConfigInlineAttribute : Attribute { }

    /// Name of this type in documentation. For generic types, "&lt;0&gt;" indicates the first template parameter, "&lt;1&gt;" the second, and so on.
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigDocumentationNameAttribute : Attribute {
        public readonly string Value;

        public ConfigDocumentationNameAttribute(string value) {
            Value = value;
        }
    }

    /// Description of this type or field
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
    public class ConfigDocumentationDescriptionAttribute : Attribute {
        public readonly string Value;

        public ConfigDocumentationDescriptionAttribute(string value) {
            Value = value;
        }
    }

    /// Example yaml of this type
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class ConfigDocumentationExampleAttribute : Attribute {
        public readonly string Value;

        public ConfigDocumentationExampleAttribute(string value) {
            Value = value;
        }
    }
}
