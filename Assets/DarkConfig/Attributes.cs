using System;

// If the field annotated with a Mandatory, or any field on a Mandatory class,
// is not present in the YAML, DarkConfig will complain, regardless of other
// settings.
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
public class ConfigMandatoryAttribute : Attribute {
}

// If an AllowMissing field, or any field on an AllowMissing class, is not
// present in the YAML, DarkConfig will not complain, regardless of other
// settings.
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
public class ConfigAllowMissingAttribute : Attribute {
}

// If a field has the Ignore attribute, it will be completely ignored by
// DarkConfig; not set, not checked, it's as if it wasn't on the class in the
// first place.
[AttributeUsage(AttributeTargets.Field)]
public class ConfigIgnoreAttribute : Attribute {
}