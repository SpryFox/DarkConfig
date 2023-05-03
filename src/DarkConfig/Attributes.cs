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
}
